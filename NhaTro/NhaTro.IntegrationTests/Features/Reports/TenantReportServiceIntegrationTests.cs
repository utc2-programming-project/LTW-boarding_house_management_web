using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Accounts;
using Web.Features.Reports;
using Web.Features.Rooms;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.Entities;

namespace NhaTro.IntegrationTests.Features.Reports;

public sealed class TenantReportServiceIntegrationTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task Owner_summary_matches_P3_even_when_toggles_deny()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "tenant-report-user", Roles.KhachThue);
        db.Tenants.AddRange(
            new Tenant { FullName = "Active", Phone = "0901000001", UserId = "tenant-report-user" },
            new Tenant { FullName = "Inactive", Phone = "0901000002", IsActive = false },
            new Tenant { FullName = "Deleted", Phone = "0901000003", IsDeleted = true });
        await db.SaveChangesAsync();
        var policy = new Policy(RoomChangeDecision.Denied);
        var tenants = new TenantService(fixture, TimeProvider.System, policy);
        var caller = new TenantCaller("owner-report", Roles.ChuTro);
        var expected = await tenants.GetSummaryAsync(caller);
        var actual = await new TenantReportService(tenants, policy).GetAsync(caller);
        Assert.Equal(expected.TotalCount, actual.Total);
        Assert.Equal(expected.ActiveCount, actual.Active);
        Assert.Equal(expected.LinkedAccountCount, actual.LinkedAccountCount);
        Assert.Equal(2, actual.Total);
        Assert.Equal(1, actual.Active);
        Assert.Equal(1, actual.LinkedAccountCount);
        Assert.Empty(policy.Codes);
    }

    [Fact]
    public async Task Allowed_manager_has_no_system_wide_numbers()
    {
        var policy = new Policy(RoomChangeDecision.Allowed);
        var result = await new TenantReportService(null!, policy).GetAsync(new("manager", Roles.QuanLy));
        Assert.Null(result.Total); Assert.Null(result.Active); Assert.Null(result.LinkedAccountCount);
        Assert.Equal(new[] { "SW-15", "SW-15b" }, policy.Codes);
    }

    [Theory]
    [InlineData("SW-15", RoomChangeDecision.Denied)]
    [InlineData("SW-15b", RoomChangeDecision.Denied)]
    [InlineData("SW-15", RoomChangeDecision.RequiresApproval)]
    [InlineData("SW-15b", RoomChangeDecision.RequiresApproval)]
    public async Task Manager_fails_closed(string blocked, RoomChangeDecision decision)
    {
        var policy = new Policy(decision, blocked);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new TenantReportService(null!, policy).GetAsync(new("manager", Roles.QuanLy)));
        Assert.Contains(blocked, policy.Codes);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.KhachThue)]
    public async Task Other_roles_are_denied(string role) =>
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new TenantReportService(null!, null!).GetAsync(new("user", role)));

    private sealed class Policy(RoomChangeDecision decision, string? blocked = null) : IManagerTogglePolicy
    {
        public List<string> Codes { get; } = [];
        public Task<RoomChangeDecision> EvaluateAsync(ManagerAction action, string role, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RoomChangeDecision> EvaluateAsync(string code, string role, CancellationToken ct = default)
        {
            Codes.Add(code);
            return Task.FromResult(blocked is null || blocked == code ? decision : RoomChangeDecision.Allowed);
        }
    }
}
