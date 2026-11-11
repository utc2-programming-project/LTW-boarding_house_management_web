using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityReadingAdjustmentTests
{
    [Fact]
    public void Apply_WritesAuditWithProvidedTimestamp()
    {
        var changedAt = new DateTimeOffset(2026, 11, 11, 8, 30, 0, TimeSpan.Zero);
        var reading = new UtilityReading { Id = 7, ElectricEnd = 12m, WaterEnd = 4m };

        UtilityReadingService.Apply(reading, new AdjustUtilityReadingRequest(15m, 6m, "Kiểm tra lại"),
            "manager-1", changedAt);

        var audit = Assert.Single(reading.Audits);
        Assert.Equal(changedAt, audit.ChangedAt);
        Assert.Equal(changedAt, reading.UpdatedAt);
        Assert.Equal(12m, audit.OldElectricEnd);
        Assert.Equal(15m, audit.NewElectricEnd);
    }
}
