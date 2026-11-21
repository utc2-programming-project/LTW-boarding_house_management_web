using System.Text.RegularExpressions;

namespace Web.Features.Assistant.Rules;

/// <summary>
/// Che PII trước khi gửi ngữ cảnh ra model: số CCCD/CMND (9–12 chữ số) chỉ giữ 3 số cuối,
/// số tài khoản đầy đủ bị thay bằng dấu '*'. Số tiền/ngày không khớp mẫu này nên giữ nguyên.
/// Số điện thoại (đầu 03/05/07/08/09 + 8 số) và email được giữ để AI soạn nháp liên hệ/khách thuê;
/// đây là dữ liệu chủ trọ chủ động nhập cho mục đích tạo hồ sơ.
/// Lớp thứ hai: khóa JSON nhạy cảm nếu lọt vào ngữ cảnh cũng bị che.
/// </summary>
public static partial class AiPiiMasker
{
    public static string Mask(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        var masked = JsonSensitiveField().Replace(json, match =>
            $"\"{match.Groups[1].Value}\":\"***\"");

        // CCCD/CMND: giữ 3 số cuối để chủ trọ vẫn đối chiếu được nếu cần.
        masked = LongDigitRun().Replace(masked, match =>
        {
            var digits = match.Value;
            return digits.Length is >= 9 and <= 12
                ? new string('*', digits.Length - 3) + digits[^3..]
                : digits;
        });

        return masked;
    }

    [GeneratedRegex("\"(citizenId|idNumber|bankAccountNumber|bankAccountName)\"\\s*:\\s*\"[^\"]*\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonSensitiveField();

    // Bỏ qua số điện thoại di động Việt Nam (0 + 3/5/7/8/9 + 8 số) để giữ liên hệ khách thuê.
    [GeneratedRegex("(?<![\\d\\-:.])(?!0[35789]\\d{8}(?!\\d))\\d{9,12}(?![\\d\\-:.])",
        RegexOptions.CultureInvariant)]
    private static partial Regex LongDigitRun();
}
