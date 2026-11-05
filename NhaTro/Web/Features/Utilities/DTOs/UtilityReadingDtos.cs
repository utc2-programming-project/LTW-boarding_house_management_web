namespace Web.Features.Utilities.DTOs;

public sealed record UtilityReadingDto(
    int Id,
    int RoomId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal ElectricStart,
    decimal ElectricEnd,
    decimal WaterStart,
    decimal WaterEnd,
    decimal ElectricityUsage,
    decimal WaterUsage,
    string RecordedByUserId,
    DateTimeOffset CreatedAt);

public sealed record UtilityReadingHistoryDto(
    int Id,
    int UtilityReadingId,
    decimal OldElectricEnd,
    decimal NewElectricEnd,
    decimal OldWaterEnd,
    decimal NewWaterEnd,
    string Reason,
    string ChangedByUserId,
    DateTimeOffset ChangedAt);

public sealed record RecordUtilityReadingRequest(
    int RoomId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal ElectricEnd,
    decimal WaterEnd);

public sealed record AdjustUtilityReadingRequest(
    decimal ElectricEnd,
    decimal WaterEnd,
    string Reason);

/// <summary>Payload lưu trong hàng đợi SW-09 khi Chủ trọ duyệt sửa chỉ số kỳ trước.</summary>
public sealed record UtilityAdjustmentApprovalPayload(
    int ReadingId,
    decimal ElectricEnd,
    decimal WaterEnd,
    string Reason);
