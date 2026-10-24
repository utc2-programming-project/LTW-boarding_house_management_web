namespace Web.Infrastructure.Logging;

/// <summary>
/// Output template dùng chung cho các sink. Không có <c>{Exception}</c> — exception chỉ được
/// ghi qua <see cref="SafeExceptionEnricher"/> để tránh lộ dữ liệu nội bộ.
/// </summary>
public static class LogTemplates
{
    public const string Console = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{SafeException}";

    public const string File = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{SafeException}";
}
