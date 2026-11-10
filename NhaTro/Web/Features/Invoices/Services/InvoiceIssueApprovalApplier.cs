using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Áp dụng yêu cầu SW-11: chỉ chuyển Draft → Issued khi Chủ trọ duyệt. Kiểm lại toàn bộ
/// điều kiện phát hành tại thời điểm duyệt vì hợp đồng/kỳ/dòng có thể đã đổi lúc chờ duyệt,
/// và dùng <c>ExpectedToken</c> để không ghi đè hóa đơn đã bị luồng khác sửa.
/// </summary>
public sealed class InvoiceIssueApprovalApplier(TimeProvider clock) : IApprovalApplier
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string OperationCode => "SW-11";

    public async Task ApplyAsync(
        ManagerApprovalRequest request,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Invoice
            || !int.TryParse(
                request.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out var invoiceId)
            || invoiceId <= 0
            || request.PropertyId is null
            || request.ExpectedToken is not { Length: > 0 })
        {
            throw Invalid(InvoiceErrorCodes.InvoiceNotFound, InvoiceErrorMessages.InvoiceNotFound);
        }

        InvoiceIssueApprovalPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<InvoiceIssueApprovalPayload>(
                request.PayloadJson, PayloadJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ApprovalApplyException(
                InvoiceErrorCodes.InvalidStatus, InvoiceErrorMessages.InvoiceNotFound, ex);
        }

        if (payload is null || payload.InvoiceId != invoiceId)
        {
            throw Invalid(InvoiceErrorCodes.InvoiceNotFound, InvoiceErrorMessages.InvoiceNotFound);
        }

        var invoice = await db.Invoices
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == invoiceId, ct);
        if (invoice is null)
        {
            throw Invalid(InvoiceErrorCodes.InvoiceNotFound, InvoiceErrorMessages.InvoiceNotFound);
        }

        var contract = await db.Contracts.SingleOrDefaultAsync(
            item => item.ContractId == invoice.ContractId, ct);
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

        // Ai đó đã phát hành (hoặc hóa đơn không còn là nháp) trong lúc chờ duyệt: không phát hành hai lần.
        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw Invalid(InvoiceErrorCodes.InvalidStatus, InvoiceErrorMessages.InvalidStatus);
        }

        try
        {
            InvoiceService.ValidateContractAndPeriod(contract, invoice.PeriodStart, invoice.PeriodEnd);
        }
        catch (Exception ex) when (ex is IInvoiceServiceError error)
        {
            throw new ApprovalApplyException(error.ErrorCode, ex.Message, ex);
        }

        var total = InvoiceCalculationRules.TotalOf(invoice.Items
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .Select(item => new InvoiceItemDto
            {
                ItemType = item.ItemType,
                Description = item.Description,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Amount,
            }));
        if (invoice.Items.Count == 0 || total != invoice.TotalAmount)
        {
            throw Invalid(InvoiceErrorCodes.TotalMismatch, InvoiceErrorMessages.TotalMismatch);
        }

        var now = clock.UtcNow();
        invoice.Status = InvoiceStatus.Issued;
        invoice.IssuedAt = now;
        invoice.UpdatedAt = now;
        db.Entry(invoice).Property(item => item.RowVersion).OriginalValue = request.ExpectedToken;
    }

    private static ApprovalApplyException Invalid(string errorCode, string message) =>
        new(errorCode, message);
}
