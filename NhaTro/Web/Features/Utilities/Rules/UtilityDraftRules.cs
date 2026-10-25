using System.Security.Claims;
using Web.Features.Accounts;
using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.Rules;

public enum UtilityErrorCode
{
    NameIsRequired,
    PriceMustBePositive,
    InvalidServiceUnit,
    EffectiveFromIsRequired,
    EffectiveToMustBeAfterEffectiveFrom,
    InvalidBuildingScope,
    MeterReadingMustBeGreaterOrEqualStart,
    MeterReadingCannotBeNegative,
    AdjustmentReasonIsRequired,
    BillingPeriodEndMustBeOnOrAfterStart,
    AccessDenied,
    ServiceNotFound,
    ReadOnlyTariff,
    PriceVersionConflict,
    RoomNotFound,
}

public sealed record UtilityValidationIssue(string Field, UtilityErrorCode Error);

public static class UtilityDraftRules
{
    public static readonly IReadOnlyList<ElectricTierDto> DefaultElectricTiers =
    [
        new(1, 50m, 1_800m),
        new(2, 50m, 2_000m),
        new(3, null, 3_000m),
    ];

    public static ElectricityCalculationResult CalculateElectricityTiered(
        decimal usageKwh,
        IReadOnlyList<ElectricTierDto>? tiers = null)
    {
        tiers ??= DefaultElectricTiers;
        var remaining = Math.Max(0m, usageKwh);
        var total = 0m;
        var applied = new List<TierAppliedDto>();

        foreach (var tier in tiers)
        {
            if (remaining <= 0m) break;

            var unitsInTier = tier.MaxKwh is null ? remaining : Math.Min(remaining, tier.MaxKwh.Value);
            var cost = unitsInTier * tier.Rate;
            total += cost;
            remaining -= unitsInTier;
            applied.Add(new TierAppliedDto(tier.Id, unitsInTier, tier.Rate, cost));
        }

        return new ElectricityCalculationResult(total, applied);
    }

    public static WaterCalculationResult CalculateWaterCost(decimal usageM3, decimal unitRate = 15_000m) =>
        new(Math.Max(0m, usageM3) * unitRate);

    public static TotalMonthlyBillResult CalculateTotalMonthlyBill(
        decimal roomRent,
        decimal electricityUsageKwh,
        decimal waterUsageM3,
        decimal waterRate = 15_000m,
        decimal internetCost = 0m,
        decimal cleaningCost = 0m,
        decimal parkingCost = 0m)
    {
        var electricity = CalculateElectricityTiered(electricityUsageKwh).Total;
        var water = CalculateWaterCost(waterUsageM3, waterRate).Total;
        var serviceCost = internetCost + cleaningCost + parkingCost;
        return new TotalMonthlyBillResult(
            roomRent,
            electricity,
            water,
            serviceCost,
            roomRent + electricity + water + serviceCost);
    }

    public static List<UtilityValidationIssue> ValidateCreateService(CreateServiceRequest request)
    {
        var issues = new List<UtilityValidationIssue>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            issues.Add(new(nameof(request.Name), UtilityErrorCode.NameIsRequired));
        }

        if (request.Rate <= 0m)
        {
            issues.Add(new(nameof(request.Rate), UtilityErrorCode.PriceMustBePositive));
        }

        if (request.EffectiveFrom == default)
        {
            issues.Add(new(nameof(request.EffectiveFrom), UtilityErrorCode.EffectiveFromIsRequired));
        }

        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value <= request.EffectiveFrom)
        {
            issues.Add(new(nameof(request.EffectiveTo), UtilityErrorCode.EffectiveToMustBeAfterEffectiveFrom));
        }

        if (!Enum.IsDefined(request.Unit))
        {
            issues.Add(new(nameof(request.Unit), UtilityErrorCode.InvalidServiceUnit));
        }

        return issues;
    }

    public static List<UtilityValidationIssue> ValidateUpdatePrice(UpdatePriceRequest request)
    {
        var issues = new List<UtilityValidationIssue>();
        if (request.NewRate <= 0m)
        {
            issues.Add(new(nameof(request.NewRate), UtilityErrorCode.PriceMustBePositive));
        }

        if (!Enum.IsDefined(request.Unit))
        {
            issues.Add(new(nameof(request.Unit), UtilityErrorCode.InvalidServiceUnit));
        }

        if (request.EffectiveFrom == default)
        {
            issues.Add(new(nameof(request.EffectiveFrom), UtilityErrorCode.EffectiveFromIsRequired));
        }

        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value <= request.EffectiveFrom)
        {
            issues.Add(new(nameof(request.EffectiveTo), UtilityErrorCode.EffectiveToMustBeAfterEffectiveFrom));
        }

        return issues;
    }

    public static List<UtilityValidationIssue> ValidateBillingPeriod(DateOnly start, DateOnly end) =>
        end < start
            ? [new(nameof(end), UtilityErrorCode.BillingPeriodEndMustBeOnOrAfterStart)]
            : [];

    public static List<UtilityValidationIssue> ValidateMeterReading(
        decimal startReading,
        decimal endReading,
        bool hasChangedFromInitial = false,
        string? adjustmentReason = null)
    {
        var issues = new List<UtilityValidationIssue>();
        if (endReading < 0m)
        {
            issues.Add(new("EndReading", UtilityErrorCode.MeterReadingCannotBeNegative));
        }

        if (endReading < startReading)
        {
            issues.Add(new("EndReading", UtilityErrorCode.MeterReadingMustBeGreaterOrEqualStart));
        }

        if (hasChangedFromInitial && string.IsNullOrWhiteSpace(adjustmentReason))
        {
            issues.Add(new("AdjustmentReason", UtilityErrorCode.AdjustmentReasonIsRequired));
        }

        return issues;
    }

    public static string ResolveRole(ClaimsPrincipal user)
    {
        // Match the restrictive role precedence used by the server caller providers.
        return new[] { Roles.Admin, Roles.KhachThue, Roles.QuanLy, Roles.ChuTro }
            .FirstOrDefault(user.IsInRole) ?? string.Empty;
    }

    public static string ResolveEmail(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? string.Empty;

    public static bool CanViewUtilityData(string? role) =>
        IsRole(role, Roles.ChuTro) || IsRole(role, Roles.QuanLy) || IsRole(role, Roles.KhachThue);

    public static bool CanManageServicePrices(string? role) => IsRole(role, Roles.ChuTro);

    public static bool CanRecordMeterReadings(string? role) =>
        IsRole(role, Roles.ChuTro) || IsRole(role, Roles.QuanLy);

    public static bool IsTariffReadOnly(string serviceCode) =>
        string.Equals(serviceCode, "ELEC", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(serviceCode, "WATER", StringComparison.OrdinalIgnoreCase);

    private static bool IsRole(string? value, string role) =>
        string.Equals(value, role, StringComparison.OrdinalIgnoreCase);
}
