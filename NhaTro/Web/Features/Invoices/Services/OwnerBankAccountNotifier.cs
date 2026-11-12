namespace Web.Features.Invoices.Services;

/// <summary>
/// PLAN-028: cầu nối giữa trang /profile và banner nhắc Chủ trọ nhập tài khoản nhận tiền.
/// Sau khi lưu hồ sơ, trang gọi <see cref="NotifyChanged"/> để banner tự ẩn/hiện lại
/// mà không cần tải lại trang.
/// </summary>
public sealed class OwnerBankAccountNotifier
{
    /// <summary>Phát khi tài khoản nhận tiền của người đang đăng nhập vừa thay đổi.</summary>
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
