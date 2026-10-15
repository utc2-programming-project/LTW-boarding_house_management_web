using Web.Features.Rooms.Enums;

namespace Web.Features.Rooms.DTOs;

// Dong list phong. PropertyName do service dien (join Properties), khong luu trong Rooms.
public sealed class RoomListItem
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int? Floor { get; set; }
    public decimal? AreaM2 { get; set; }
    public decimal StandardPrice { get; set; }
    public RoomStatus Status { get; set; }
    public bool IsActive { get; set; }
}
