using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using Web.Common.Utils;
using Web.Features.Accounts;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.MockData;
using Web.Features.Utilities.Rules;

namespace Web.Features.Utilities.Services;

public static class MeterReadingErrorCodes
{
    public const string NegativeReading = nameof(NegativeReading);
    public const string RoomNotFound = nameof(RoomNotFound);
    public const string ElectricEndLessThanStart = nameof(ElectricEndLessThanStart);
    public const string WaterEndLessThanStart = nameof(WaterEndLessThanStart);
    public const string AdjustmentReasonRequired = nameof(AdjustmentReasonRequired);
    public const string InvalidRequest = nameof(InvalidRequest);
    public const string SaveFailed = nameof(SaveFailed);
}

public sealed record SaveResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    RoomReadingSampleDto? Room = null);

public sealed record UtilityCostBreakdown(
    decimal RoomRent,
    decimal ElectricityUsage,
    decimal ElectricityCost,
    IReadOnlyList<TierAppliedDto> ElectricTiers,
    decimal WaterUsage,
    decimal WaterCost,
    IReadOnlyList<TierAppliedDto> WaterTiers,
    decimal InternetCost,
    decimal CleaningCost,
    decimal ParkingCost,
    decimal ServiceCost,
    decimal TotalCost,
    bool HasAbnormalElectricUsage = false,
    bool HasAbnormalWaterUsage = false);

