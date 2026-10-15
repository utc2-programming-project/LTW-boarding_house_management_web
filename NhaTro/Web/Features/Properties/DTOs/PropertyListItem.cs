namespace Web.Features.Properties.DTOs;

// Dong list nha tro. RoomCount do service dien (dem phong chua xoa mem), mac dinh 0.
public sealed class PropertyListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public int RoomCount { get; set; }
}
