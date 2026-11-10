namespace Web.Infrastructure.Email;

/// <summary>
/// Gửi thư tuỳ ý ngoài 3 hàm cố định của <c>IEmailSender&lt;ApplicationUser&gt;</c>
/// (dùng cho lời mời khách thuê — PLAN-006).
/// </summary>
public interface IOutboundEmailSender
{
    /// <summary>Gửi một thư HTML tới địa chỉ người dùng; tự áp danh sách chuyển hướng demo.</summary>
    Task SendAsync(string toEmail, string subject, string html, CancellationToken ct = default);
}
