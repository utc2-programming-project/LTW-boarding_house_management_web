namespace Web.Features.Utilities.Rules;

public sealed record UtilityReadingIssue(string Field, UtilityReadingErrorCode Error);

/// <summary>
/// Quy tắc thuần cho kỳ ghi chỉ số (VHT-51): kiểm kỳ và kiểm chỉ số đầu/cuối.
/// Không truy cập DB để unit test chạy nhanh.
/// </summary>
public static class UtilityReadingRules
{
    /// <summary>Lượng tiêu thụ = cuối kỳ − đầu kỳ; không bao giờ âm sau khi đã validate.</summary>
    public static decimal CalculateUsage(decimal start, decimal end) =>
        end <= start ? 0m : end - start;

    public static IReadOnlyList<UtilityReadingIssue> ValidatePeriod(DateOnly periodStart, DateOnly periodEnd)
    {
        if (periodEnd < periodStart)
        {
            return [new(nameof(periodEnd), UtilityReadingErrorCode.InvalidPeriod)];
        }

        return [];
    }

    public static IReadOnlyList<UtilityReadingIssue> ValidateReadings(
        decimal electricStart,
        decimal electricEnd,
        decimal waterStart,
        decimal waterEnd)
    {
        var issues = new List<UtilityReadingIssue>(2);

        if (electricEnd < 0m)
        {
            issues.Add(new(nameof(electricEnd), UtilityReadingErrorCode.ReadingCannotBeNegative));
        }
        else if (electricEnd < electricStart)
        {
            issues.Add(new(nameof(electricEnd), UtilityReadingErrorCode.ReadingLessThanPrevious));
        }

        if (waterEnd < 0m)
        {
            issues.Add(new(nameof(waterEnd), UtilityReadingErrorCode.ReadingCannotBeNegative));
        }
        else if (waterEnd < waterStart)
        {
            issues.Add(new(nameof(waterEnd), UtilityReadingErrorCode.ReadingLessThanPrevious));
        }

        return issues;
    }
}