public sealed class MeterReadingPageService(
    AuthenticationStateProvider authState,
    UtilitySampleStateService sampleState,
    IUtilityTariffService tariffService,
    IServiceScopeFactory scopeFactory,
    ILogger<MeterReadingPageService> logger,
    IStringLocalizer<Shared> localizer)
{
    // EVN 6-tier household rates (Decision 1279 / Seed Tariff Id = 1)
    public static readonly IReadOnlyList<ElectricTierDto> EvnElectricTiers =
    [
        new(1, 50m, 1_984m),
        new(2, 50m, 2_050m),
        new(3, 100m, 2_380m),
        new(4, 100m, 2_998m),
        new(5, 100m, 3_350m),
        new(6, null, 3_460m),
    ];

    // HCM Water tiered rates (Decision 04 / Seed Tariff Id = 2)
    public static readonly IReadOnlyList<ElectricTierDto> WaterTiersSeed =
    [
        new(1, 4m, 6_700m),
        new(2, 2m, 12_900m),
        new(3, null, 14_400m),
    ];

    public async Task<string> GetCurrentRoleAsync()
    {
        var auth = await authState.GetAuthenticationStateAsync();
        var user = auth.User;
        var role = Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;

        if (string.Equals(role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Admin bị chặn truy cập nghiệp vụ dịch vụ/chỉ số (BusinessGuard).");
        }

        return role;
    }

    public async Task<string> GetCurrentEmailAsync()
    {
        var auth = await authState.GetAuthenticationStateAsync();
        var user = auth.User;
        return user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? string.Empty;
    }

    public async Task<IReadOnlyList<BuildingOptionDto>> GetBuildingsAsync()
    {
        var role = await GetCurrentRoleAsync();
        var email = await GetCurrentEmailAsync();
        return sampleState.GetBuildingsForCaller(role, email);
    }

    public async Task<IReadOnlyList<RoomReadingSampleDto>> GetRoomReadingsAsync()
    {
        var role = await GetCurrentRoleAsync();
        var email = await GetCurrentEmailAsync();
        return sampleState.GetRoomReadingsForCaller(role, email);
    }

    public async Task<SaveResult> SaveReadingAsync(
        string roomId,
        decimal electricEnd,
        decimal waterEnd,
        DateOnly periodStart,
        DateOnly periodEnd,
        string? reason,
        bool isLock = false,
        CancellationToken ct = default)
    {
        var role = await GetCurrentRoleAsync();
        var email = await GetCurrentEmailAsync();

        if (string.Equals(role, Roles.KhachThue, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Khách thuê {Email} cố gắng ghi chỉ số cho phòng {RoomId}", email, roomId);
            throw new UnauthorizedAccessException("Khách thuê chỉ có quyền xem, không được ghi chỉ số.");
        }

        if (electricEnd < 0m || waterEnd < 0m)
        {
            return new SaveResult(false, MeterReadingErrorCodes.NegativeReading, "Chỉ số không được là số âm.");
        }

        var rooms = sampleState.GetRoomReadingsForCaller(role, email);
        var currentRoom = rooms.FirstOrDefault(r => r.Id == roomId);
        if (currentRoom is null)
        {
            return new SaveResult(false, MeterReadingErrorCodes.RoomNotFound, "Không tìm thấy phòng trong phạm vi được phép.");
        }

        if (electricEnd < currentRoom.ElectricStart)
        {
            return new SaveResult(
                false,
                MeterReadingErrorCodes.ElectricEndLessThanStart,
                $"Chỉ số điện mới ({electricEnd}) phải lớn hơn hoặc bằng chỉ số cũ ({currentRoom.ElectricStart}).");
        }

        if (waterEnd < currentRoom.WaterStart)
        {
            return new SaveResult(
                false,
                MeterReadingErrorCodes.WaterEndLessThanStart,
                $"Chỉ số nước mới ({waterEnd}) phải lớn hơn hoặc bằng chỉ số cũ ({currentRoom.WaterStart}).");
        }

        // Khi không xác định được email người thực hiện (vd. hệ thống/giả lập quản lý),
        // lấy tên người dùng từ localizer theo ngôn ngữ giao diện, không hard-code tiếng Việt.
        var modifierName = string.IsNullOrWhiteSpace(email) ? localizer["ManagerDisplayName"].Value : email;
        var (success, error, updatedRoom) = sampleState.SaveRoomReading(
            roomId, electricEnd, waterEnd, periodStart, periodEnd, reason, role, email, modifierName, isLock);

        if (!success || updatedRoom is null)
        {
            var code = error switch
            {
                UtilityErrorCode.AdjustmentReasonIsRequired => MeterReadingErrorCodes.AdjustmentReasonRequired,
                UtilityErrorCode.MeterReadingMustBeGreaterOrEqualStart => MeterReadingErrorCodes.InvalidRequest,
                UtilityErrorCode.MeterReadingCannotBeNegative => MeterReadingErrorCodes.NegativeReading,
                _ => MeterReadingErrorCodes.SaveFailed
            };
            var msg = error switch
            {
                UtilityErrorCode.AdjustmentReasonIsRequired => "Vui lòng nhập lý do điều chỉnh chỉ số khi sửa đổi.",
                UtilityErrorCode.MeterReadingMustBeGreaterOrEqualStart => "Chỉ số mới phải lớn hơn hoặc bằng chỉ số cũ.",
                UtilityErrorCode.MeterReadingCannotBeNegative => "Chỉ số không được âm.",
                _ => "Không thể lưu chỉ số phòng."
            };
            return new SaveResult(false, code, msg);
        }

        return new SaveResult(true, null, null, updatedRoom);
    }

    public async Task<UtilityCostBreakdown> CalculateCostAsync(
        RoomReadingSampleDto room,
        decimal electricEnd,
        decimal waterEnd,
        CancellationToken ct = default)
    {
        var electricUsage = Math.Max(0m, electricEnd - room.ElectricStart);
        var waterUsage = Math.Max(0m, waterEnd - room.WaterStart);

        // 1. Tính tiền điện (Ưu tiên IUtilityTariffService với scope ngắn hạn, fallback EVN 6 bậc)
        IReadOnlyList<TierAppliedDto> electricTiers = [];
        var electricCost = 0m;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var scopedTariff = scope.ServiceProvider.GetService<IUtilityTariffService>() ?? tariffService;
            var tariffDto = await scopedTariff.CalculateForPeriodAsync(
                TariffType.Electric,
                "VN-EVN-HOUSEHOLD",
                room.PeriodStart,
                room.PeriodEnd,
                electricUsage,
                residentCount: 1,
                cancellationToken: ct);

            if (tariffDto.Tiers.Count > 0)
            {
                electricCost = tariffDto.Total;
                electricTiers = tariffDto.Tiers.Select(t => new TierAppliedDto(
                    t.TierNumber,
                    t.Quantity,
                    t.Price,
                    t.Amount)).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "CalculateForPeriodAsync electric tariff service failed, using EVN fallback");
        }

        if (electricTiers.Count == 0 && electricUsage > 0m)
        {
            // Fallback EVN 6 bậc cho 2C — VHT-51 sẽ ghép service thật
            var evnResult = UtilityDraftRules.CalculateElectricityTiered(electricUsage, EvnElectricTiers);
            electricCost = evnResult.Total;
            electricTiers = evnResult.TiersApplied;
        }

        // 2. Tính tiền nước (Phương án A: BẬC THANG 4-2 m3 từ UtilityTariffSeedData)
        IReadOnlyList<TierAppliedDto> waterTiers = [];
        var waterCost = 0m;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var scopedTariff = scope.ServiceProvider.GetService<IUtilityTariffService>() ?? tariffService;
            var waterTariffDto = await scopedTariff.CalculateForPeriodAsync(
                TariffType.Water,
                "HCMC-BENTHANH",
                room.PeriodStart,
                room.PeriodEnd,
                waterUsage,
                residentCount: 1,
                cancellationToken: ct);

            if (waterTariffDto.Tiers.Count > 0)
            {
                waterCost = waterTariffDto.Total;
                waterTiers = waterTariffDto.Tiers.Select(t => new TierAppliedDto(
                    t.TierNumber,
                    t.Quantity,
                    t.Price,
                    t.Amount)).ToList();
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "CalculateForPeriodAsync water tariff service failed, using HCM water fallback");
        }

        if (waterTiers.Count == 0 && waterUsage > 0m)
        {
            // Fallback nước bậc thang HCM (Quy định tại UtilityTariffSeedData.cs)
            // Bậc 1: 0 - 4 m3 @ 6.700 đ
            // Bậc 2: 4 - 6 m3 @ 12.900 đ
            // Bậc 3: > 6 m3 @ 14.400 đ
            var applied = new List<TierAppliedDto>();
            var remaining = waterUsage;
            var totalWater = 0m;

            foreach (var tier in WaterTiersSeed)
            {
                if (remaining <= 0m) break;
                var units = tier.MaxKwh is null ? remaining : Math.Min(remaining, tier.MaxKwh.Value);
                var cost = units * tier.Rate;
                totalWater += cost;
                remaining -= units;
                applied.Add(new TierAppliedDto(tier.Id, units, tier.Rate, cost));
            }

            waterCost = totalWater;
            waterTiers = applied;
        }

        var serviceCost = room.InternetCost + room.CleaningCost + room.ParkingCost;
        var totalCost = room.RoomRent + electricCost + waterCost + serviceCost;

        var hasAbnormalElectric = room.PreviousElectricUsage is > 0m && electricUsage > 3m * room.PreviousElectricUsage.Value;
        var hasAbnormalWater = room.PreviousWaterUsage is > 0m && waterUsage > 3m * room.PreviousWaterUsage.Value;

        return new UtilityCostBreakdown(
            room.RoomRent,
            electricUsage,
            electricCost,
            electricTiers,
            waterUsage,
            waterCost,
            waterTiers,
            room.InternetCost,
            room.CleaningCost,
            room.ParkingCost,
            serviceCost,
            totalCost,
            hasAbnormalElectric,
            hasAbnormalWater);
    }
}
