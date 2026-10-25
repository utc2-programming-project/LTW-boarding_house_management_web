using Serilog.Core;
using Serilog.Events;

namespace Web.Infrastructure.Logging;

/// <summary>
/// Bộ lọc log: che các thuộc tính có tên thuộc danh sách nhạy cảm trước khi ghi log.
/// Đây là lớp phòng ngừa — mã nghiệp vụ vẫn KHÔNG được ghi thẳng secret vào thông điệp log.
/// </summary>
/// <remarks>
/// Chỉ xử lý được thuộc tính có cấu trúc (ví dụ <c>{Password}</c>); chuỗi nội suy sẵn không che được
/// nên quy tắc là: không nội suy dữ liệu nhạy cảm vào thông điệp.
/// </remarks>
public sealed class SensitiveDataScrubber : ILogEventEnricher
{
    private const string Mask = "***";

    private static readonly HashSet<string> BannedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordhash",
        "hashedpassword",
        "token",
        "accesstoken",
        "refreshtoken",
        "authorization",
        "cookie",
        "setcookie",
        "idnumber",
        "cccd",
        "citizenid",
        "connectionstring",
        "apikey",
        "secret",
        "clientsecret",
        "securitystamp",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            if (!BannedNames.Contains(property.Key))
            {
                continue;
            }

            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(property.Key, Mask));
        }
    }
}
