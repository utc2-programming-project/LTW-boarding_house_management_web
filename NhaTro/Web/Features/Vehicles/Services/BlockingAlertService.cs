using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Vehicles.Services;

public interface IBlockingAlertService
{
    Task<BlockingAlertResult> SendBlockingAlertAsync(BlockingAlertRequest request, PropertyCaller caller, CancellationToken ct = default);
}

/// <summary>
/// V3 &amp; D-VH-09: Báo đậu chắn lối ẩn danh trong cùng nhà trọ.
/// Không bao giờ trả về tên đầy đủ hay số điện thoại của chủ xe cho người báo.
/// Giới hạn tần suất tối đa 1 lần / 5 phút cho cùng một biển số.
/// </summary>
public sealed class BlockingAlertService(
    ApplicationDbContext db,
    INotificationService notifications,
    IMemoryCache cache,
    AuditLogService audit,
    TimeProvider clock,
    IStringLocalizer<VehicleResource>? localizer = null) : IBlockingAlertService
{
    public async Task<BlockingAlertResult> SendBlockingAlertAsync(
        BlockingAlertRequest request,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        var canAccess = caller.IsKhachThue
            ? (await VehicleScope.GetTenantPropertyIdsAsync(db, caller, clock.TodayVietnam(), ct)).Contains(request.PropertyId)
            : await VehicleScope.CanAccessPropertyAsync(db, request.PropertyId, caller, ct);

        if (!canAccess)
        {
            return BlockingAlertResult.Fail(VehicleErrorCodes.Forbidden);
        }

        var setting = await VehicleSettingService.EnsureEntityAsync(db, request.PropertyId, clock.GetUtcNow(), ct);
        if (caller.IsKhachThue && !setting.AllowBlockingAlert)
        {
            return BlockingAlertResult.Fail(VehicleErrorCodes.BlockingAlertDisabled);
        }

        var rawQuery = request.PlateQuery?.Trim() ?? string.Empty;
        var normalizedQuery = VehicleRules.NormalizePlateNumber(rawQuery);
        if (normalizedQuery.Length < 4 && !rawQuery.StartsWith("VH-", StringComparison.OrdinalIgnoreCase))
        {
            return BlockingAlertResult.Fail(VehicleErrorCodes.InvalidPlateNumber);
        }

        // Mục 55: lọc exact/4 số cuối ngay trên SQL, không nạp toàn bộ xe Active của nhà.
        var vehicleQuery = from v in db.Vehicles.AsNoTracking()
                           join r in db.Rooms.IgnoreQueryFilters().AsNoTracking() on v.RoomId equals r.Id
                           where v.PropertyId == request.PropertyId && v.Status == VehicleStatus.Active
                           select new
                           {
                               v.Id,
                               v.PropertyId,
                               v.RoomId,
                               RoomCode = r.Code,
                               v.ContractId,
                               v.TenantId,
                               v.PlateNumber,
                               v.QrCodeToken,
                           };

        var normalizedLower = normalizedQuery.ToLowerInvariant();
        var rawLower = rawQuery.ToLowerInvariant();

        // Chỉ cần tối đa 2 dòng để phân biệt "không có" và "trùng biển số".
        var exactMatches = await vehicleQuery
            .Where(v => v.PlateNumber.ToLower() == normalizedLower || v.QrCodeToken.ToLower() == rawLower)
            .Take(2)
            .ToListAsync(ct);

        var matches = exactMatches.Count > 0
            ? exactMatches
            : await vehicleQuery
                .Where(v => v.PlateNumber.ToLower().EndsWith(normalizedLower))
                .Take(2)
                .ToListAsync(ct);

        if (matches.Count == 0)
        {
            return BlockingAlertResult.Fail(VehicleErrorCodes.VehicleNotFound);
        }

        if (matches.Count > 1)
        {
            return BlockingAlertResult.Fail(VehicleErrorCodes.BlockingAlertAmbiguousPlate);
        }

        var target = matches[0];
        var now = clock.GetUtcNow();
        var cacheKey = $"vehicle-blocking-alert:{target.PropertyId}:{target.PlateNumber}";

        if (cache.TryGetValue<DateTimeOffset>(cacheKey, out var lastSentAt)
            && now - lastSentAt < VehicleRules.BlockingAlertCooldown)
        {
            return BlockingAlertResult.Fail(VehicleErrorCodes.BlockingAlertRateLimited);
        }

        cache.Set(cacheKey, now, VehicleRules.BlockingAlertCooldown);

        var recipientUserIds = new HashSet<string>(StringComparer.Ordinal);

        if (target.TenantId.HasValue)
        {
            var vehicleTenantUserId = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.Id == target.TenantId.Value && t.UserId != null)
                .Select(t => t.UserId!)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(vehicleTenantUserId))
            {
                recipientUserIds.Add(vehicleTenantUserId);
            }
        }

        var roomUserIds = await (from m in db.ContractMembers.AsNoTracking()
                                 join t in db.Tenants.IgnoreQueryFilters().AsNoTracking() on m.TenantId equals t.Id
                                 where m.ContractId == target.ContractId && t.UserId != null
                                 select t.UserId!)
            .Distinct()
            .ToListAsync(ct);

        foreach (var uid in roomUserIds)
        {
            recipientUserIds.Add(uid);
        }

        var displayPlate = VehicleRules.FormatPlateForDisplay(target.PlateNumber);
        var cleanNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var title = VehicleLocalization.T(localizer, "BlockingAlertNotifTitle",
            "Xe {0} đang đậu chắn lối", displayPlate);
        var body = cleanNote is null
            ? VehicleLocalization.T(localizer, "BlockingAlertNotifBody",
                "Xe {0} (Phòng {1}) đang đậu chắn lối, vui lòng ra dắt xe giúp.", displayPlate, target.RoomCode)
            : VehicleLocalization.T(localizer, "BlockingAlertNotifBodyWithNote",
                "Xe {0} (Phòng {1}) đang đậu chắn lối: \"{2}\". Vui lòng ra dắt xe giúp.",
                displayPlate, target.RoomCode, cleanNote);

        foreach (var userId in recipientUserIds)
        {
            await notifications.NotifyAsync(userId, title, body, "/my/vehicles");
        }

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.BlockingAlertSent",
            caller.UserId,
            target.PropertyId,
            $"Plate={target.PlateNumber};Room={target.RoomCode};Recipients={recipientUserIds.Count}",
            now,
            ct);

        // Chỉ trả về biển số và mã phòng, tuyệt đối không trả về tên khách thuê hay số điện thoại
        return BlockingAlertResult.Success(target.PlateNumber, target.RoomCode, recipientUserIds.Count);
    }
}
