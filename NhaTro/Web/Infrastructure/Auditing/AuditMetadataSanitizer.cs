using System.Text.RegularExpressions;

namespace Web.Infrastructure.Auditing;

/// <summary>
/// Lọc dữ liệu nhạy cảm trước khi ghi nhật ký thao tác ra file (VHT-104).
/// Metadata chỉ là mô tả ngắn; lớp này là chốt chặn cuối, mã nghiệp vụ vẫn không được chủ động ghi secret.
/// </summary>
public static partial class AuditMetadataSanitizer
{
    public const int MaxLength = 1000;
    private const string Mask = "***";

    public static string? Sanitize(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        // Mask token trước: nếu chạy sau, "Authorization: Bearer xyz" bị regex gán nhãn cắt mất "Bearer"
        // và token thật còn sót lại.
        var cleaned = BearerToken().Replace(metadata, Mask);
        cleaned = SensitiveAssignment().Replace(cleaned, match => $"{match.Groups[1].Value}={Mask}");
        cleaned = CitizenId().Replace(cleaned, Mask);

        return cleaned.Length <= MaxLength ? cleaned : cleaned[..MaxLength];
    }

    [GeneratedRegex(
        @"(?i)\b(password|passwordhash|hashedpassword|token|access_token|refresh_token|authorization|cookie|idnumber|id_number|cccd|citizenid|citizen_id|secret|apikey|api_key)\b\s*[:=]\s*[^,;\s]+")]
    private static partial Regex SensitiveAssignment();

    [GeneratedRegex(@"\b\d{12}\b")]
    private static partial Regex CitizenId();

    [GeneratedRegex(@"(?i)\bbearer\s+[a-z0-9._\-]+")]
    private static partial Regex BearerToken();
}
