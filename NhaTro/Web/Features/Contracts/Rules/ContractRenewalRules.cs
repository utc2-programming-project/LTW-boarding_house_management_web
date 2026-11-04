namespace Web.Features.Contracts.Rules;

/// <summary>
/// Ngưỡng hợp lệ của một yêu cầu gia hạn (VHT-125/2D). Cùng nguồn cho tầng service và
/// check constraint của DB để client không tự đặt ra con số khác server.
/// </summary>
public static class ContractRenewalRules
{
    public const int MinRequestedMonths = 1;

    /// <summary>10 năm: trên mức này là ký hợp đồng mới, không phải gia hạn.</summary>
    public const int MaxRequestedMonths = 120;

    public const int MaxNoteLength = 500;

    public static bool IsMonthsInRange(int requestedMonths) =>
        requestedMonths is >= MinRequestedMonths and <= MaxRequestedMonths;
}
