namespace Web.Features.Properties.Entities;

/// <summary>
/// VHT-24: phân công Quản lý theo tòa nhà (quan hệ nhiều-nhiều, có lịch sử).
/// Bỏ gán là set <see cref="RevokedAt"/> thay vì xóa dòng; bản ghi đang hiệu lực có
/// <see cref="RevokedAt"/> == null và là cơ sở để lọc phạm vi trong query/update.
/// </summary>
public class PropertyManager
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    public string ManagerUserId { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public string AssignedByUserId { get; set; } = string.Empty;

    /// <summary>NULL khi phân công đang hiệu lực; có giá trị khi đã bỏ gán.</summary>
    public DateTime? RevokedAt { get; set; }

    public string? RevokedByUserId { get; set; }

    public bool IsActive => RevokedAt is null;
}
