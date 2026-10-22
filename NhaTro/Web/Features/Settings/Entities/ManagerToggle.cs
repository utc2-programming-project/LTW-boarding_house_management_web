using Web.Features.Settings.Enums;

namespace Web.Features.Settings.Entities;

/// <summary>
/// Một dòng công tắc Quản lý lưu trong SQL (VHT-94). Khóa chính là mã công tắc
/// (SW-01…SW-16, SW-15a…SW-15e) để đọc theo mã mà không cần join danh mục.
/// Công tắc <b>không</b> lưu trong appsettings: Chủ trọ sửa lúc chạy.
/// </summary>
public class ManagerToggle
{
    public string Code { get; set; } = string.Empty;

    public ManagerToggleMode Mode { get; set; } = ManagerToggleMode.Approve;

    /// <summary>Thời điểm đổi gần nhất (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Chủ trọ đổi gần nhất; null với dòng seed.</summary>
    public string? UpdatedByUserId { get; set; }
}
