using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Services;

namespace NhaTro.IntegrationTests.Features.Tenants;

public sealed class TenantApprovalApplierIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Số giấy tờ trong payload duyệt được mã hoá; applier phải giải mã rồi mới ghi hồ sơ.</summary>
    [SqlServerFact]
    public async Task Apply_Decrypts_Protected_IdNumber()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var provider = new EphemeralDataProtectionProvider();
        const string idNumber = "079201000123";

        var request = NewRequest(new TenantApprovalPayload
        {
            Action = TenantApprovalActions.Create,
            FullName = "Nguyễn Văn An",
            PhoneNumber = "0901234567",
            IdNumber = TenantApprovalPayloadCodec.Protect(provider, idNumber),
        });

        await new TenantCreateApprovalApplier(TimeProvider.System, provider).ApplyAsync(request, db);
        await db.SaveChangesAsync();

        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(item => item.CitizenId == idNumber);
        Assert.Equal("Nguyễn Văn An", tenant.FullName);
    }

    /// <summary>Ngày cấp trong tương lai bị từ chối khi duyệt (khác luồng lưu trực tiếp).</summary>
    [SqlServerFact]
    public async Task Apply_Rejects_Future_IdIssuedDate()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var provider = new EphemeralDataProtectionProvider();

        var request = NewRequest(new TenantApprovalPayload
        {
            Action = TenantApprovalActions.Create,
            FullName = "Nguyễn Văn Bình",
            PhoneNumber = "0901234568",
            IdIssuedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
        });

        await Assert.ThrowsAsync<ApprovalApplyException>(() =>
            new TenantCreateApprovalApplier(TimeProvider.System, provider).ApplyAsync(request, db));
        Assert.Empty(await db.Tenants.IgnoreQueryFilters().ToListAsync());
    }

    private static ManagerApprovalRequest NewRequest(TenantApprovalPayload payload) => new()
    {
        OperationCode = "SW-04",
        TargetType = ApprovalTargetType.Tenant,
        TargetKey = "new-tenant-test",
        PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions),
        ExpectedToken = new byte[8],
        Status = ApprovalRequestStatus.Pending,
        RequestedByUserId = "quan-ly",
        RequestedAt = DateTime.UtcNow,
    };
}
