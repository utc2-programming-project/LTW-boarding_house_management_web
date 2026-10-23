using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Web.Infrastructure.Logging;

namespace NhaTro.UnitTests.Infrastructure.Logging;

/// <summary>Bộ lọc log che thuộc tính nhạy cảm, giữ nguyên thuộc tính bình thường.</summary>
public sealed class SensitiveDataScrubberTests
{
    [Fact]
    public void Che_cac_thuoc_tinh_nhay_cam_va_giu_thuoc_tinh_khac()
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplateParser().Parse("Dang nhap {Email} voi {Password} va {IdNumber}"),
            new[]
            {
                new LogEventProperty("Email", new ScalarValue("a@b.local")),
                new LogEventProperty("Password", new ScalarValue("Secret123!")),
                new LogEventProperty("IdNumber", new ScalarValue("079201000001")),
            });

        new SensitiveDataScrubber().Enrich(logEvent, new TestPropertyFactory());

        Assert.Equal("***", ((ScalarValue)logEvent.Properties["Password"]).Value);
        Assert.Equal("***", ((ScalarValue)logEvent.Properties["IdNumber"]).Value);
        Assert.Equal("a@b.local", ((ScalarValue)logEvent.Properties["Email"]).Value);
    }

    [Fact]
    public void Khong_dong_vao_thong_diep_khong_co_thuoc_tinh_nhay_cam()
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplateParser().Parse("Da xu ly {Count} tep"),
            new[] { new LogEventProperty("Count", new ScalarValue(3)) });

        new SensitiveDataScrubber().Enrich(logEvent, new TestPropertyFactory());

        Assert.Equal(3, ((ScalarValue)logEvent.Properties["Count"]).Value);
    }

    private sealed class TestPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
