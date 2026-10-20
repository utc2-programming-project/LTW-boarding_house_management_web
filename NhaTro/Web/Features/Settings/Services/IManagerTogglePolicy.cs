using Web.Features.Rooms;
using Web.Features.Settings.Enums;

namespace Web.Features.Settings.Services;

/// <summary>
/// Policy handler dùng chung cho mọi thao tác ⚙️ (VHT-94):
/// Cho phép → <see cref="RoomChangeDecision.Allowed"/> (thực thi ngay);
/// Duyệt → <see cref="RoomChangeDecision.RequiresApproval"/> (trả "cần tạo yêu cầu", hàng đợi NEW-03);
/// Không thể → <see cref="RoomChangeDecision.Denied"/> (từ chối).
/// Chủ trọ luôn thực thi ngay (D4) và không phụ thuộc công tắc.
/// </summary>
public interface IManagerTogglePolicy
{
    Task<RoomChangeDecision> EvaluateAsync(
        ManagerAction action, string role, CancellationToken ct = default);

    Task<RoomChangeDecision> EvaluateAsync(
        string code, string role, CancellationToken ct = default);
}
