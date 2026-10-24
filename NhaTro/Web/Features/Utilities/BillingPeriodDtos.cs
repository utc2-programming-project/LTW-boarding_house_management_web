// VHT-9 — DTO mẫu P5 (Kỳ, chỉ số, chi phí)
namespace Web.Features.Utilities
{
    /// <summary>Mở / Đã chốt / Đã phát hành hóa đơn.</summary>
    public enum BillingPeriodStatus { Open, Closed, Invoiced }

    public enum CostLineType { Rent, Electric, Water, Service }

    public record CostLineDto(
        CostLineType Type,
        string Label,
        decimal Quantity,
        decimal UnitPrice,
        decimal Amount,
        string? ServiceCode = null);

    /// <summary>Lịch sử sửa chỉ số kỳ liền trước (D10): ai, khi nào, cũ → mới, lý do.</summary>
    public record IndexCorrectionDto(
        int ByUserId,
        DateTime At,
        string Field,
        decimal OldValue,
        decimal NewValue,
        string Reason);

    public record BillingPeriodDto(
        int Id,
        int ContractId,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        BillingPeriodStatus Status,
        decimal ElectricStart,
        decimal ElectricEnd,
        decimal WaterStart,
        decimal WaterEnd,
        int TariffVersionId,
        IReadOnlyList<CostLineDto> CostLines,
        decimal Total,
        int? InvoiceId,
        IReadOnlyList<IndexCorrectionDto> CorrectionHistory);
}

namespace BoardingHouse.Web.Features.Utilities
{
    public enum BillingPeriodStatus { Open, Closed, Invoiced }
    public enum CostLineType { Rent, Electric, Water, Service }
    public record CostLineDto(CostLineType Type, string Label, decimal Quantity, decimal UnitPrice, decimal Amount);
    public record IndexCorrectionDto(int ByUserId, DateTime At, string Field, decimal OldValue, decimal NewValue, string Reason);
    public record BillingPeriodDto(int Id, int ContractId, DateOnly PeriodStart, DateOnly PeriodEnd, BillingPeriodStatus Status, decimal ElectricStart, decimal ElectricEnd, decimal WaterStart, decimal WaterEnd, int TariffVersionId, IReadOnlyList<CostLineDto> CostLines, decimal Total, int? InvoiceId, IReadOnlyList<IndexCorrectionDto> CorrectionHistory);
}
