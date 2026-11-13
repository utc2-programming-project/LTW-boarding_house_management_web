using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Notifications;
using Web.Features.Properties;

namespace Web.Features.Contracts.Services;

public interface IContractDepositPaymentService
{
    Task<ContractDepositPaymentDto?> GetLatestAsync(Guid contractId, PropertyCaller caller, CancellationToken ct = default);

    Task<ContractDepositPaymentDto> SubmitAsync(
        Guid contractId,
        string? transactionCode,
        IReadOnlyList<Guid> proofFileIds,
        PropertyCaller caller,
        CancellationToken ct = default);

    Task<ContractDepositPaymentDto> ConfirmAsync(int paymentId, PropertyCaller caller, CancellationToken ct = default);

    Task<ContractDepositPaymentDto> RejectAsync(
        int paymentId, string reason, PropertyCaller caller, CancellationToken ct = default);
}

/// <summary>Phiếu báo chuyển cọc của khách và bước chủ trọ xác nhận/từ chối.</summary>
public sealed class ContractDepositPaymentService(
    ApplicationDbContext db,
    INotificationService notifications,
    TimeProvider time,
    IInvoiceService? invoices = null,
    ILogger<ContractDepositPaymentService>? logger = null) : IContractDepositPaymentService
{
    public async Task<ContractDepositPaymentDto?> GetLatestAsync(
        Guid contractId, PropertyCaller caller, CancellationToken ct = default)
    {
        await EnsureCanReadAsync(contractId, caller, ct);
        var payment = await db.ContractDepositPayments.AsNoTracking()
            .Where(row => row.ContractId == contractId)
            .OrderByDescending(row => row.RequestedAt)
            .ThenByDescending(row => row.Id)
            .FirstOrDefaultAsync(ct);
        return payment is null ? null : ToDto(payment);
    }

    public async Task<ContractDepositPaymentDto> SubmitAsync(
        Guid contractId,
        string? transactionCode,
        IReadOnlyList<Guid> proofFileIds,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsKhachThue)
        {
            throw new ContractOperationException(ContractErrorMessages.Forbidden, ContractErrorCodes.Forbidden);
        }

        if (proofFileIds.Count == 0)
        {
            throw new ContractValidationException(
                "Vui lòng tải lên ít nhất một minh chứng chuyển cọc.",
                "DepositProofRequired",
                nameof(proofFileIds));
        }

        var contract = await LoadTenantContractAsync(contractId, caller, ct);
        if (contract.DepositAmount <= 0m
            || contract.DepositPaidAt is not null
            || contract.Status is not (ContractStatus.Approved or ContractStatus.Pending or ContractStatus.TenantAccepted))
        {
            throw new ContractOperationException(
                ContractErrorMessages.DepositPaymentNotAllowed,
                ContractErrorCodes.DepositPaymentNotAllowed);
        }

        var hasPending = await db.ContractDepositPayments
            .AnyAsync(row => row.ContractId == contractId && row.Status == DepositPaymentStatus.Pending, ct);
        if (hasPending)
        {
            throw new ContractOperationException(
                ContractErrorMessages.DepositPaymentPending,
                ContractErrorCodes.DepositPaymentPending);
        }

        var now = time.GetUtcNow();
        var payment = new ContractDepositPayment
        {
            ContractId = contractId,
            Amount = contract.DepositAmount,
            TransferredAt = now.UtcDateTime,
            TransactionCode = string.IsNullOrWhiteSpace(transactionCode) ? null : transactionCode.Trim(),
            ProofFileIdsJson = System.Text.Json.JsonSerializer.Serialize(proofFileIds),
            Status = DepositPaymentStatus.Pending,
            RequestedAt = now.UtcDateTime,
            RequestedByUserId = caller.UserId,
        };
        db.ContractDepositPayments.Add(payment);
        await db.SaveChangesAsync(ct);

        var ownerId = await OwnerIdAsync(contractId, ct);
        await notifications.NotifyAsync(
            ownerId ?? string.Empty,
            "Khách báo đã chuyển tiền cọc",
            $"Hợp đồng {contract.ContractNumber}: khách báo đã chuyển {contract.DepositAmount:N0} ₫ tiền cọc.",
            $"/contracts/{contractId}");

        return ToDto(payment);
    }

    public async Task<ContractDepositPaymentDto> ConfirmAsync(
        int paymentId, PropertyCaller caller, CancellationToken ct = default)
    {
        var payment = await LoadManageablePaymentAsync(paymentId, caller, ct);
        if (payment.Status != DepositPaymentStatus.Pending)
        {
            return ToDto(payment);
        }

        var contract = await db.Contracts.SingleAsync(row => row.ContractId == payment.ContractId, ct);
        var now = time.GetUtcNow();
        payment.Status = DepositPaymentStatus.Confirmed;
        payment.ReviewedAt = now.UtcDateTime;
        payment.ReviewedByUserId = caller.UserId;

        contract.DepositPaidAmount = contract.DepositAmount;
        contract.DepositPaidAt = now;
        contract.DepositPaidByUserId = caller.UserId;
        contract.DepositDueAt = null;

        var propertyId = await PropertyIdAsync(contract.ContractId, ct);
        ContractLifecycleOperations.AddAudit(
            db, "ContractDepositConfirmed", caller.UserId, propertyId,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                contractId = contract.ContractId,
                paymentId = payment.Id,
                amount = payment.Amount,
            }), now);
        await db.SaveChangesAsync(ct);

        // Cọc xong mở luôn hóa đơn nháp kỳ thuê đầu để chủ trọ phát hành.
        await TryCreateFirstRentInvoiceAsync(contract, caller, ct);

        await NotifyTenantsAsync(contract, "Tiền cọc đã được xác nhận",
            $"Chủ trọ đã xác nhận nhận {payment.Amount:N0} ₫ tiền cọc của hợp đồng {contract.ContractNumber}.");
        return ToDto(payment);
    }

    private async Task TryCreateFirstRentInvoiceAsync(
        Contract contract, PropertyCaller caller, CancellationToken ct)
    {
        if (invoices is null || !caller.IsChuTro)
        {
            return;
        }

        try
        {
            var start = DateOnly.FromDateTime(contract.StartDate);
            var end = start.AddMonths(Math.Max(1, contract.PaymentCycleMonths)).AddDays(-1);
            var propertyId = await PropertyIdAsync(contract.ContractId, ct);
            var services = await ContractServiceLineLoader.LoadAsync(
                db, propertyId, contract.ContractId, start, ct);

            var items = new List<InvoiceItemInputDto>
            {
                new()
                {
                    ItemType = InvoiceItemType.Rent,
                    Description = "Tiền thuê phòng",
                    Unit = "tháng",
                    Quantity = 1m,
                    UnitPrice = contract.SnapshotPrice,
                    Amount = contract.SnapshotPrice,
                },
            };
            items.AddRange(services
                .Where(service => service.IsRegistered)
                .Select(service => new InvoiceItemInputDto
                {
                    ItemType = InvoiceItemType.Service,
                    Description = service.Name,
                    Unit = service.Unit,
                    Quantity = 1m,
                    UnitPrice = service.UnitPrice,
                    Amount = service.UnitPrice,
                }));

            await invoices.CreateDraftAsync(new CreateInvoiceRequest
            {
                ContractId = contract.ContractId,
                PeriodStart = start,
                PeriodEnd = end,
                DueDate = start.AddDays(5),
                Items = items,
            }, caller, ct);
        }
        catch (Exception ex)
        {
            // Không để lỗi tạo hóa đơn làm hỏng xác nhận cọc; chủ trọ vẫn có thể lập tay.
            logger?.LogWarning(ex, "Không tự tạo được hóa đơn kỳ đầu cho hợp đồng {ContractId}", contract.ContractId);
        }
    }

    public async Task<ContractDepositPaymentDto> RejectAsync(
        int paymentId, string reason, PropertyCaller caller, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ContractValidationException(
                ContractErrorMessages.RejectReasonRequired,
                ContractErrorCodes.RejectReasonRequired,
                nameof(reason));
        }

        var payment = await LoadManageablePaymentAsync(paymentId, caller, ct);
        if (payment.Status != DepositPaymentStatus.Pending)
        {
            return ToDto(payment);
        }

        if (reason.Trim().Length > 500)
        {
            throw new ContractValidationException(
                ContractErrorMessages.RejectReasonTooLong,
                ContractErrorCodes.RejectReasonTooLong,
                nameof(reason));
        }

        var now = time.GetUtcNow();
        payment.Status = DepositPaymentStatus.Rejected;
        payment.ReviewedAt = now.UtcDateTime;
        payment.ReviewedByUserId = caller.UserId;
        payment.RejectionReason = reason.Trim();

        var propertyId = await PropertyIdAsync(payment.ContractId, ct);
        ContractLifecycleOperations.AddAudit(
            db, "ContractDepositRejected", caller.UserId, propertyId,
            System.Text.Json.JsonSerializer.Serialize(new { contractId = payment.ContractId, paymentId = payment.Id }),
            now);
        await db.SaveChangesAsync(ct);

        var contract = await db.Contracts.SingleAsync(row => row.ContractId == payment.ContractId, ct);
        await NotifyTenantsAsync(contract, "Minh chứng cọc bị từ chối", payment.RejectionReason);
        return ToDto(payment);
    }

    private async Task<Contract> LoadTenantContractAsync(
        Guid contractId, PropertyCaller caller, CancellationToken ct)
    {
        var contract = await (
            from row in db.Contracts
            where row.ContractId == contractId
            join member in db.ContractMembers on row.ContractId equals member.ContractId
            join tenant in db.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
            where tenant.UserId == caller.UserId && !tenant.IsDeleted
            select row).FirstOrDefaultAsync(ct);

        return contract ?? throw new ContractOperationException(
            ContractErrorMessages.ContractNotFound, ContractErrorCodes.ContractNotFound);
    }

    private async Task EnsureCanReadAsync(Guid contractId, PropertyCaller caller, CancellationToken ct)
    {
        if (caller.IsKhachThue)
        {
            _ = await LoadTenantContractAsync(contractId, caller, ct);
            return;
        }

        if (caller.IsChuTro)
        {
            var owned = await (
                from contract in db.Contracts
                join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                where contract.ContractId == contractId && property.OwnerId == caller.UserId
                select contract.ContractId).AnyAsync(ct);
            if (owned) return;
        }
        else if (caller.IsQuanLy)
        {
            var managed = await (
                from contract in db.Contracts
                join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                join assignment in db.PropertyManagers on room.PropertyId equals assignment.PropertyId
                where contract.ContractId == contractId
                    && assignment.ManagerUserId == caller.UserId
                    && assignment.RevokedAt == null
                select contract.ContractId).AnyAsync(ct);
            if (managed) return;
        }

        throw new ContractOperationException(ContractErrorMessages.Forbidden, ContractErrorCodes.Forbidden);
    }

    private async Task<ContractDepositPayment> LoadManageablePaymentAsync(
        int paymentId, PropertyCaller caller, CancellationToken ct)
    {
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw new ContractOperationException(ContractErrorMessages.Forbidden, ContractErrorCodes.Forbidden);
        }

        var payment = await db.ContractDepositPayments
            .SingleOrDefaultAsync(row => row.Id == paymentId, ct)
            ?? throw new ContractOperationException(
                ContractErrorMessages.DepositPaymentNotFound, ContractErrorCodes.DepositPaymentNotFound);

        await EnsureCanReadAsync(payment.ContractId, caller, ct);
        return payment;
    }

    private Task<int> PropertyIdAsync(Guid contractId, CancellationToken ct) =>
        (from contract in db.Contracts
         join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
         where contract.ContractId == contractId
         select room.PropertyId).SingleAsync(ct);

    private Task<string?> OwnerIdAsync(Guid contractId, CancellationToken ct) =>
        (from contract in db.Contracts
         join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
         join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
         where contract.ContractId == contractId
         select property.OwnerId).FirstOrDefaultAsync(ct);

    private async Task NotifyTenantsAsync(Contract contract, string title, string body)
    {
        var userIds = await (
            from member in db.ContractMembers.AsNoTracking()
            join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on member.TenantId equals tenant.Id
            where member.ContractId == contract.ContractId && tenant.UserId != null && !tenant.IsDeleted
            select tenant.UserId).Distinct().ToListAsync();
        foreach (var userId in userIds)
        {
            await notifications.NotifyAsync(userId!, title, body, $"/my/contracts/{contract.ContractId}");
        }
    }

    private static ContractDepositPaymentDto ToDto(ContractDepositPayment payment) => new(
        payment.Id,
        payment.ContractId,
        payment.Amount,
        payment.TransferredAt,
        payment.TransactionCode,
        payment.Status,
        payment.RequestedAt,
        payment.ReviewedAt,
        payment.RejectionReason,
        ParseProofIds(payment.ProofFileIdsJson));

    private static IReadOnlyList<Guid> ParseProofIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
