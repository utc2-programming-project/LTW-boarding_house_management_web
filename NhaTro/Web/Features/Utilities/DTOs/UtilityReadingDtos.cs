using Web.Features.Utilities.Enums;

namespace Web.Features.Utilities.DTOs;

/// <summary>
/// Ảnh chụp kỳ ghi chỉ số thật (VHT-51): chỉ số đầu/cuối, lượng tiêu thụ, chi phí snapshot
/// điện/nước và trạng thái khóa hóa đơn. Chi phí dịch vụ lấy từ đơn giá đang hiệu lực của tòa.
/// </summary>
public sealed record UtilityReadingPeriodDto(
    int Id,
    Guid ContractId,
    int RoomId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal ElectricStart,
    decimal ElectricEnd,
    decimal WaterStart,
    decimal WaterEnd,
    decimal ElectricUsage,
    decimal WaterUsage,
    int ResidentCount,
    UtilityReadingPeriodStatus Status,
    int? InvoiceId,
    bool HasReadings,
    decimal RentAmount,
    decimal ElectricCost,
    decimal WaterCost,
    decimal ServiceCost,
    decimal TotalAmount,
    int? ElectricSnapshotId,
    int? WaterSnapshotId);
