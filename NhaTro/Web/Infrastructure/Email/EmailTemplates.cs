// Khuôn (template) thư HTML dùng chung cho các thư hệ thống.
using System.Globalization;
using System.Net;

namespace Web.Infrastructure.Email;

/// <summary>
/// Nội dung thư HTML tối giản (CSS inline) cho các thư hệ thống: xác nhận tài khoản,
/// đặt lại mật khẩu, mã đặt lại mật khẩu và mời khách thuê.
/// Không phụ thuộc file ngoài để hộp thư hiển thị giống nhau mọi nơi.
/// </summary>
public static class EmailTemplates
{
    public sealed record EmailContent(string Subject, string Html);

    public static EmailContent PasswordReset(
        string? displayName,
        string accountEmail,
        EmailRecipient recipient,
        string resetLink)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? accountEmail : displayName.Trim();
        var account = WebUtility.HtmlEncode(accountEmail);

        return IsEnglish
            ? new EmailContent(
                Prefix(recipient) + $"Reset your password ({accountEmail})",
                Layout(
                    heading: "Reset your password",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Hi <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 16px">We received a request to reset the password for
                        <strong>{account}</strong>.</p>
                        {Button("Reset password", resetLink)}
                        <p style="margin:16px 0 0;font-size:13px;color:#6b7280">The link expires in 24 hours
                        and can be used once.</p>
                        <p style="margin:8px 0 0;font-size:13px;color:#6b7280">If you did not request this,
                        ignore this email; your password stays unchanged.</p>
                        """))
            : new EmailContent(
                Prefix(recipient) + $"Đặt lại mật khẩu ({accountEmail})",
                Layout(
                    heading: "Đặt lại mật khẩu",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Xin chào <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 16px">Hệ thống quản lý nhà trọ nhận được yêu cầu đặt lại mật khẩu
                        cho tài khoản <strong>{account}</strong>.</p>
                        {Button("Đặt lại mật khẩu", resetLink)}
                        <p style="margin:16px 0 0;font-size:13px;color:#6b7280">Liên kết có hiệu lực trong
                        24 giờ và chỉ dùng được một lần.</p>
                        <p style="margin:8px 0 0;font-size:13px;color:#6b7280">Nếu bạn không yêu cầu, hãy bỏ qua
                        thư này; mật khẩu hiện tại không thay đổi.</p>
                        """));
    }

    public static EmailContent ConfirmAccount(
        string? displayName,
        string accountEmail,
        EmailRecipient recipient,
        string confirmationLink)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? accountEmail : displayName.Trim();
        var account = WebUtility.HtmlEncode(accountEmail);

        return IsEnglish
            ? new EmailContent(
                Prefix(recipient) + $"Confirm your account ({accountEmail})",
                Layout(
                    heading: "Confirm your account",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Hi <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 16px">Please confirm the account <strong>{account}</strong>
                        to start using the boarding-house system.</p>
                        {Button("Confirm account", confirmationLink)}
                        """))
            : new EmailContent(
                Prefix(recipient) + $"Xác nhận tài khoản ({accountEmail})",
                Layout(
                    heading: "Xác nhận tài khoản",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Xin chào <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 16px">Vui lòng xác nhận tài khoản <strong>{account}</strong>
                        để bắt đầu dùng hệ thống quản lý nhà trọ.</p>
                        {Button("Xác nhận tài khoản", confirmationLink)}
                        """));
    }

    public static EmailContent PasswordResetCode(
        string? displayName,
        string accountEmail,
        EmailRecipient recipient,
        string resetCode)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? accountEmail : displayName.Trim();
        var account = WebUtility.HtmlEncode(accountEmail);
        var code = WebUtility.HtmlEncode(resetCode);

        return IsEnglish
            ? new EmailContent(
                Prefix(recipient) + $"Password reset code ({accountEmail})",
                Layout(
                    heading: "Password reset code",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Hi <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 16px">Use the code below to reset the password for
                        <strong>{account}</strong>.</p>
                        <p style="margin:0 0 16px;font-size:26px;font-weight:700;letter-spacing:4px">{code}</p>
                        """))
            : new EmailContent(
                Prefix(recipient) + $"Mã đặt lại mật khẩu ({accountEmail})",
                Layout(
                    heading: "Mã đặt lại mật khẩu",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Xin chào <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 16px">Dùng mã dưới đây để đặt lại mật khẩu cho
                        <strong>{account}</strong>.</p>
                        <p style="margin:0 0 16px;font-size:26px;font-weight:700;letter-spacing:4px">{code}</p>
                        """));
    }

    public static EmailContent Otp(
        string accountEmail,
        string code,
        string purposeLabel,
        int lifetimeMinutes)
    {
        var account = WebUtility.HtmlEncode(accountEmail);
        var safeCode = WebUtility.HtmlEncode(code);
        var purpose = WebUtility.HtmlEncode(purposeLabel);
        var recipient = new EmailRecipient(accountEmail, accountEmail);

        return IsEnglish
            ? new EmailContent(
                Prefix(recipient) + $"Your verification code ({accountEmail})",
                Layout(
                    heading: "Verification code",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Use this code to complete {purpose} for
                        <strong>{account}</strong>.</p>
                        <p style="margin:0 0 16px;font-size:30px;font-weight:700;letter-spacing:8px">{safeCode}</p>
                        <p style="margin:0;font-size:13px;color:#6b7280">The code expires in {lifetimeMinutes} minutes
                        and can be used once. Do not share it with anyone.</p>
                        """))
            : new EmailContent(
                Prefix(recipient) + $"Mã xác thực ({accountEmail})",
                Layout(
                    heading: "Mã xác thực",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Dùng mã dưới đây để hoàn tất {purpose} cho
                        <strong>{account}</strong>.</p>
                        <p style="margin:0 0 16px;font-size:30px;font-weight:700;letter-spacing:8px">{safeCode}</p>
                        <p style="margin:0;font-size:13px;color:#6b7280">Mã hết hạn sau {lifetimeMinutes} phút
                        và chỉ dùng được một lần. Không chia sẻ mã với người khác.</p>
                        """));
    }

    /// <summary>Thư mời khách thuê đặt mật khẩu để tham gia hệ thống (PLAN-006).</summary>
    public static EmailContent TenantInvitation(
        string? displayName,
        string accountEmail,
        EmailRecipient recipient,
        string inviteLink,
        string ownerName)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? accountEmail : displayName.Trim();
        var account = WebUtility.HtmlEncode(accountEmail);
        var owner = WebUtility.HtmlEncode(ownerName);

        return IsEnglish
            ? new EmailContent(
                Prefix(recipient) + $"Invitation to join the boarding-house system ({accountEmail})",
                Layout(
                    heading: "You are invited",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Hi <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 16px"><strong>{owner}</strong> invited you to the boarding-house
                        system with the account <strong>{account}</strong>.</p>
                        {Button("Set your password", inviteLink)}
                        <p style="margin:16px 0 0;font-size:13px;color:#6b7280">The invitation expires in 15 minutes.
                        If you did not expect this email, you can ignore it.</p>
                        """))
            : new EmailContent(
                Prefix(recipient) + $"Lời mời tham gia hệ thống nhà trọ ({accountEmail})",
                Layout(
                    heading: "Bạn được mời tham gia",
                    recipient,
                    body: $"""
                        <p style="margin:0 0 12px">Xin chào <strong>{WebUtility.HtmlEncode(name)}</strong>,</p>
                        <p style="margin:0 0 16px"><strong>{owner}</strong> mời bạn tham gia hệ thống quản lý
                        nhà trọ với tài khoản <strong>{account}</strong>.</p>
                        {Button("Đặt mật khẩu", inviteLink)}
                        <p style="margin:16px 0 0;font-size:13px;color:#6b7280">Lời mời hết hạn sau 15 phút.
                        Nếu bạn không mong đợi thư này, hãy bỏ qua.</p>
                        """));
    }

    private static bool IsEnglish =>
        string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    private static string Prefix(EmailRecipient recipient) => recipient.Redirected ? "[Demo] " : string.Empty;

    private static string Button(string text, string url) =>
        $"""
        <p style="margin:0 0 8px">
          <a href="{url}" style="display:inline-block;padding:10px 18px;background:#0f766e;color:#ffffff;
             text-decoration:none;border-radius:6px;font-weight:600">{text}</a>
        </p>
        <p style="margin:0;font-size:12px;color:#6b7280;word-break:break-all">{url}</p>
        """;

    private static string Layout(string heading, EmailRecipient recipient, string body) =>
        $"""
        <div style="font-family:'Segoe UI',Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2937;
             background:#ffffff;padding:24px">
          <h2 style="margin:0 0 16px;color:#0f766e;font-size:20px">{heading}</h2>
          {RedirectNote(recipient)}
          {body}
          <hr style="margin:24px 0;border:none;border-top:1px solid #e5e7eb" />
          <p style="margin:0;font-size:12px;color:#9ca3af">
            Thư tự động từ hệ thống quản lý nhà trọ. Vui lòng không trả lời thư này.
          </p>
        </div>
        """;

    private static string RedirectNote(EmailRecipient recipient) =>
        recipient.Redirected
            ? $"""
               <p style="margin:0 0 16px;padding:8px 12px;background:#fef3c7;border-radius:6px;font-size:13px">
                 Thư này được chuyển tới hộp thư demo. Người nhận gốc:
                 <strong>{WebUtility.HtmlEncode(recipient.OriginalAddress)}</strong>.
               </p>
               """
            : string.Empty;
}
