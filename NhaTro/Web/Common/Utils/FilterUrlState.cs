using Microsoft.JSInterop;

namespace Web.Common.Utils;

/// <summary>
/// Ghi trạng thái bộ lọc hiện tại vào query string (history.replaceState) để tải lại/chia sẻ
/// vẫn giữ đúng trang. Không điều hướng Blazor nên không kích hoạt tải lại dữ liệu.
/// </summary>
public sealed class FilterUrlState(IJSRuntime js)
{
    public async Task ReplaceAsync(IReadOnlyDictionary<string, string?> values)
    {
        try
        {
            await js.InvokeVoidAsync("appFilterUrl.replace", values);
        }
        catch (JSDisconnectedException)
        {
            // Circuit đã đóng: không còn gì để đồng bộ.
        }
        catch (InvalidOperationException)
        {
            // Đang prerender (chưa có JS): bỏ qua.
        }
    }
}
