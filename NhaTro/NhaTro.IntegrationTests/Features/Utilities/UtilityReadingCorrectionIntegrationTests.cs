using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Data.Migrations;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;
using ReadingException = Web.Features.Utilities.Services.UtilityReadingException;

namespace NhaTro.IntegrationTests.Features.Utilities;

public sealed class UtilityReadingCorrectionIntegrationTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task AdjustAsync_LatestUninvoiced_WritesOneHistoryRow()
    {
        var (db, reading, owner) = await SeedAsync();
        await using (db)
        {
            var service = NewService(db);
            var result = await service.AdjustAsync(reading.Id, new AdjustUtilityReadingRequest(150m, 15m, "Kiểm tra"), owner);
            var audit = await db.UtilityReadingAudits.SingleAsync();
            Assert.Equal("Kiểm tra", audit.Reason);
            Assert.Equal(100m, audit.OldElectricEnd);
            Assert.Equal(150m, audit.NewElectricEnd);
            Assert.Equal(10m, audit.OldWaterEnd);
            Assert.Equal(15m, audit.NewWaterEnd);
            Assert.Equal(150m, result.ElectricEnd);
            Assert.Equal(15m, result.WaterEnd);
        }
    }

    [SqlServerFact]
    public async Task AdjustAsync_OlderPeriod_IsNotAdjustableAndWritesNoHistory()
    {
        var (db, reading, owner) = await SeedAsync();
        await using (db)
        {
            db.UtilityReadings.Add(new UtilityReading { RoomId = reading.RoomId, PeriodStart = new DateOnly(2026, 11, 1), PeriodEnd = new DateOnly(2026, 11, 30), CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<ReadingException>(() =>
                NewService(db).AdjustAsync(reading.Id, new AdjustUtilityReadingRequest(150m, 15m, "Lý do"), owner));
            Assert.Equal(UtilityReadingErrorCodes.NotAdjustable, error.ErrorCode);
            Assert.Equal(0, await db.UtilityReadingAudits.CountAsync());
        }
    }

    [SqlServerFact]
    public async Task AdjustAsync_WithoutReason_WritesNoHistory()
    {
        var (db, reading, owner) = await SeedAsync();
        await using (db)
        {
            var error = await Assert.ThrowsAsync<ReadingException>(() =>
                NewService(db).AdjustAsync(reading.Id, new AdjustUtilityReadingRequest(150m, 15m, " "), owner));
            Assert.Equal(UtilityReadingErrorCodes.AdjustmentReasonRequired, error.ErrorCode);
            Assert.Equal(0, await db.UtilityReadingAudits.CountAsync());
        }
    }

    [SqlServerFact]
    public async Task AuditRows_UpdateAndDelete_AreRejectedByTrigger()
    {
        var (db, reading, owner) = await SeedAsync();
        await using (db)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(UtilityReadingAuditImmutabilitySql.CreateOrAlter);
                await NewService(db).AdjustAsync(reading.Id, new AdjustUtilityReadingRequest(150m, 15m, "Lý do"), owner);
                var id = await db.UtilityReadingAudits.Select(item => item.Id).SingleAsync();
                await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlAsync($"UPDATE dbo.UtilityReadingAudits SET Reason = N'x' WHERE Id = {id}"));
                await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlAsync($"DELETE FROM dbo.UtilityReadingAudits WHERE Id = {id}"));
            }
            finally { await db.Database.ExecuteSqlRawAsync(UtilityReadingAuditImmutabilitySql.Drop); }
        }
    }

    private async Task<(ApplicationDbContext Db, UtilityReading Reading, PropertyCaller Owner)> SeedAsync()
    {
        await fixture.ResetAsync();
        var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);
        var reading = new UtilityReading { RoomId = ids.RoomAId, PeriodStart = new DateOnly(2026, 10, 1), PeriodEnd = new DateOnly(2026, 10, 31), ElectricEnd = 100m, WaterEnd = 10m, CreatedAt = DateTimeOffset.UtcNow };
        db.UtilityReadings.Add(reading);
        await db.SaveChangesAsync();
        return (db, reading, new PropertyCaller(ids.ChuTroAId, Roles.ChuTro));
    }

    private static UtilityReadingService NewService(ApplicationDbContext db)
    {
        var tariffs = new UtilityTariffService(db);
        return new UtilityReadingService(db, TimeProvider.System, new UtilityPeriodCostSnapshotService(tariffs));
    }
}
