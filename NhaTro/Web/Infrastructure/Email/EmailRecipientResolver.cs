// Quyết định địa chỉ nhận thư thật cho một email người dùng.
using Microsoft.Extensions.Options;
using Web.Configuration.Options;

namespace Web.Infrastructure.Email;

/// <summary>Địa chỉ nhận thư sau khi áp dụng danh sách chuyển hướng của cấu hình.</summary>
/// <param name="Address">Địa chỉ thực sự nhận thư.</param>
/// <param name="OriginalAddress">Địa chỉ gốc của người dùng (có thể không tồn tại).</param>
public sealed record EmailRecipient(string Address, string OriginalAddress)
{
    /// <summary>Đã bị chuyển hướng sang hộp thư khác hay chưa.</summary>
    public bool Redirected =>
        !string.Equals(Address, OriginalAddress, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Quyết định hộp thư thật sẽ nhận thư. Dữ liệu mẫu dùng địa chỉ không tồn tại
/// (admin@admin.com, chutro@nhatro.local...) nên thư được chuyển về hộp thư demo.
/// </summary>
public sealed class EmailRecipientResolver(IOptions<EmailOptions> options)
{
    private readonly EmailOptions settings = options.Value;

    /// <summary>
    /// Thứ tự ưu tiên: danh sách chuyển hướng riêng → chế độ demo → giữ nguyên địa chỉ gốc.
    /// </summary>
    public EmailRecipient Resolve(string? requestedAddress)
    {
        var original = (requestedAddress ?? string.Empty).Trim();

        // Không có địa chỉ gốc thì không gửi đi đâu cả (tránh gửi nhầm vào hộp thư demo).
        if (original.Length == 0)
        {
            return new EmailRecipient(string.Empty, string.Empty);
        }

        if (settings.FindRecipientOverride(original) is { } mapped)
        {
            return new EmailRecipient(mapped, original);
        }

        if (settings.DemoMode && !string.IsNullOrWhiteSpace(settings.FallbackRecipient))
        {
            return new EmailRecipient(settings.FallbackRecipient.Trim(), original);
        }

        return new EmailRecipient(original, original);
    }
}
