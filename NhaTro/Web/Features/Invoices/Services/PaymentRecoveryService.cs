using Microsoft.JSInterop;

namespace Web.Features.Invoices.Services;

/// <summary>
/// VHT-143: giữ ngữ cảnh tra cứu P6 tối thiểu theo tab và báo trang khi circuit phục hồi.
/// Chỉ lưu định danh thao tác (hóa đơn/yêu cầu, tài khoản, loại thao tác); không lưu
/// số tiền, mã giao dịch, thời điểm chuyển, minh chứng hay payload biểu mẫu.
/// </summary>
public sealed class PaymentRecoveryService(IJSRuntime js) : IAsyncDisposable
{
    private const string ModulePath = "./js/payment-recovery.js";

    private IJSObjectReference? _module;

    /// <summary>Ghi marker trước lệnh ghi. Trả false khi trình duyệt không lưu được.</summary>
    public async Task<bool> MarkAsync(
        string target,
        string operation,
        string userId,
        CancellationToken cancellationToken = default) =>
        await (await ModuleAsync(cancellationToken)).InvokeAsync<bool>(
            "mark",
            cancellationToken,
            target,
            operation,
            userId);

    /// <summary>Xóa marker sau khi đã có kết quả chắc chắn từ server.</summary>
    public async Task ClearAsync(string target, CancellationToken cancellationToken = default) =>
        await (await ModuleAsync(cancellationToken)).InvokeVoidAsync("clear", cancellationToken, target);

    /// <summary>Kiểm tra marker còn hiệu lực cho đúng tài khoản và đúng mục tiêu không.</summary>
    public async Task<bool> HasAsync(
        string target,
        string userId,
        CancellationToken cancellationToken = default) =>
        await (await ModuleAsync(cancellationToken)).InvokeAsync<bool>(
            "has",
            cancellationToken,
            target,
            userId);

    /// <summary>Đăng ký nhận sự kiện reconnect/resume của VHT-62 cho trang P6.</summary>
    public async Task ConnectAsync<T>(
        DotNetObjectReference<T> reference,
        CancellationToken cancellationToken = default)
        where T : class =>
        await (await ModuleAsync(cancellationToken)).InvokeVoidAsync("connect", cancellationToken, reference);

    public async Task DisconnectAsync<T>(
        DotNetObjectReference<T> reference,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (_module is null)
        {
            return;
        }

        await _module.InvokeVoidAsync("disconnect", cancellationToken, reference);
    }

    private async Task<IJSObjectReference> ModuleAsync(CancellationToken cancellationToken) =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
