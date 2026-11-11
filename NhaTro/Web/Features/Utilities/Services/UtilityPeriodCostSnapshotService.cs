using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Utilities.Entities;

namespace Web.Features.Utilities.Services;

public sealed class UtilityTariffUnavailableException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);

/// <summary>Làm mới snapshot biểu giá thuộc một chỉ số điện/nước.</summary>
public sealed class UtilityPeriodCostSnapshotService(IUtilityTariffService tariffService)
{
    public const string ElectricScopeCode = "VN-EVN-HOUSEHOLD";
    public const string WaterScopeCode = "HCMC-BENTHANH";

    public async Task RefreshAsync(ApplicationDbContext db, int readingId, CancellationToken cancellationToken = default)
    {
        // Ưu tiên entity đang được theo dõi vì AdjustAsync đã đổi chỉ số nhưng chưa lưu.
        var reading = db.UtilityReadings.Local.SingleOrDefault(item => item.Id == readingId)
            ?? await db.UtilityReadings.AsNoTracking()
                .SingleAsync(item => item.Id == readingId, cancellationToken);

        var contract = await db.Contracts.AsNoTracking()
            .Where(item => item.RoomId == reading.RoomId
                && item.Status == ContractStatus.Approved
                && item.StartDate <= reading.PeriodEnd.ToDateTime(TimeOnly.MaxValue)
                && item.EndDate >= reading.PeriodStart.ToDateTime(TimeOnly.MinValue))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        var residentCount = contract is null ? 1 : Math.Max(1, await db.ContractMembers.CountAsync(
            member => member.ContractId == contract.ContractId, cancellationToken));

        var oldSnapshotIds = await db.UtilityBillingPeriodSnapshots.AsNoTracking()
            // VHT-102 dùng readingId; VHT-51 dùng UtilityReadingPeriod.Id. Không đổi schema tại đây.
            .Where(item => item.BillingPeriodId == readingId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (oldSnapshotIds.Count > 0)
        {
            // Xóa ngay trong transaction của caller để SnapshotForPeriodAsync không đọc snapshot cũ.
            await db.UtilityBillingCostLineSnapshots
                .Where(item => oldSnapshotIds.Contains(item.UtilityBillingPeriodSnapshotId))
                .ExecuteDeleteAsync(cancellationToken);
            await db.UtilityBillingPeriodSnapshots
                .Where(item => oldSnapshotIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var electricUsage = Math.Max(0m, reading.ElectricEnd - reading.ElectricStart);
        var waterUsage = Math.Max(0m, reading.WaterEnd - reading.WaterStart);
        try
        {
            if (electricUsage > 0m)
                await tariffService.SnapshotForPeriodAsync(readingId, TariffType.Electric, ElectricScopeCode,
                    reading.PeriodStart, reading.PeriodEnd, electricUsage, residentCount, cancellationToken);
            if (waterUsage > 0m)
                await tariffService.SnapshotForPeriodAsync(readingId, TariffType.Water, WaterScopeCode,
                    reading.PeriodStart, reading.PeriodEnd, waterUsage, residentCount, cancellationToken);
        }
        catch (UtilityTariffNotConfiguredException exception)
        {
            throw new UtilityTariffUnavailableException(
                $"Chưa cấu hình biểu giá điện/nước cho kỳ {reading.PeriodStart:dd/MM/yyyy} - {reading.PeriodEnd:dd/MM/yyyy}.", exception);
        }
    }
}
