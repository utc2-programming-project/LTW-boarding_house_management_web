using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Web.Infrastructure.Logging;

namespace NhaTro.UnitTests.Infrastructure.Logging;

/// <summary>Exception khi ghi log phải được làm sạch (không lộ SQL/connection string/đường dẫn máy).</summary>
public sealed class SafeExceptionEnricherTests
{
    [Fact]
    public void Exception_duoc_lam_sach_truoc_khi_ghi_log()
    {
        var exception = new InvalidOperationException(
            "Cannot open database. Server=db.internal;Database=NhaTro;Password=Secret123;",
            new Exception("Loi tai D:\\du-an\\NhaTro\\Web\\Program.cs:line 42"));

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            exception,
            new MessageTemplateParser().Parse("Loi he thong"),
            Array.Empty<LogEventProperty>());

        new SafeExceptionEnricher().Enrich(logEvent, new TestPropertyFactory());

        var safe = (string)((ScalarValue)logEvent.Properties["SafeException"]).Value!;
        Assert.Contains(nameof(InvalidOperationException), safe);
        Assert.DoesNotContain("Secret123", safe);
        Assert.DoesNotContain("db.internal", safe);
        Assert.DoesNotContain("D:\\", safe);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new MessageTemplateTextFormatter(LogTemplates.File, CultureInfo.InvariantCulture)
            .Format(logEvent, writer);
        var output = writer.ToString();

        Assert.Contains(nameof(InvalidOperationException), output);
        Assert.DoesNotContain("Secret123", output);
        Assert.DoesNotContain("db.internal", output);
        Assert.DoesNotContain("D:\\", output);
    }

    private sealed class TestPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
