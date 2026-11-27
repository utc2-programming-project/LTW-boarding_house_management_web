using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.Entities;

/// <summary>
/// Ô đỗ xe cố định trong một ParkingZone có HasSlots = true (ví dụ: A01, A02...).
/// </summary>
public sealed class ParkingSlot
{
    public int Id { get; set; }
    public int ParkingZoneId { get; set; }
    public string Code { get; set; } = string.Empty;
    public ParkingSlotStatus Status { get; set; } = ParkingSlotStatus.Available;
    public int? CurrentVehicleId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
