namespace Web.Components.Account;

/// <summary>
/// Nhận diện request tài nguyên tĩnh (CSS, JS, font, ảnh, _content, _framework...).
///
/// Hai middleware chặn-route (<see cref="PendingVerificationMiddleware"/>,
/// <see cref="PendingRoleMiddleware"/>) chỉ được chuyển hướng điều hướng trang.
/// Nếu chuyển hướng cả asset thì trình duyệt nhận HTML kèm Content-Type text/html
/// cho thẻ &lt;link rel="stylesheet"&gt; và bỏ qua stylesheet, giao diện mất toàn bộ style.
/// </summary>
public static class StaticAssetRequest
{
    /// <summary>
    /// True khi request không phải điều hướng trang. Ưu tiên Sec-Fetch-Dest của trình duyệt;
    /// client cũ không gửi header này thì xét Accept rồi tới phần mở rộng của đường dẫn.
    /// </summary>
    public static bool IsStaticAsset(HttpRequest request)
    {
        var destination = request.Headers["Sec-Fetch-Dest"].ToString();
        if (destination.Length > 0
            && !string.Equals(destination, "document", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(destination, "empty", StringComparison.OrdinalIgnoreCase))
        {
            // style/script/image/font/iframe... không bao giờ là điều hướng trang.
            return true;
        }

        // "empty" là fetch()/XHR: enhanced navigation của Blazor cũng đi đường này,
        // nên vẫn phải xét Accept để không bỏ lọt điều hướng trang.
        var accept = request.Headers.Accept.ToString();
        if (accept.Length > 0)
        {
            return !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }

        return System.IO.Path.HasExtension(request.Path.Value);
    }
}
