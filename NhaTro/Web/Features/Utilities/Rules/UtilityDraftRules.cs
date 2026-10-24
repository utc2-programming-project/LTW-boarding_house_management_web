using System.Globalization;
using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.Rules;

public enum UtilityValidationError
{
    PriceMustBePositive,
    NameIsRequired,
    EffectiveToMustBeAfterOrOnEffectiveFrom,
    MeterReadingMustBeGreaterOrEqualStart,
    MeterReadingCannotBeNegative,
    AdjustmentReasonIsRequired,
}

public sealed record UtilityValidationIssue(string Field, UtilityValidationError Error, string Message);

public static class UtilityDraftRules
{
    private static readonly NumberFormatInfo VndNumberFormat = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3],
    };

    public static readonly IReadOnlyList<ElectricTierDto> DefaultElectricTiers =
    [
        new(1, "Bậc 1 (0-50 kWh)", 50m, 1_800m),
        new(2, "Bậc 2 (51-100 kWh)", 50m, 2_000m),
        new(3, "Bậc 3 (>100 kWh)", null, 3_000m),
    ];

    public static ElectricityCalculationResult CalculateElectricityTiered(
        decimal usageKwh,
        IReadOnlyList<ElectricTierDto>? tiers = null)
    {
        tiers ??= DefaultElectricTiers;
        var safeUsage = Math.Max(0m, usageKwh);

        if (safeUsage == 0m)
        {
            return new ElectricityCalculationResult(0m, "0 kWh × 0 ₫", []);
        }

        var remaining = safeUsage;
        var total = 0m;
        var applied = new List<TierAppliedDto>();
        var formulaParts = new List<string>();

        foreach (var tier in tiers)
        {
            if (remaining <= 0m) break;

            var unitsInTier = tier.MaxKwh is null ? remaining : Math.Min(remaining, tier.MaxKwh.Value);
            var cost = unitsInTier * tier.Rate;
            total += cost;
            remaining -= unitsInTier;

            applied.Add(new TierAppliedDto(tier.Name, unitsInTier, tier.Rate, cost));
            formulaParts.Add($"{unitsInTier.ToString("N0", VndNumberFormat)}×{tier.Rate.ToString("N0", VndNumberFormat)}");
        }

        var breakdown = $"Bậc thang {applied.Count} bậc: {string.Join(" + ", formulaParts)}";
        return new ElectricityCalculationResult(total, breakdown, applied);
    }

    public static WaterCalculationResult CalculateWaterCost(decimal usageM3, decimal unitRate = 15_000m)
    {
        var safeUsage = Math.Max(0m, usageM3);
        var total = safeUsage * unitRate;
        var breakdown = $"Đơn giá cố định: {safeUsage.ToString("N0", VndNumberFormat)} m³ × {unitRate.ToString("N0", VndNumberFormat)} ₫";
        return new WaterCalculationResult(total, breakdown);
    }

    public static TotalMonthlyBillResult CalculateTotalMonthlyBill(
        decimal roomRent,
        decimal electricityUsageKwh,
        decimal waterUsageM3,
        decimal waterRate = 15_000m,
        decimal internetCost = 0m,
        decimal cleaningCost = 0m,
        decimal parkingCost = 0m)
    {
        var elecCalc = CalculateElectricityTiered(electricityUsageKwh);
        var waterCalc = CalculateWaterCost(waterUsageM3, waterRate);
        var serviceCost = internetCost + cleaningCost + parkingCost;
        var totalCost = roomRent + elecCalc.Total + waterCalc.Total + serviceCost;

        return new TotalMonthlyBillResult(
            roomRent,
            elecCalc.Total,
            waterCalc.Total,
            serviceCost,
            totalCost);
    }

    public static List<UtilityValidationIssue> ValidateCreateService(CreateServiceRequest request)
    {
        var issues = new List<UtilityValidationIssue>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            issues.Add(new UtilityValidationIssue(
                nameof(request.Name),
                UtilityValidationError.NameIsRequired,
                "Tên dịch vụ không được để trống / Service name is required."));
        }

        if (request.Rate <= 0m)
        {
            issues.Add(new UtilityValidationIssue(
                nameof(request.Rate),
                UtilityValidationError.PriceMustBePositive,
                "Đơn giá phải là số dương lớn hơn 0 / Price must be greater than 0."));
        }

        return issues;
    }

    public static List<UtilityValidationIssue> ValidateUpdatePrice(UpdatePriceRequest request)
    {
        var issues = new List<UtilityValidationIssue>();

        if (request.NewRate <= 0m)
        {
            issues.Add(new UtilityValidationIssue(
                nameof(request.NewRate),
                UtilityValidationError.PriceMustBePositive,
                "Đơn giá phải là số dương lớn hơn 0 / Price must be greater than 0."));
        }

        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom)
        {
            issues.Add(new UtilityValidationIssue(
                nameof(request.EffectiveTo),
                UtilityValidationError.EffectiveToMustBeAfterOrOnEffectiveFrom,
                "Ngày hết hiệu lực phải sau hoặc bằng ngày bắt đầu / Effective end date must be on or after start date."));
        }

        return issues;
    }

    public static List<UtilityValidationIssue> ValidateMeterReading(
        decimal startReading,
        decimal endReading,
        bool hasChangedFromInitial = false,
        string? adjustmentReason = null)
    {
        var issues = new List<UtilityValidationIssue>();

        if (endReading < 0m)
        {
            issues.Add(new UtilityValidationIssue(
                "EndReading",
                UtilityValidationError.MeterReadingCannotBeNegative,
                "Chỉ số cuối kỳ không thể là số âm / End reading cannot be negative."));
        }

        if (endReading < startReading)
        {
            issues.Add(new UtilityValidationIssue(
                "EndReading",
                UtilityValidationError.MeterReadingMustBeGreaterOrEqualStart,
                $"Chỉ số cuối kỳ ({endReading.ToString("N0", VndNumberFormat)}) phải lớn hơn hoặc bằng chỉ số đầu kỳ ({startReading.ToString("N0", VndNumberFormat)}) / End reading must be greater than or equal to start reading."));
        }

        if (hasChangedFromInitial && string.IsNullOrWhiteSpace(adjustmentReason))
        {
            issues.Add(new UtilityValidationIssue(
                "AdjustmentReason",
                UtilityValidationError.AdjustmentReasonIsRequired,
                "Bắt buộc nhập lý do điều chỉnh chỉ số / Adjustment reason is required."));
        }

        return issues;
    }

    public static bool CanManageServicePrices(string? role) =>
        string.Equals(role, "ChuTro", StringComparison.OrdinalIgnoreCase);

    public static bool CanRecordMeterReadings(string? role) =>
        string.Equals(role, "ChuTro", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "QuanLy", StringComparison.OrdinalIgnoreCase);

    public static bool IsTariffReadOnly(string serviceCode) =>
        string.Equals(serviceCode, "ELEC", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(serviceCode, "WATER", StringComparison.OrdinalIgnoreCase);
}
