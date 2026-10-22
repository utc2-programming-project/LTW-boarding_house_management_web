using Web.Features.Settings.Enums;

namespace Web.Features.Settings.DTOs;

/// <summary>
/// Một công tắc trả cho UI cấu hình (NEW-04 — Phú). Kèm tên tiếng Việt và danh sách chế độ
/// được phép để UI không tự suy luận quy tắc của SW-15 / SW-15a…e.
/// </summary>
public sealed record ManagerToggleItem(
    string Code,
    string Name,
    string Group,
    ManagerToggleMode Mode,
    IReadOnlyList<ManagerToggleMode> AllowedModes,
    DateTimeOffset UpdatedAt,
    string? UpdatedByUserId);
