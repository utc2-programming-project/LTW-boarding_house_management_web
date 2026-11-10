namespace Web.Features.Utilities.Enums;

/// <summary>
/// Trạng thái kỳ ghi chỉ số P5 (VHT-51).
/// <see cref="Open"/>: chưa phát hành hóa đơn (VHT-51 chỉ ghi chỉ số lần đầu);
/// <see cref="Invoiced"/>: đã phát hành hóa đơn, D10 khóa vĩnh viễn chỉ số/chi phí của kỳ.
/// </summary>
public enum UtilityReadingPeriodStatus
{
    Open = 0,
    Invoiced = 1,
}
