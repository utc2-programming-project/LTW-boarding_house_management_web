namespace Web.Features.Rooms;

/// <summary>
/// Loại thay đổi phòng mà Quản lý thực hiện — tương ứng 3 công tắc SW-01/SW-02/SW-03
/// trong docs/task/thao_tac_va_quyen.xlsx.
/// </summary>
public enum RoomChangeKind
{
    EditInfo = 1,
    ChangePrice = 2,
    ChangeStatus = 3,
}

/// <summary>Kết quả công tắc: Cho phép / Duyệt / Không thể.</summary>
public enum RoomChangeDecision
{
    Allowed = 0,
    RequiresApproval = 1,
    Denied = 2,
}

/// <summary>Yêu cầu kiểm công tắc trước khi Quản lý sửa phòng.</summary>
public sealed record RoomChangeRequest(
    string UserId,
    string Role,
    int RoomId,
    RoomChangeKind Kind);

/// <summary>
/// Điểm gọi policy công tắc Quản lý (VHT-94). VHT-7 chỉ chừa điểm gọi:
/// hiện dùng <see cref="RoomChangePolicyStub"/>; VHT-94 thay bằng policy đọc 16 công tắc thật
/// và hàng đợi duyệt (VHT-96).
/// </summary>
public interface IRoomChangePolicy
{
    Task<RoomChangeDecision> EvaluateAsync(RoomChangeRequest request, CancellationToken ct = default);
}

/// <summary>
/// Tạm thời theo mặc định của ma trận (Duyệt) khi VHT-94 chưa có:
/// Chủ trọ luôn có hiệu lực ngay (D4); Quản lý chờ duyệt cho tới khi công tắc được cấu hình.
/// </summary>
public sealed class RoomChangePolicyStub : IRoomChangePolicy
{
    public Task<RoomChangeDecision> EvaluateAsync(RoomChangeRequest request, CancellationToken ct = default) =>
        Task.FromResult(request.Role == Accounts.Roles.ChuTro
            ? RoomChangeDecision.Allowed
            : RoomChangeDecision.RequiresApproval);
}
