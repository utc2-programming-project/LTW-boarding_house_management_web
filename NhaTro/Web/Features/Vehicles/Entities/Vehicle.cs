using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.Entities;

/// <summary>
/// Phương tiện 2 bánh của khách thuê gắn với phòng và hợp đồng đang hiệu lực.
/// Giữ lịch sử qua trạng thái Active/Inactive, không xóa vật lý.
/// </summary>
public sealed class Vehicle
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    public Guid ContractId { get; set; }
    public int? TenantId { get; set; }
    public VehicleType VehicleType { get; set; } = VehicleType.ManualMotorbike;
    public string PlateNumber { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Color { get; set; }
    public string? PhotoRef { get; set; }
    public string? Note { get; set; }
    public string QrCodeToken { get; set; } = string.Empty;
    public int? ParkingZoneId { get; set; }
    public int? ParkingSlotId { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Active;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? EndReason { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
