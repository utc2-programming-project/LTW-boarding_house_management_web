using Web.Features.Rooms.Enums;

namespace Web.Features.Rooms.DTOs;

// Du lieu chon phong ban giao cho Dao (hop dong VHT-11/VHT-29) va query P8 (Tinh VHT-41).
// Gon nhe cho dropdown: khong kem Floor/Area/CreatedAt.
public sealed class RoomOption
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal StandardPrice { get; set; }
    public RoomStatus Status { get; set; }
}
