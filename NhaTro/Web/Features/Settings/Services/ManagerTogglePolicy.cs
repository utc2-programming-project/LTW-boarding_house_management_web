using Web.Features.Accounts;
using Web.Features.Rooms;
using Web.Features.Settings.Enums;

namespace Web.Features.Settings.Services;

/// <summary>
/// VHT-94 — cài đặt policy handler. Đọc chế độ công tắc từ SQL qua <see cref="IManagerToggleReader"/>
/// rồi quy đổi sang quyết định của tầng nghiệp vụ.
/// </summary>
public sealed class ManagerTogglePolicy(
    IManagerToggleReader reader,
    ILogger<ManagerTogglePolicy> logger) : IManagerTogglePolicy, IRoomChangePolicy
{
    public Task<RoomChangeDecision> EvaluateAsync(
        ManagerAction action, string role, CancellationToken ct = default) =>
        EvaluateAsync(ManagerToggleCatalog.ToCode(action), role, ct);

    public async Task<RoomChangeDecision> EvaluateAsync(
        string code, string role, CancellationToken ct = default)
    {
        // D4: Chủ trọ là cấp cao nhất, thao tác của Chủ trọ luôn có hiệu lực ngay.
        // Lệnh cấm tự duyệt chỉ áp dụng cho Quản lý nên không đọc công tắc ở đây.
        if (role == Roles.ChuTro)
        {
            return RoomChangeDecision.Allowed;
        }

        if (!ManagerToggleCatalog.IsKnown(code))
        {
            // Không có công tắc thì không được lọt qua: fail closed, không mặc định cho phép.
            logger.LogError("Mã công tắc không tồn tại: {Code}", code);
            return RoomChangeDecision.Denied;
        }

        var mode = await reader.GetModeAsync(code, ct);
        return ToDecision(mode);
    }

    /// <summary>Điểm gọi cũ của P2 (VHT-7) — Quản lý sửa phòng đi qua SW-01/02/03.</summary>
    public Task<RoomChangeDecision> EvaluateAsync(
        RoomChangeRequest request, CancellationToken ct = default) =>
        EvaluateAsync(ManagerToggleCatalog.ToCode(ToAction(request.Kind)), request.Role, ct);

    public static ManagerAction ToAction(RoomChangeKind kind) => kind switch
    {
        RoomChangeKind.ChangePrice => ManagerAction.SW02,
        RoomChangeKind.ChangeStatus => ManagerAction.SW03,
        _ => ManagerAction.SW01,
    };

    public static RoomChangeDecision ToDecision(ManagerToggleMode mode) => mode switch
    {
        ManagerToggleMode.Allow => RoomChangeDecision.Allowed,
        ManagerToggleMode.Approve => RoomChangeDecision.RequiresApproval,
        ManagerToggleMode.Deny => RoomChangeDecision.Denied,
        _ => RoomChangeDecision.Denied,
    };
}
