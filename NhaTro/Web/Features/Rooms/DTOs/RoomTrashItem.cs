using Web.Features.Rooms.Enums;

namespace Web.Features.Rooms.DTOs;

// Dòng thùng rác phòng (VHT-98). DeletedAt/DeletedBy lấy từ dấu vết xóa mềm.
public sealed class RoomTrashItem
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int? Floor { get; set; }
    public decimal StandardPrice { get; set; }
    public RoomStatus Status { get; set; }
    public DateTime? DeletedAt { get; set; }
}
