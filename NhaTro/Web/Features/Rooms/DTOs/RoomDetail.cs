using Web.Features.Rooms.Enums;

namespace Web.Features.Rooms.DTOs;

// Chi tiet 1 phong de hien thi gia/trang thai (Phuc: VHT-16).
public sealed class RoomDetail
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
