using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.DTOs;

public sealed record VehiclePropertyOptionDto(
    int PropertyId,
    string Name,
    bool EnableParkingZones);

public sealed record VehicleRoomOptionDto(
    int RoomId,
    int PropertyId,
    string PropertyName,
    string RoomCode,
    Guid ContractId,
    string ContractNumber,
    int ActiveVehicleCount,
    int MaxVehiclesPerRoom,
    IReadOnlyList<VehicleTenantOptionDto> Tenants);

public sealed record VehicleTenantOptionDto(
    int TenantId,
    string FullName,
    string? UserId);

public sealed record VehicleDto(
    int Id,
    int PropertyId,
    string PropertyName,
    int RoomId,
    string RoomCode,
    Guid ContractId,
    string ContractNumber,
    int? TenantId,
    string? TenantName,
    VehicleType VehicleType,
    string PlateNumber,
    string? Brand,
    string? Color,
    string? PhotoRef,
    string? Note,
    string QrCodeToken,
    int? ParkingZoneId,
    string? ParkingZoneName,
    int? ParkingSlotId,
    string? ParkingSlotCode,
    VehicleStatus Status,
    decimal MonthlyFee,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? EndReason);

public sealed record VehicleSummaryDto(
    int TotalActiveCount,
    int TotalInactiveCount,
    int ElectricMotorbikeCount,
    int ManualMotorbikeCount,
    int ScooterCount,
    decimal EstimatedMonthlyRevenue,
    int ActiveGuestNoticeCount,
    int TotalZoneCapacity,
    int TotalZoneOccupied);

public sealed record VehicleListResultDto(
    IReadOnlyList<VehicleDto> Items,
    VehicleSummaryDto Summary);

public sealed record VehicleFilter
{
    public int? PropertyId { get; init; }
    public int? RoomId { get; init; }
    public Guid? ContractId { get; init; }
    public VehicleType? VehicleType { get; init; }
    public VehicleStatus? Status { get; init; }
    public int? ParkingZoneId { get; init; }
    public string? Keyword { get; init; }
}

