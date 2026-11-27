using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.Entities;

/// <summary>
/// Phiếu báo xe bạn của khách thuê gửi nhờ / qua đêm để phân biệt với xe lạ (không tính phí tháng).
/// </summary>
public sealed class GuestVehicleNotice
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    public int TenantId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; } = VehicleType.ManualMotorbike;
    public string? GuestName { get; set; }
    public DateTimeOffset ExpectedFrom { get; set; }
    public DateTimeOffset ExpectedTo { get; set; }
    public string? Note { get; set; }
    public GuestVehicleNoticeStatus Status { get; set; } = GuestVehicleNoticeStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
}
