using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Audit.Entities;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Contracts.DTOs;
using Web.Features.Notifications;
using Web.Features.Utilities.Entities;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;
using Web.Features.Vehicles.Services;

namespace Web.Features.Contracts.Rules;

/// <summary>
/// Quy tắc gia hạn / thanh lý hợp đồng P4 (SW-07). Dùng chung cho thao tác trực tiếp của
/// Chủ trọ và applier khi Chủ trọ duyệt yêu cầu của Quản lý.
/// </summary>
public static class ContractLifecycleOperations
{
    public static class ContractAuditActions
    {
        public const string DepositUpdated = "ContractDepositUpdated";
        public const string Renewed = "ContractRenewed";
        public const string Liquidated = "ContractLiquidated";
    }
    /// <summary>Hợp đồng đã duyệt và hôm nay nằm trong thời hạn (bao gồm cả ngày kết thúc).</summary>
    public static bool IsActionable(
        ContractStatus status,
        DateTime startDate,
        DateTime endDate,
        DateOnly today) =>
        status == ContractStatus.Approved
        && DateOnly.FromDateTime(startDate) <= today
        && today <= DateOnly.FromDateTime(endDate);

    public static void EnsureRenewalEndDate(DateTime currentEndDate, DateTime newEndDate)
    {
        if (DateOnly.FromDateTime(newEndDate) <= DateOnly.FromDateTime(currentEndDate))
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidRenewalEndDate,
                ContractErrorCodes.InvalidRenewalEndDate,
                nameof(RenewContractRequestDto.EndDate));
        }
    }

    /// <summary>Đã có hợp đồng kế nhiệm đã duyệt cho cùng phòng hay chưa.</summary>
    public static Task<bool> HasApprovedSuccessorAsync(
        ApplicationDbContext db,
        Guid contractId,
        int roomId,
        DateTime startDate,
        CancellationToken cancellationToken = default) =>
        db.Contracts.AnyAsync(
            other => other.RoomId == roomId
                && other.ContractId != contractId
                && other.Status == ContractStatus.Approved
                && other.StartDate > startDate,
            cancellationToken);

    public static void EnsureDepositEditable(Contract contract, DateOnly today, bool hasSuccessor)
    {
        if (!IsActionable(contract.Status, contract.StartDate, contract.EndDate, today) || hasSuccessor)
        {
            throw new ContractOperationException(ContractErrorMessages.ContractNotActive, ContractErrorCodes.ContractNotActive);
        }
    }

    public static async Task EnsureNoPendingApprovalAsync(ApplicationDbContext db, Guid contractId, CancellationToken ct = default)
    {
        var key = contractId.ToString("D");
        if (await db.Set<ManagerApprovalRequest>().AnyAsync(x => x.TargetType == ApprovalTargetType.Contract && x.TargetKey == key && x.Status == ApprovalRequestStatus.Pending, ct))
        {
            throw new ContractOperationException(ContractErrorMessages.DuplicatePendingRequest, ContractErrorCodes.DuplicatePendingRequest);
        }
    }

    public static async Task SettlePendingRenewalRequestsAsync(ApplicationDbContext db, Guid contractId, RenewalRequestStatus status, string actorUserId, DateTime decidedAt, string? reason, CancellationToken ct = default)
    {
        var requests = await db.ContractRenewalRequests.Where(x => x.ContractId == contractId && x.Status == RenewalRequestStatus.Pending).ToListAsync(ct);
        foreach (var request in requests)
        {
            request.Status = status;
            request.DecidedByUserId = actorUserId;
            request.DecidedAt = decidedAt;
            request.RejectReason = reason;
        }
    }

    public static async Task CopyFutureServiceRegistrationsAsync(ApplicationDbContext db, Guid fromContractId, Guid toContractId, DateOnly fromMonth, DateTimeOffset createdAt, CancellationToken ct = default)
    {
        var registrations = await db.Set<ContractServiceRegistration>()
            .Where(x => x.ContractId == fromContractId && x.MonthStart >= fromMonth).ToListAsync(ct);
        db.Set<ContractServiceRegistration>().AddRange(registrations.Select(x => new ContractServiceRegistration
        {
            ContractId = toContractId, ServiceCatalogId = x.ServiceCatalogId, MonthStart = x.MonthStart,
            RegisteredByUserId = x.RegisteredByUserId, CreatedAt = createdAt,
        }));
    }

    public static void AddAudit(ApplicationDbContext db, string action, string actorUserId, int propertyId, string metadata, DateTimeOffset occurredAt) =>
        db.Set<AuditLogEntry>().Add(new AuditLogEntry { Category = AuditLogCategory.Business, Action = action, ActorUserId = actorUserId, PropertyId = propertyId, Metadata = metadata, OccurredAt = occurredAt });

    public static Task<bool> HasOverlappingApprovedAsync(
        ApplicationDbContext db,
        int roomId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default) =>
        db.Contracts.AnyAsync(
            other => other.RoomId == roomId
                && other.Status == ContractStatus.Approved
                && other.StartDate <= endDate
                && startDate <= other.EndDate,
            cancellationToken);

    /// <summary>
    /// Tạo hợp đồng kế nhiệm đã duyệt, bắt đầu ngay sau ngày kết thúc hiện tại. Giữ lịch sử
    /// hợp đồng cũ, sao chép khách thuê và chốt snapshot giá / cọc theo giá phòng hiện tại.
    /// </summary>
    public static async Task<Contract> CreateRenewalAsync(
        ApplicationDbContext db,
        Contract current,
        Room room,
        DateTime newEndDate,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        var contractId = Guid.NewGuid();
        var renewal = new Contract
        {
            ContractId = contractId,
            ContractNumber = $"HD-{contractId:N}".ToUpperInvariant(),
            RoomId = current.RoomId,
            StartDate = current.EndDate.AddDays(1),
            EndDate = newEndDate.Date,
            SnapshotPrice = current.SnapshotPrice,
            DepositAmount = current.DepositAmount,
            Status = ContractStatus.Approved,
            PaymentCycleMonths = current.PaymentCycleMonths,
            MeterReadingDay = current.MeterReadingDay,
            Terms = current.Terms,
            CreatedByUserId = current.CreatedByUserId,
            CreatedByRole = current.CreatedByRole,
            CreatedAt = createdAt,
        };

        var members = await db.ContractMembers
            .Where(member => member.ContractId == current.ContractId)
            .Select(member => new { member.TenantId, member.IsRepresentative, member.Relationship })
            .ToListAsync(cancellationToken);

        db.Contracts.Add(renewal);
        db.ContractMembers.AddRange(members.Select(member => new ContractMember
        {
            ContractId = renewal.ContractId,
            TenantId = member.TenantId,
            IsRepresentative = member.IsRepresentative,
            Relationship = member.Relationship,
        }));

        await CopyFutureServiceRegistrationsAsync(db, current.ContractId, renewal.ContractId,
            new DateOnly(renewal.StartDate.Year, renewal.StartDate.Month, 1), createdAt, cancellationToken);

        // Xe đang gửi thuộc hợp đồng cũ phải được gắn sang hợp đồng kế nhiệm ngay,
        // tránh tính phí tháng mới theo hợp đồng đã hết hạn (double-billing / mất phí).
        var activeVehicles = await db.Vehicles
            .Where(vehicle => vehicle.ContractId == current.ContractId
                && vehicle.Status == VehicleStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var vehicle in activeVehicles)
        {
            vehicle.ContractId = renewal.ContractId;
        }

        return renewal;
    }

    /// <summary>
    /// Chuyển hợp đồng sang Đã thanh lý. Phòng chỉ được trả về trạng thái trống khi không còn
    /// hợp đồng đã duyệt nào khác đang hiệu lực hôm nay.
    /// </summary>
    public static async Task LiquidateAsync(
        ApplicationDbContext db,
        Contract contract,
        Room room,
        DateOnly today,
        string actorUserId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        contract.Status = ContractStatus.Liquidated;

        var todayDate = today.ToDateTime(TimeOnly.MinValue);
        var hasOtherCurrent = await db.Contracts.AnyAsync(
            other => other.RoomId == contract.RoomId
                && other.ContractId != contract.ContractId
                && other.Status == ContractStatus.Approved
                && other.StartDate <= todayDate
                && todayDate <= other.EndDate,
            cancellationToken);

        if (room.Status == RoomStatus.Rented && !hasOtherCurrent)
        {
            room.Status = RoomStatus.Available;
        }

        // Mục 2.7/10: thanh lý hợp đồng phải ngừng luôn xe đang gửi, trả ô đỗ và báo cho khách/chủ.
        await DeactivateContractVehiclesAsync(db, contract, room, actorUserId, occurredAt, cancellationToken);
    }

    /// <summary>
    /// Ngừng toàn bộ xe đang Active của hợp đồng khi hợp đồng hết hiệu lực/thanh lý:
    /// trả ô đỗ về trống, đánh dấu Inactive kèm EndedAt, ghi audit và gửi thông báo.
    /// </summary>
    public static async Task<int> DeactivateContractVehiclesAsync(
        ApplicationDbContext db,
        Contract contract,
        Room room,
        string actorUserId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        var activeVehicles = await db.Vehicles
            .Where(vehicle => vehicle.ContractId == contract.ContractId
                && vehicle.Status == VehicleStatus.Active)
            .ToListAsync(cancellationToken);

        if (activeVehicles.Count == 0)
        {
            return 0;
        }

        var slotIds = activeVehicles
            .Where(vehicle => vehicle.ParkingSlotId.HasValue)
            .Select(vehicle => vehicle.ParkingSlotId!.Value)
            .Distinct()
            .ToArray();
        if (slotIds.Length > 0)
        {
            var slots = await db.ParkingSlots
                .Where(slot => slotIds.Contains(slot.Id))
                .ToListAsync(cancellationToken);
            foreach (var slot in slots)
            {
                slot.Status = ParkingSlotStatus.Available;
                slot.CurrentVehicleId = null;
            }
        }

        var reason = VehicleLocalization.TCurrent("ContractLiquidatedReason", "Hợp đồng đã thanh lý");
        foreach (var vehicle in activeVehicles)
        {
            vehicle.Status = VehicleStatus.Inactive;
            vehicle.EndedAt = occurredAt;
            vehicle.EndReason = reason;
            vehicle.ParkingSlotId = null;
        }

        AddAudit(db, "Vehicle.ContractVehiclesDeactivated", actorUserId, room.PropertyId,
            $"ContractId={contract.ContractId};Count={activeVehicles.Count};Reason={reason}", occurredAt);

        var plateList = string.Join(", ",
            activeVehicles.Select(vehicle => VehicleRules.FormatPlateForDisplay(vehicle.PlateNumber)));
        var link = $"/vehicles?propertyId={room.PropertyId}&roomId={room.Id}";
        var notifTitle = VehicleLocalization.TCurrent("ContractLiquidatedNotifTitle", "Hợp đồng đã thanh lý");

        var tenantUserIds = await (
            from member in db.ContractMembers.AsNoTracking()
            join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
            where member.ContractId == contract.ContractId && tenant.UserId != null
            select tenant.UserId!)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var tenantUserId in tenantUserIds)
        {
            db.Set<AppNotification>().Add(new AppNotification
            {
                RecipientUserId = tenantUserId,
                Title = notifTitle,
                Body = VehicleLocalization.TCurrent(
                    "ContractLiquidatedTenantBody",
                    "Hợp đồng phòng {0} đã thanh lý. Xe {1} đã được ngừng gửi.",
                    room.Code, plateList),
                Link = "/my/vehicles",
                CreatedAt = occurredAt,
            });
        }

        var ownerId = await db.Properties.AsNoTracking()
            .Where(property => property.Id == room.PropertyId)
            .Select(property => property.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            db.Set<AppNotification>().Add(new AppNotification
            {
                RecipientUserId = ownerId,
                Title = notifTitle,
                Body = VehicleLocalization.TCurrent(
                    "ContractLiquidatedOwnerBody",
                    "Phòng {0}: {1} xe ({2}) đã được ngừng gửi do thanh lý hợp đồng.",
                    room.Code, activeVehicles.Count, plateList),
                Link = link,
                CreatedAt = occurredAt,
            });
        }

        return activeVehicles.Count;
    }
}