public sealed record VehicleCreateRequest
{
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    public int? TenantId { get; set; }
    public VehicleType VehicleType { get; set; } = VehicleType.ManualMotorbike;
    public string PlateNumber { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Color { get; set; }
    public string? PhotoRef { get; set; }
    public string? Note { get; set; }
    public int? ParkingZoneId { get; set; }
    public int? ParkingSlotId { get; set; }
}

public sealed record VehicleUpdateRequest
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public VehicleType VehicleType { get; set; } = VehicleType.ManualMotorbike;
    public string PlateNumber { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Color { get; set; }
    public string? PhotoRef { get; set; }
    public string? Note { get; set; }
    public int? ParkingZoneId { get; set; }
    public int? ParkingSlotId { get; set; }

    /// <summary>
    /// true khi người gọi muốn gỡ khu/ô đỗ. Khi false và ParkingZoneId/ParkingSlotId đều null,
    /// hệ thống giữ nguyên gán hiện tại thay vì xoá (tránh mất dữ liệu khi cập nhật một phần).
    /// </summary>
    public bool ClearParkingAssignment { get; set; }
}

public sealed record VehicleOperationResult(
    bool Succeeded,
    string? ErrorCode = null,
    VehicleDto? Vehicle = null)
{
    public static VehicleOperationResult Success(VehicleDto? vehicle = null) => new(true, null, vehicle);
    public static VehicleOperationResult Fail(string errorCode) => new(false, errorCode, null);
}

/// <summary>
/// Kết quả tra cứu nhanh biển số / 4 số cuối / QR.
/// Khi người gọi là Khách thuê, TenantName luôn là null để không lộ thông tin cá nhân/SĐT.
/// </summary>
public sealed record VehicleLookupItemDto(
    int VehicleId,
    int PropertyId,
    string PropertyName,
    int RoomId,
    string RoomCode,
    string PlateNumber,
    VehicleType VehicleType,
    string? Brand,
    string? Color,
    string? ParkingZoneName,
    string? ParkingSlotCode,
    string? TenantName);

public sealed record BlockingAlertRequest
{
    public int PropertyId { get; set; }
    public string PlateQuery { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed record BlockingAlertResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? MatchedPlateNumber = null,
    string? RoomCode = null,
    int NotifiedCount = 0)
{
    public static BlockingAlertResult Success(string plate, string roomCode, int notifiedCount) =>
        new(true, null, plate, roomCode, notifiedCount);

    public static BlockingAlertResult Fail(string errorCode) => new(false, errorCode);
}

public sealed record VehiclePropertySettingDto(
    int Id,
    int PropertyId,
    string PropertyName,
    decimal ElectricMotorbikeMonthlyFee,
    decimal ManualMotorbikeMonthlyFee,
    decimal ScooterMonthlyFee,
    int MaxVehiclesPerRoom,
    bool EnableParkingZones,
    int ZoneNearFullThresholdPercent,
    bool AllowTenantGuestNotice,
    bool AllowBlockingAlert,
    bool AllowManagerManageVehicles,
    DateTimeOffset UpdatedAt);

public sealed record VehiclePropertySettingSaveRequest
{
    public int PropertyId { get; set; }
    public decimal ElectricMotorbikeMonthlyFee { get; set; } = 50_000m;
    public decimal ManualMotorbikeMonthlyFee { get; set; } = 80_000m;
    public decimal ScooterMonthlyFee { get; set; } = 100_000m;
    public int MaxVehiclesPerRoom { get; set; } = 2;
    public bool EnableParkingZones { get; set; }
    public int ZoneNearFullThresholdPercent { get; set; } = 85;
    public bool AllowTenantGuestNotice { get; set; } = true;
    public bool AllowBlockingAlert { get; set; } = true;
    public bool AllowManagerManageVehicles { get; set; } = true;
}

public sealed record ParkingSlotDto(
    int Id,
    int ParkingZoneId,
    string Code,
    ParkingSlotStatus Status,
    int? CurrentVehicleId,
    string? CurrentPlateNumber,
    string? CurrentRoomCode,
    VehicleType? CurrentVehicleType);

public sealed record ParkingZoneDto(
    int Id,
    int PropertyId,
    string PropertyName,
    string Name,
    bool IsIndoor,
    int Capacity,
    int OccupiedCount,
    int UsagePercent,
    bool IsNearFull,
    bool IsFull,
    AllowedVehicleTypeFlags AllowedVehicleTypes,
    bool HasSlots,
    bool IsFree,
    decimal? OverrideMonthlyFee,
    bool IsActive,
    string? SlotPrefix,
    IReadOnlyList<ParkingSlotDto> Slots);

public sealed record ParkingZoneSaveRequest
{
    public int? Id { get; set; }
    public int PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsIndoor { get; set; } = true;
    public int Capacity { get; set; } = 20;
    public AllowedVehicleTypeFlags AllowedVehicleTypes { get; set; } = AllowedVehicleTypeFlags.All;
    public bool HasSlots { get; set; }
    public bool IsFree { get; set; }
    public decimal? OverrideMonthlyFee { get; set; }
    public bool IsActive { get; set; } = true;
    // null = giữ tiền tố đã lưu (hoặc "A" cho khu mới) thay vì ghi đè về "A" khi sửa khu.
    public string? SlotPrefix { get; set; }
    public int? GenerateSlotCount { get; set; }
}

public sealed record GuestVehicleNoticeDto(
    int Id,
    int PropertyId,
    string PropertyName,
    int RoomId,
    string RoomCode,
    int? TenantId,
    string? TenantName,
    string PlateNumber,
    VehicleType VehicleType,
    string? GuestName,
    DateTimeOffset ExpectedFrom,
    DateTimeOffset ExpectedTo,
    string? Note,
    GuestVehicleNoticeStatus Status,
    DateTimeOffset CreatedAt);

public sealed record GuestVehicleNoticeCreateRequest
{
    public int RoomId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; } = VehicleType.ManualMotorbike;
    public string? GuestName { get; set; }
    public DateTimeOffset ExpectedFrom { get; set; }
    public DateTimeOffset ExpectedTo { get; set; }
    public string? Note { get; set; }
}

public sealed record VehicleAuditItemDto(
    int Id,
    int AuditSessionId,
    int? VehicleId,
    int? GuestNoticeId,
    string PlateNumberSnapshot,
    int RoomIdSnapshot,
    string RoomCodeSnapshot,
    VehicleType VehicleTypeSnapshot,
    string? Brand,
    string? Color,
    string? ParkingZoneName,
    string? ParkingSlotCode,
    VehicleAuditItemResult Result,
    DateTimeOffset? CheckedAt,
    string? Note);

public sealed record VehicleAuditSessionDto(
    int Id,
    int PropertyId,
    string PropertyName,
    string AuditedByUserId,
    string? AuditedByName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    VehicleAuditSessionStatus Status,
    int TotalExpected,
    int TotalPresent,
    int TotalMissing,
    int TotalPending,
    string? Note,
    IReadOnlyList<VehicleAuditItemDto> Items);

public sealed record VehicleIncidentCreateRequest
{
    public int RoomId { get; set; }
    public int? VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed record ContractVehicleBillingSummaryDto(
    Guid ContractId,
    int RoomId,
    int ActiveVehicleCount,
    int ElectricMotorbikeCount,
    int ManualMotorbikeCount,
    int ScooterCount,
    decimal TotalMonthlyFee,
    string InvoiceLineDescription);
