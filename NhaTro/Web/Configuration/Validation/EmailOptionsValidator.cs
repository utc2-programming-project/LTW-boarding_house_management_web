using System.Net.Mail;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;

namespace Web.Configuration.Validation;

/// <summary>
/// Kiểm tra cấu hình "Email" ngay khi khởi động để lỗi hiện sớm, thay vì chỉ vỡ lúc gửi thư.
/// Không bắt buộc phải có mật khẩu: chưa cấu hình thì hệ thống lùi về chế độ ghi log.
/// </summary>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var failures = new List<string>();

        if (options.DemoMode && string.IsNullOrWhiteSpace(options.FallbackRecipient))
        {
            failures.Add("Email:DemoMode = true nhưng Email:FallbackRecipient đang trống.");
        }

        if (!string.IsNullOrWhiteSpace(options.FallbackRecipient) && !IsEmail(options.FallbackRecipient))
        {
            failures.Add($"Email:FallbackRecipient không phải địa chỉ hợp lệ: {options.FallbackRecipient}");
        }

        foreach (var (key, value) in options.RecipientOverrides)
        {
            if (string.IsNullOrWhiteSpace(key) || !IsEmail(value))
            {
                failures.Add($"Email:RecipientOverrides có mục không hợp lệ: '{key}' -> '{value}'");
            }
        }

        var smtp = options.Smtp;
        if (smtp.Port is <= 0 or > 65535)
        {
            failures.Add($"Email:Smtp:Port không hợp lệ: {smtp.Port}");
        }
        else if (smtp.Port == 465)
        {
            failures.Add("Email:Smtp:Port = 465 dùng SSL ngầm, System.Net.Mail không hỗ trợ; dùng 587 + STARTTLS.");
        }

        if (smtp.TimeoutSeconds <= 0)
        {
            failures.Add($"Email:Smtp:TimeoutSeconds phải lớn hơn 0: {smtp.TimeoutSeconds}");
        }

        if (!string.IsNullOrWhiteSpace(smtp.UserName) && !IsEmail(smtp.UserName))
        {
            failures.Add($"Email:Smtp:UserName không phải địa chỉ hợp lệ: {smtp.UserName}");
        }

        if (!string.IsNullOrWhiteSpace(smtp.FromAddress) && !IsEmail(smtp.FromAddress))
        {
            failures.Add($"Email:Smtp:FromAddress không phải địa chỉ hợp lệ: {smtp.FromAddress}");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return MailAddress.TryCreate(value.Trim(), out var parsed)
            && string.Equals(parsed.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
