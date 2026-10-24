using Serilog.Core;
using Serilog.Events;

namespace Web.Infrastructure.Logging;

/// <summary>
/// Thêm thuộc tính <c>SafeException</c> cho mọi log event có exception.
/// Output template của sink không dùng <c>{Exception}</c> nữa mà dùng <c>{SafeException}</c>,
/// nhờ vậy log không ghi raw stack trace/message (có thể chứa SQL, connection string, đường dẫn máy).
/// </summary>
public sealed class SafeExceptionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var value = logEvent.Exception is null
            ? string.Empty
            : LogSanitizer.Describe(logEvent.Exception);

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SafeException", value));
    }
}
