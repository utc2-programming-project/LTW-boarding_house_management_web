using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;

namespace Web.Features.Contracts.Rules;

/// <summary>
/// Quy tắc gia hạn / thanh lý hợp đồng P4 (SW-07). Dùng chung cho thao tác trực tiếp của
/// Chủ trọ và applier khi Chủ trọ duyệt yêu cầu của Quản lý.
/// </summary>
public static class ContractLifecycleOperations
{
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
            SnapshotPrice = room.StandardPrice,
            DepositAmount = MoneyFormatter.RoundVnd(room.StandardPrice * 0.20m),
            Status = ContractStatus.Approved,
        };

        var tenantIds = await db.ContractMembers
            .Where(member => member.ContractId == current.ContractId)
            .Select(member => member.TenantId)
            .ToListAsync(cancellationToken);

        db.Contracts.Add(renewal);
        db.ContractMembers.AddRange(tenantIds.Select(tenantId => new ContractMember
        {
            ContractId = renewal.ContractId,
            TenantId = tenantId,
        }));

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

        if (!hasOtherCurrent)
        {
            room.Status = RoomStatus.Available;
        }
    }
}
