using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Features.PermissionMatrix;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Audit.Entities;
using Web.Features.Properties.DTOs;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;

namespace NhaTro.IntegrationTests.Features.TestReadiness;

/// <summary>
/// VHT-48 3A — quy tắc giữ lịch sử: audit sống độc lập, FK Restrict chặn xóa dây chuyền,
/// bảng giá giữ bản cũ, yêu cầu duyệt còn dòng sau khi quyết định.
/// </summary>
public sealed class HistoryRetentionTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private async Task<PermissionMatrixWorld> NewWorldAsync()
    {
        var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();
        return world;
    }

    [SqlServerFact]
    public async Task AuditLog_ton_tai_sau_khi_xoa_nha()
    {
        await using var world = await NewWorldAsync();
        var property = await world.NewProperties().CreateAsync(
            new PropertySave { Name = "Nhà kiểm lịch sử VHT-48" }, world.OwnerA);
        Assert.True(property.Succeeded, property.ErrorCode);
        var propertyId = property.Property!.Id;

        await world.NewAuditLogs().RecordAsync(
            AuditLogCategory.Business, "PropertyCreated", PermissionMatrixWorld.OwnerAId, propertyId, "VHT-48");
        await using (var db = world.NewDb())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Properties WHERE Id = {propertyId}");
        }

        await using var verify = world.NewDb();
        Assert.True(await verify.Set<AuditLogEntry>().AsNoTracking()
            .AnyAsync(entry => entry.PropertyId == propertyId && entry.Action == "PropertyCreated"));
    }

    [SqlServerFact]
    public async Task Khong_xoa_duoc_chi_so_khi_con_lich_su()
    {
        await using var world = await NewWorldAsync();
        int readingId;
        await using (var db = world.NewDb())
        {
            readingId = await db.UtilityReadings.AsNoTracking().Select(item => item.Id).FirstAsync();
            db.Set<UtilityReadingAudit>().Add(new UtilityReadingAudit
            {
                UtilityReadingId = readingId,
                OldElectricEnd = 120m,
                NewElectricEnd = 130m,
                OldWaterEnd = 60m,
                NewWaterEnd = 65m,
                Reason = "Điều chỉnh kiểm lịch sử",
                ChangedByUserId = PermissionMatrixWorld.OwnerAId,
                ChangedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            var exception = await Assert.ThrowsAsync<SqlException>(
                () => db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM UtilityReadings WHERE Id = {readingId}"));
            Assert.Contains(
                "FK_UtilityReadingAudits_UtilityReadings_UtilityReadingId",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = world.NewDb();
        Assert.True(await verify.Set<UtilityReadingAudit>().AsNoTracking()
            .AnyAsync(audit => audit.UtilityReadingId == readingId));
    }

    [SqlServerFact]
    public async Task Khong_xoa_duoc_hoa_don_khi_con_Payment()
    {
        await using var world = await NewWorldAsync();
        await using var db = world.NewDb();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Invoices (ContractId, PeriodStart, PeriodEnd, Status, TotalAmount, CreatedAt)
            VALUES ({world.Data.ContractAId}, '2030-01-01', '2030-01-31', 1, 1000, SYSDATETIMEOFFSET())
            """);
        var invoiceId = await db.Invoices.AsNoTracking()
            .Where(item => item.ContractId == world.Data.ContractAId)
            .OrderByDescending(item => item.Id)
            .Select(item => item.Id)
            .FirstAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Payments
                (InvoiceId, Amount, TransferredAt, Status, RequestedAt, RequestedByUserId)
            VALUES
                ({invoiceId}, 1000, SYSDATETIMEOFFSET(), 0, SYSDATETIMEOFFSET(), {PermissionMatrixWorld.TenantUserAId})
            """);

        var exception = await Assert.ThrowsAsync<SqlException>(
            () => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Invoices WHERE Id = {invoiceId}"));
        Assert.Contains("FK_Payments_Invoices_InvoiceId", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact]
    public async Task Bang_gia_giu_ban_cu_khi_doi_gia()
    {
        await using var world = await NewWorldAsync();
        int before;
        await using (var db = world.NewDb())
        {
            before = await db.ServicePriceHistories.AsNoTracking()
                .CountAsync(version => version.ServiceCatalogId == world.Data.ServiceCatalogId);
        }

        var updated = await world.NewServiceCatalog().CreateOrUpdateServicePriceAsync(
            world.Data.ServiceCatalogId,
            new UpdateServicePriceDto
            {
                Name = "Internet",
                Unit = "tháng",
                CalculationMethod = "FixedMonthly",
                UnitPrice = 200_000m,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
            },
            world.OwnerA);
        Assert.NotNull(updated);

        await using var verify = world.NewDb();
        var versions = await verify.ServicePriceHistories.AsNoTracking()
            .Where(version => version.ServiceCatalogId == world.Data.ServiceCatalogId)
            .OrderBy(version => version.EffectiveDate)
            .Select(version => version.UnitPrice)
            .ToListAsync();
        Assert.Equal(before + 1, versions.Count);
        Assert.Contains(150_000m, versions);
        Assert.Contains(200_000m, versions);
    }

    [SqlServerFact]
    public async Task Yeu_cau_duyet_con_dong_sau_khi_tu_choi()
    {
        await using var world = await NewWorldAsync();
        var approvals = world.NewApprovals();
        var created = await approvals.CreateAsync(new CreateApprovalRequest
        {
            OperationCode = "SW-10",
            TargetType = ApprovalTargetType.Contract,
            TargetKey = world.Data.ContractAId.ToString(),
            PropertyId = world.Data.PropertyAId,
            PayloadJson = "{}",
        }, world.ManagerApproval);
        Assert.True(created.Succeeded, created.ErrorCode);

        var rejected = await approvals.RejectAsync(
            created.Request!.Id,
            new RejectApprovalRequest { Reason = "Không đồng ý kiểm lịch sử" },
            world.OwnerAApproval);
        Assert.True(rejected.Succeeded, rejected.ErrorCode);

        await using var verify = world.NewDb();
        var row = await verify.ManagerApprovalRequests.AsNoTracking()
            .SingleAsync(request => request.Id == created.Request.Id);
        Assert.Equal(ApprovalRequestStatus.Rejected, row.Status);
        Assert.Equal("Không đồng ý kiểm lịch sử", row.DecisionReason);
    }
}
