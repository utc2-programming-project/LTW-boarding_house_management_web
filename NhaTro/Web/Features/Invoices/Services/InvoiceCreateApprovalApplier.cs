using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Áp dụng yêu cầu SW-10: hóa đơn nháp chỉ được ghi sổ SAU khi Chủ trọ duyệt, vì lúc Quản lý
/// gửi yêu cầu thì nghiệp vụ chưa chạm bảng hóa đơn. Chạy trong transaction của
/// <see cref="ManagerApprovalService"/> nên không tự SaveChanges/Commit.
/// </summary>
public sealed class InvoiceCreateApprovalApplier(TimeProvider clock) : IApprovalApplier
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string OperationCode => "SW-10";

    public async Task ApplyAsync(
        ManagerApprovalRequest request,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Contract
            || !Guid.TryParse(request.TargetKey, out var contractId)
            || request.PropertyId is null)
        {
            throw Invalid(InvoiceErrorCodes.ContractNotFound, InvoiceErrorMessages.ContractNotFound);
        }

        InvoiceDraftApprovalPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<InvoiceDraftApprovalPayload>(
                request.PayloadJson, PayloadJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ApprovalApplyException(
                InvoiceErrorCodes.InvalidItem, InvoiceErrorMessages.InvalidItem, ex);
        }

        // Payload phải nói về đúng hợp đồng mà khóa yêu cầu trỏ tới, không tin một mình nó.
        if (payload is null || payload.ContractId != contractId)
        {
            throw Invalid(InvoiceErrorCodes.ContractNotFound, InvoiceErrorMessages.ContractNotFound);
        }

        IReadOnlyList<InvoiceItemInputDto> items;
        try
        {
            items = InvoiceDraftRules.NormalizeItems(payload.Items);
        }
        catch (InvoiceValidationException ex)
        {
            throw new ApprovalApplyException(ex.ErrorCode, ex.Message, ex);
        }

        var contract = await db.Contracts.SingleOrDefaultAsync(
            item => item.ContractId == contractId, ct);
        if (contract is null)
        {
            throw Invalid(InvoiceErrorCodes.ContractNotFound, InvoiceErrorMessages.ContractNotFound);
        }

        var roomPropertyId = await db.Rooms
            .IgnoreQueryFilters()
            .Where(room => room.Id == contract.RoomId)
            .Select(room => (int?)room.PropertyId)
            .SingleOrDefaultAsync(ct);
        if (roomPropertyId != request.PropertyId.Value)
        {
            throw Invalid(InvoiceErrorCodes.Forbidden, InvoiceErrorMessages.Forbidden);
        }

        try
        {
            InvoiceService.ValidateContractAndPeriod(contract, payload.PeriodStart, payload.PeriodEnd);
        }
        catch (Exception ex) when (ex is IInvoiceServiceError error)
        {
            throw new ApprovalApplyException(error.ErrorCode, ex.Message, ex);
        }

        // Nguồn chi phí có thể đã đổi hoặc đã có hóa đơn cùng kỳ trong lúc yêu cầu chờ duyệt:
        // chặn bằng lỗi có tên còn hơn để DbUpdateException phá transaction của quyết định duyệt.
        if (await db.Invoices.AnyAsync(
                invoice => invoice.ContractId == contractId
                    && invoice.PeriodStart == payload.PeriodStart,
                ct))
        {
            throw Invalid(InvoiceErrorCodes.DuplicateInvoice, InvoiceErrorMessages.DuplicateInvoice);
        }

        db.Invoices.Add(new Invoice
        {
            ContractId = contractId,
            PeriodStart = payload.PeriodStart,
            PeriodEnd = payload.PeriodEnd,
            Status = InvoiceStatus.Draft,
            TotalAmount = InvoiceCalculationRules.CalculateTotal(items),
            CreatedAt = clock.UtcNow(),
            Items = InvoiceService.ToEntities(items),
        });
    }

    private static ApprovalApplyException Invalid(string errorCode, string message) =>
        new(errorCode, message);
}
