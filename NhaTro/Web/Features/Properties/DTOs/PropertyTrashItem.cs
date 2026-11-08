namespace Web.Features.Properties.DTOs;

/// <summary>Một nhà trọ đang nằm trong thùng rác (VHT: xóa mềm nhà trọ).</summary>
public sealed class PropertyTrashItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }

    /// <summary>
    /// Số phòng đã xóa mềm của nhà trọ này. Nhà còn phòng sống thì không xóa được,
    /// nên đây chính là số phòng chờ khôi phục sau khi nhà trọ trở lại danh sách.
    /// </summary>
    public int TrashedRoomCount { get; set; }

    public DateTime? DeletedAt { get; set; }
}
