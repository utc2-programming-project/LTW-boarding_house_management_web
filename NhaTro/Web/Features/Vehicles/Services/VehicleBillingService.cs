using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Features.Properties;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Vehicles.Services;

public interface IVehicleBillingService
{
    Task<ContractVehicleBillingSummaryDto> CalculateContractFeeAsync(Guid contractId, CancellationToken ct = default);
    Task<InvoiceItemInputDto?> BuildInvoiceItemInputAsync(Guid contractId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default);
    Task<int> SyncDraftInvoicesForContractAsync(Guid contractId, CancellationToken ct = default);
    Task<int> SyncPropertyDraftInvoicesAsync(int propertyId, PropertyCaller caller, CancellationToken ct = default);
}

/// <summary>
/// V2: Tự động tính phí gửi xe hàng tháng theo 3 loại xe (hoặc quy tắc khu đỗ khi bật EnableParkingZones)
/// và đồng bộ lũy đẳng (idempotent) 1 dòng "Phí gửi xe" vào hóa đơn nháp.
/// Hóa đơn đã phát hành (Issued/Paid) tuyệt đối không tự sửa ngược.
/// </summary>
public sealed class VehicleBillingService(
    ApplicationDbContext db,
    AuditLogService audit,
    TimeProvider clock,
    IStringLocalizer<VehicleResource>? localizer = null) : IVehicleBillingService
{
    /// <summary>Tổng phí gửi xe hiện tại (xe đang Active) của hợp đồng — dùng cho hiển thị nhanh.</summary>
    public Task<ContractVehicleBillingSummaryDto> CalculateContractFeeAsync(
        Guid contractId,
        CancellationToken ct = default) =>
        ComputeAsync(contractId, periodStart: null, periodEnd: null, ct);

    public async Task<InvoiceItemInputDto?> BuildInvoiceItemInputAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct = default)
    {
        var summary = await ComputeAsync(contractId, periodStart, periodEnd, ct);
        if (summary.ActiveVehicleCount == 0 || summary.TotalMonthlyFee <= 0m)
        {
            return null;
        }

        return new InvoiceItemInputDto
        {
            ItemType = InvoiceItemType.Service,
            Description = summary.InvoiceLineDescription,
            Unit = VehicleLocalization.T(localizer, "InvoiceUnitMonth", "tháng"),
            Quantity = 1m,
            UnitPrice = summary.TotalMonthlyFee,
        };
    }

    /// <summary>
    /// Cập nhật lũy đẳng dòng "Phí gửi xe" trên các hóa đơn đang ở trạng thái Draft của hợp đồng.
    /// Không chạm vào hóa đơn Issued hoặc Paid.
    /// </summary>
    public async Task<int> SyncDraftInvoicesForContractAsync(
        Guid contractId,
        CancellationToken ct = default)
    {
        var draftInvoices = await db.Invoices
            .Include(i => i.Items)
            .Where(i => i.ContractId == contractId && !i.IsDeleted && i.Status == InvoiceStatus.Draft)
            .ToListAsync(ct);

        if (draftInvoices.Count == 0)
        {
            return 0;
        }

        var summaryByPeriod = new Dictionary<(DateOnly, DateOnly), ContractVehicleBillingSummaryDto>();
        var updatedCount = 0;

        foreach (var invoice in draftInvoices)
        {
            var key = (invoice.PeriodStart, invoice.PeriodEnd);
            if (!summaryByPeriod.TryGetValue(key, out var summary))
            {
                summary = await ComputeAsync(contractId, invoice.PeriodStart, invoice.PeriodEnd, ct);
                summaryByPeriod[key] = summary;
            }

            var existingLines = invoice.Items
                .Where(item => VehicleRules.IsParkingFeeLine(item.Description))
                .OrderBy(item => item.Id)
                .ToList();

            if (summary.ActiveVehicleCount == 0 || summary.TotalMonthlyFee <= 0m)
            {
                if (existingLines.Count == 0)
                {
                    continue;
                }

                var nonParkingCount = invoice.Items.Count - existingLines.Count;
                if (nonParkingCount == 0)
                {
                    // Hóa đơn chỉ còn dòng phí gửi xe: đưa dòng duy nhất về 0 thay vì giữ số tiền cũ.
                    var primary = existingLines[0];
                    primary.UnitPrice = 0m;
                    primary.Amount = 0m;

                    foreach (var extra in existingLines.Skip(1))
                    {
                        invoice.Items.Remove(extra);
                        db.InvoiceItems.Remove(extra);
                    }
                }
                else
                {
                    foreach (var line in existingLines)
                    {
                        invoice.Items.Remove(line);
                        db.InvoiceItems.Remove(line);
                    }
                }
            }
            else
            {
                var primary = existingLines.FirstOrDefault();
                if (primary is null)
                {
                    var nextOrder = invoice.Items.Count == 0
                        ? 1
                        : invoice.Items.Max(i => i.DisplayOrder) + 1;

                    primary = new InvoiceItem
                    {
                        InvoiceId = invoice.Id,
                        ItemType = InvoiceItemType.Service,
                        Description = summary.InvoiceLineDescription,
                        Unit = VehicleLocalization.T(localizer, "InvoiceUnitMonth", "tháng"),
                        Quantity = 1m,
                        UnitPrice = summary.TotalMonthlyFee,
                        Amount = summary.TotalMonthlyFee,
                        DisplayOrder = nextOrder,
                    };
                    invoice.Items.Add(primary);
                }
                else
                {
                    primary.ItemType = InvoiceItemType.Service;
                    primary.Description = summary.InvoiceLineDescription;
                    primary.Unit = VehicleLocalization.T(localizer, "InvoiceUnitMonth", "tháng");
                    primary.Quantity = 1m;
                    primary.UnitPrice = summary.TotalMonthlyFee;
                    primary.Amount = summary.TotalMonthlyFee;

                    // Xóa các dòng "Phí gửi xe" trùng nếu có
                    foreach (var extra in existingLines.Skip(1))
                    {
                        invoice.Items.Remove(extra);
                        db.InvoiceItems.Remove(extra);
                    }
                }
            }

            invoice.TotalAmount = InvoiceCalculationRules.TotalOf(invoice.Items.Select(item => new InvoiceItemDto
            {
                ItemType = item.ItemType,
                Description = item.Description,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Amount,
            }));
            invoice.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            updatedCount++;
        }

        if (updatedCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return updatedCount;
    }

    public async Task<int> SyncPropertyDraftInvoicesAsync(
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!await VehicleScope.CanManageVehiclesAsync(db, propertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var contractIds = await (from invoice in db.Invoices.AsNoTracking()
                                 join contract in db.Contracts.AsNoTracking() on invoice.ContractId equals contract.ContractId
                                 join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
                                 where room.PropertyId == propertyId && !invoice.IsDeleted && invoice.Status == InvoiceStatus.Draft
                                 select contract.ContractId)
            .Distinct()
            .ToListAsync(ct);

        var totalSynced = 0;
        foreach (var contractId in contractIds)
        {
            totalSynced += await SyncDraftInvoicesForContractAsync(contractId, ct);
        }

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.BillingDraftsSynced",
            caller.UserId,
            propertyId,
            $"SyncedDraftInvoices={totalSynced}",
            clock.GetUtcNow(),
            ct);

        return totalSynced;
    }

    /// <summary>
    /// Tính phí gửi xe của hợp đồng. Khi có kỳ (periodStart/periodEnd), chỉ đếm xe đã gửi trong kỳ đó
    /// (StartedAt trước hết kỳ và EndedAt rỗng hoặc sau/đúng đầu kỳ). Khi không có kỳ, đếm xe đang Active hiện tại.
    /// Luôn lọc theo ContractId để không cộng trùng khi một phòng có nhiều hợp đồng.
    /// </summary>
    private async Task<ContractVehicleBillingSummaryDto> ComputeAsync(
        Guid contractId,
        DateOnly? periodStart,
        DateOnly? periodEnd,
        CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking()
            .Where(c => c.ContractId == contractId)
            .Select(c => new { c.ContractId, c.RoomId })
            .FirstOrDefaultAsync(ct);

        if (contract is null)
        {
            return EmptySummary(contractId, 0);
        }

        var propertyId = await db.Rooms.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.Id == contract.RoomId)
            .Select(r => r.PropertyId)
            .FirstOrDefaultAsync(ct);

        var setting = await db.VehiclePropertySettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PropertyId == propertyId, ct)
            ?? new VehiclePropertySetting { PropertyId = propertyId };

        List<Vehicle> vehicles;
        if (periodStart.HasValue && periodEnd.HasValue)
        {
            var (rangeStart, rangeEndExclusive) = PeriodRangeUtc(periodStart.Value, periodEnd.Value);
            vehicles = await db.Vehicles.AsNoTracking()
                .Where(v => v.ContractId == contractId
                    && v.StartedAt < rangeEndExclusive
                    && (v.EndedAt == null || v.EndedAt >= rangeStart))
                .ToListAsync(ct);
        }
        else
        {
            vehicles = await db.Vehicles.AsNoTracking()
                .Where(v => v.ContractId == contractId && v.Status == VehicleStatus.Active)
                .ToListAsync(ct);
        }

        if (vehicles.Count == 0)
        {
            return EmptySummary(contract.ContractId, contract.RoomId);
        }

        var zoneIds = vehicles
            .Where(v => v.ParkingZoneId.HasValue)
            .Select(v => v.ParkingZoneId!.Value)
            .Distinct()
            .ToArray();

        var zonesById = zoneIds.Length == 0
            ? new Dictionary<int, ParkingZone>()
            : await db.ParkingZones.AsNoTracking()
                .Where(z => zoneIds.Contains(z.Id))
                .ToDictionaryAsync(z => z.Id, ct);

        var totalFee = 0m;
        var electricCount = 0;
        var manualCount = 0;
        var scooterCount = 0;
        var billedVehicles = new List<(VehicleType Type, decimal Fee)>(vehicles.Count);

        foreach (var vehicle in vehicles)
        {
            var zone = vehicle.ParkingZoneId.HasValue
                ? zonesById.GetValueOrDefault(vehicle.ParkingZoneId.Value)
                : null;
            var fee = VehicleRules.CalculateMonthlyFee(vehicle.VehicleType, setting, zone);
            totalFee += fee;
            billedVehicles.Add((vehicle.VehicleType, fee));

            switch (vehicle.VehicleType)
            {
                case VehicleType.ElectricMotorbike:
                    electricCount++;
                    break;
                case VehicleType.ManualMotorbike:
                    manualCount++;
                    break;
                case VehicleType.Scooter:
                    scooterCount++;
                    break;
            }
        }

        var linePrefix = VehicleLocalization.T(localizer, "InvoiceParkingLinePrefix", VehicleRules.ParkingInvoiceLinePrefix);
        var description = VehicleRules.BuildInvoiceLineDescription(
            billedVehicles,
            localizer is null ? null : type => VehicleLocalization.T(localizer, type.ToString(), type.ToString()),
            linePrefix);
        return new ContractVehicleBillingSummaryDto(
            contract.ContractId,
            contract.RoomId,
            vehicles.Count,
            electricCount,
            manualCount,
            scooterCount,
            decimal.Round(totalFee, 2),
            description);
    }

    private static (DateTimeOffset Start, DateTimeOffset EndExclusive) PeriodRangeUtc(DateOnly periodStart, DateOnly periodEnd)
    {
        var (startUtc, _) = VietnamTime.DayRangeUtc(periodStart);
        var (_, endExclusiveUtc) = VietnamTime.DayRangeUtc(periodEnd);
        return (
            new DateTimeOffset(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(endExclusiveUtc, DateTimeKind.Utc)));
    }

    private static ContractVehicleBillingSummaryDto EmptySummary(Guid contractId, int roomId) =>
        new(
            contractId,
            roomId,
            0,
            0,
            0,
            0,
            0m,
            VehicleRules.ParkingInvoiceLinePrefix);
}
