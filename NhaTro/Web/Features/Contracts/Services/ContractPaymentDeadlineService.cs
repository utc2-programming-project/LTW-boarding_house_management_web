using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Notifications;
using Web.Features.Rooms.Enums;
using Web.Infrastructure.Time;

namespace Web.Features.Contracts.Services;

/// <summary>
/// Quét hợp đồng đã ký nhưng quá hạn thanh toán cọc: huỷ hợp đồng và trả phòng.
/// Chạy mỗi 15 phút, không phụ thuộc việc mở trang.
/// </summary>
public sealed class ContractPaymentDeadlineService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ContractPaymentDeadlineService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SweepAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var now = timeProvider.GetUtcNow();

            var overdueIds = await db.Contracts.IgnoreQueryFilters().AsNoTracking()
                .Where(contract => !contract.IsDeleted
                    && contract.DepositDueAt != null
                    && contract.DepositDueAt <= now
                    && contract.DepositPaidAt == null
                    && contract.Status == ContractStatus.Approved
                    && !db.ContractDepositPayments.Any(payment =>
                        payment.ContractId == contract.ContractId
                        && payment.Status == DepositPaymentStatus.Pending))
                .OrderBy(contract => contract.DepositDueAt)
                .Select(contract => contract.ContractId)
                .Take(100)
                .ToListAsync(cancellationToken);

            foreach (var contractId in overdueIds)
            {
                await CancelOverdueAsync(db, notifications, contractId, now, cancellationToken);
            }

            await SendDepositRemindersAsync(db, notifications, now, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không quét được hợp đồng quá hạn thanh toán cọc.");
        }
    }

    private async Task CancelOverdueAsync(
        ApplicationDbContext db,
        INotificationService notifications,
        Guid contractId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        Contract? contract;
        try
        {
            contract = await db.Contracts
                .FromSqlInterpolated($"SELECT * FROM [Contracts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContractId] = {contractId}")
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);

            if (contract is null
                || contract.IsDeleted
                || contract.DepositPaidAt is not null
                || contract.DepositDueAt is null
                || contract.DepositDueAt > now
                || contract.Status != ContractStatus.Approved
                || await db.ContractDepositPayments.AnyAsync(payment =>
                    payment.ContractId == contract.ContractId
                    && payment.Status == DepositPaymentStatus.Pending, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var propertyId = await (
                from contractRoom in db.Rooms.IgnoreQueryFilters()
                join property in db.Properties.IgnoreQueryFilters() on contractRoom.PropertyId equals property.Id
                where contractRoom.Id == contract.RoomId
                select property.Id)
                .SingleAsync(cancellationToken);

            if (contract.DepositPaidAmount is > 0m && contract.DepositRefundedAt is null)
            {
                contract.DepositRefundAmount = contract.DepositPaidAmount;
            }

            contract.Status = ContractStatus.Cancelled;
            contract.RejectReason = "Quá hạn thanh toán tiền cọc giữ căn.";

            var room = await db.Rooms
                .FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {contract.RoomId}")
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);
            if (room is not null && room.Status == RoomStatus.Rented)
            {
                var today = timeProvider.TodayVietnam().ToDateTime(TimeOnly.MinValue);
                var stillOccupied = await db.Contracts.AnyAsync(other =>
                    other.ContractId != contract.ContractId
                    && other.RoomId == contract.RoomId
                    && other.Status == ContractStatus.Approved
                    && other.StartDate <= today
                    && today <= other.EndDate,
                    cancellationToken);
                if (!stillOccupied)
                {
                    room.Status = RoomStatus.Available;
                    room.UpdatedAt = timeProvider.UtcNow();
                }
            }

            ContractLifecycleOperations.AddAudit(
                db,
                "ContractPaymentExpired",
                "system",
                propertyId,
                JsonSerializer.Serialize(new { contractId, contract.ContractNumber }),
                now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await NotifyAsync(db, notifications, contract, cancellationToken);
    }

    private static async Task SendDepositRemindersAsync(
        ApplicationDbContext db,
        INotificationService notifications,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var within24h = await db.Contracts
            .Where(contract => !contract.IsDeleted
                && contract.Status == ContractStatus.Approved
                && contract.DepositPaidAt == null
                && contract.DepositReminder24hAt == null
                && contract.DepositDueAt != null
                && contract.DepositDueAt > now
                && contract.DepositDueAt <= now.AddHours(24)
                && !db.ContractDepositPayments.Any(payment =>
                    payment.ContractId == contract.ContractId
                    && payment.Status == DepositPaymentStatus.Pending))
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var contract in within24h)
        {
            contract.DepositReminder24hAt = now;
            await NotifyPartiesAsync(db, notifications, contract,
                "Sắp hết hạn thanh toán cọc",
                $"Hợp đồng {contract.ContractNumber} còn dưới 24 giờ để thanh toán cọc.",
                cancellationToken);
        }

        var within6h = await db.Contracts
            .Where(contract => !contract.IsDeleted
                && contract.Status == ContractStatus.Approved
                && contract.DepositPaidAt == null
                && contract.DepositReminder6hAt == null
                && contract.DepositDueAt != null
                && contract.DepositDueAt > now
                && contract.DepositDueAt <= now.AddHours(6)
                && !db.ContractDepositPayments.Any(payment =>
                    payment.ContractId == contract.ContractId
                    && payment.Status == DepositPaymentStatus.Pending))
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var contract in within6h)
        {
            contract.DepositReminder6hAt = now;
            await NotifyPartiesAsync(db, notifications, contract,
                "Sắp hết hạn thanh toán cọc",
                $"Hợp đồng {contract.ContractNumber} còn dưới 6 giờ để thanh toán cọc.",
                cancellationToken);
        }

        if (within24h.Count > 0 || within6h.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task NotifyPartiesAsync(
        ApplicationDbContext db,
        INotificationService notifications,
        Contract contract,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var tenantUserIds = await (
            from member in db.ContractMembers.AsNoTracking()
            join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on member.TenantId equals tenant.Id
            where member.ContractId == contract.ContractId
                && tenant.UserId != null
                && !tenant.IsDeleted
            select tenant.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var userId in tenantUserIds)
        {
            await notifications.NotifyAsync(userId!, title, body, "/my/room");
        }

        var ownerId = await (
            from room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
            join property in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
            where room.Id == contract.RoomId
            select property.OwnerId).FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            await notifications.NotifyAsync(ownerId, title, body, $"/contracts/{contract.ContractId}");
        }
    }

    private static async Task NotifyAsync(
        ApplicationDbContext db,
        INotificationService notifications,
        Contract contract,
        CancellationToken cancellationToken)
    {
        var tenantUserIds = await (
            from member in db.ContractMembers.AsNoTracking()
            join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on member.TenantId equals tenant.Id
            where member.ContractId == contract.ContractId
                && tenant.UserId != null
                && !tenant.IsDeleted
            select tenant.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var body = $"Hợp đồng {contract.ContractNumber} đã bị huỷ vì quá hạn thanh toán tiền cọc.";
        foreach (var userId in tenantUserIds)
        {
            await notifications.NotifyAsync(userId!, "Hợp đồng đã bị huỷ", body, "/my/room");
        }

        var propertyOwnerId = await (
            from room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
            join property in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
            where room.Id == contract.RoomId
            select property.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(propertyOwnerId))
        {
            await notifications.NotifyAsync(
                propertyOwnerId,
                "Hợp đồng quá hạn thanh toán cọc",
                body,
                $"/contracts/{contract.ContractId}");
        }
    }
}
