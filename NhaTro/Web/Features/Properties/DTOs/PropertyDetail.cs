namespace Web.Features.Properties.DTOs;

// Chi tiet 1 nha tro. OwnerId tra ve de UI hien dung pham vi, khong cho client sua.
public sealed class PropertyDetail
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
