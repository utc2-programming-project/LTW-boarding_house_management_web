using Web.Features.Settings.DTOs;
using Web.Features.Settings.Enums;

namespace Web.Features.Settings.Services;

/// <summary>
/// Service đọc/ghi 21 công tắc Quản lý (VHT-94). Mọi thao tác đều kiểm quyền bên trong:
/// chỉ Chủ trọ được đọc/ghi; Quản lý và Admin bị từ chối (D1, D12).
/// </summary>
public interface IManagerToggleService
{
    /// <summary>Toàn bộ công tắc theo thứ tự danh mục. Thiếu dòng trong SQL thì trả chế độ mặc định.</summary>
    Task<ManagerToggleListResult> GetAllAsync(ManagerToggleCaller caller, CancellationToken ct = default);

    /// <summary>Đổi chế độ một công tắc. Trả lỗi khi mã không có, chế độ không được phép, hoặc người gọi không phải Chủ trọ.</summary>
    Task<ManagerToggleResult> SetAsync(
        string code, ManagerToggleMode mode, ManagerToggleCaller caller, CancellationToken ct = default);
}

/// <summary>
/// Đọc chế độ công tắc cho tầng policy (nội bộ, không kiểm quyền người gọi): policy cần biết
/// công tắc đang để gì để quyết định thay cho Quản lý, còn ai được sửa công tắc là việc của
/// <see cref="IManagerToggleService"/>.
/// </summary>
public interface IManagerToggleReader
{
    /// <summary>Chế độ hiện tại của công tắc; mã lạ hoặc thiếu dòng thì trả chế độ mặc định của danh mục.</summary>
    Task<ManagerToggleMode> GetModeAsync(string code, CancellationToken ct = default);
}
