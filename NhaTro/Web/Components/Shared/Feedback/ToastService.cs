namespace Web.Components.Shared.Feedback;

public enum ToastKind
{
    Success,
    Error,
}

/// <summary>Thông báo lỗi cần được người dùng đóng; thông báo thành công tự đóng.</summary>
public sealed record ToastMessage(Guid Id, ToastKind Kind, string Text)
{
    public bool AutoClose => Kind == ToastKind.Success;
}

/// <summary>
/// Hàng đợi toast của một phiên (circuit). Inject vào trang/service rồi gọi
/// <see cref="Success"/> hoặc <see cref="Error"/>; <c>ToastHost</c> trong layout sẽ hiển thị.
/// </summary>
public sealed class ToastService
{
    private readonly List<ToastMessage> _toasts = [];

    public IReadOnlyList<ToastMessage> Toasts => _toasts;

    /// <summary>Báo khi danh sách thay đổi (thêm/đóng).</summary>
    public event Action? Changed;

    public void Success(string text) => Add(ToastKind.Success, text);

    public void Error(string text) => Add(ToastKind.Error, text);

    public void Dismiss(Guid id)
    {
        if (_toasts.RemoveAll(t => t.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }

    private void Add(ToastKind kind, string text)
    {
        _toasts.Add(new ToastMessage(Guid.NewGuid(), kind, text));
        Changed?.Invoke();
    }
}
