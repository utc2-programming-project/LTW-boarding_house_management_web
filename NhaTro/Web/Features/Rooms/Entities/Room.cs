using Web.Features.Rooms.Enums;

namespace Web.Features.Rooms.Entities;

public class Room
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    public string Code { get; set; } = string.Empty;

    public int? Floor { get; set; }

    public decimal? AreaM2 { get; set; }

    public decimal StandardPrice { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Available;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
}
