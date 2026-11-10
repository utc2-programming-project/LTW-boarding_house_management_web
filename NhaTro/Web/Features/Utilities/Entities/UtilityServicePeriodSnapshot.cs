namespace Web.Features.Utilities.Entities;

/// <summary>
/// Dòng dịch vụ đã chốt của một kỳ ghi chỉ số. Không dùng FK tới danh mục/phiên bản giá để
/// lịch sử vẫn nguyên vẹn khi danh mục bị tắt hoặc chỉnh sửa.
/// </summary>
public sealed class UtilityServicePeriodSnapshot
{
    public int Id { get; set; }

    public int UtilityReadingPeriodId { get; set; }

    public int SourceServiceCatalogId { get; set; }

    public int SourceServicePriceHistoryId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }
}
