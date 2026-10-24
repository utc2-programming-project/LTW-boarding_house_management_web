using System.Text;
using System.Text.RegularExpressions;

namespace Web.Infrastructure.Logging;

/// <summary>
/// Làm sạch chuỗi trước khi ghi log: che connection string/secret và đường dẫn tuyệt đối.
/// Dùng cho nội dung exception — phần mà <see cref="SensitiveDataScrubber"/> không chạm tới
/// (Serilog giữ exception ngoài Properties nên enricher không sửa trực tiếp được).
/// </summary>
public static class LogSanitizer
{
    public const string Mask = "***";
    public const string PathMask = "<path>";

    private const int MaxLength = 8_000;

    // Password=..., User Id=..., Token=... trong message exception / connection string.
    private static readonly Regex SecretAssignment = new(
        @"(?i)\b(password|pwd|user\s*id|uid|secret|token|api[_-]?key|account\s*key)\s*=\s*[^;,\r\n]*",
        RegexOptions.Compiled);

    // Server=..., Database=..., Data Source=... — thông tin hạ tầng nội bộ.
    private static readonly Regex EndpointAssignment = new(
        @"(?i)\b(server|data\s*source|initial\s*catalog|database|connection\s*string)\s*=\s*[^;,\r\n]*",
        RegexOptions.Compiled);

    // D:\duong\dan\file.cs (dừng ở dấu ':' của ":line" hoặc dấu nháy).
    private static readonly Regex WindowsPath = new(
        @"(?i)\b[A-Za-z]:\\[^\r\n'""<>|:*?]*",
        RegexOptions.Compiled);

    // \\may\share\...
    private static readonly Regex UncPath = new(
        @"\\\\[^\s'""\\]+(?:\\[^\r\n'""]*)?",
        RegexOptions.Compiled);

    // /home/..., /var/..., /usr/... trên Linux/macOS.
    private static readonly Regex UnixPath = new(
        @"(?<![\w:])\/(?:home|root|var|usr|etc|opt|srv|tmp)\/[^\s'""]+",
        RegexOptions.Compiled);

    /// <summary>Mô tả exception an toàn: loại + message + stack trace đã che dữ liệu nhạy cảm.</summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder();
        var current = exception;
        var depth = 0;

        while (current is not null && depth < 5)
        {
            if (depth > 0)
            {
                builder.Append(" ---> ");
            }

            builder.Append(current.GetType().FullName);

            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                builder.Append(": ").Append(current.Message);
            }

            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                builder.AppendLine(current.StackTrace);
            }

            current = current.InnerException;
            depth++;
        }

        var text = Sanitize(builder.ToString());
        return text.Length <= MaxLength ? text : string.Concat(text.AsSpan(0, MaxLength), "…");
    }

    /// <summary>Che secret/connection string và đường dẫn tuyệt đối trong chuỗi bất kỳ.</summary>
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = SecretAssignment.Replace(text, "$1=" + Mask);
        result = EndpointAssignment.Replace(result, "$1=" + Mask);
        result = WindowsPath.Replace(result, PathMask);
        result = UncPath.Replace(result, PathMask);
        result = UnixPath.Replace(result, PathMask);
        return result;
    }
}
