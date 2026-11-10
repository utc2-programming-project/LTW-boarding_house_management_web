using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Features.PermissionMatrix;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Invoices.Enums;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;

namespace NhaTro.IntegrationTests.Features.Utilities;

public sealed class ServiceRegistrationFlowIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task Request_Approve_Invoice_RejectsPendingAndTenantCannotApprove()
    {
        await fixture.ResetAsync();
        await using var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();

        var today = DateOnly.FromDateTime(world.Data.Today);
        var service = CreateService(world, world.TenantAAsPropertyCaller);

        var request = await service.RequestAsync(
            serviceCatalogId: await InternetCatalogIdAsync(world),
            note: "Đăng ký test");
        Assert.Equal(ServiceRegistrationStatus.PendingApproval, request.Status);

        // One registration per service per month: duplicate rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestAsync(
            request.ServiceCatalogId, note: null));

        // Tenant cannot approve.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(request.Id));

        var landlordService = CreateService(world, world.OwnerA);
        var approved = await landlordService.ApproveAsync(request.Id);
        Assert.Equal(ServiceRegistrationStatus.Approved, approved.Status);

        // Approved registration is applied to P5 via ContractServiceRegistration.
        await using (var db = world.NewDb())
        {
            Assert.True(await db.ContractServiceRegistrations.AnyAsync(item =>
                item.ContractId == world.Data.ContractAId && item.ServiceCatalogId == request.ServiceCatalogId));
        }

        var invoice = await landlordService.CreateInvoiceAsync(
            request.Id,
            amount: 150_000m,
            content: "Internet thang nay",
            periodStart: today.AddMonths(2),
            periodEnd: today.AddMonths(3).AddDays(-1),
            dueDate: today.AddMonths(2).AddDays(15));
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(today.AddMonths(2).AddDays(15), invoice.DueDate);
        Assert.Equal(request.Id, await RegistrationIdAsync(world, invoice.Id));

        // Idempotent: call again returns the same invoice.
        var again = await landlordService.CreateInvoiceAsync(
            request.Id, 150_000m, "Internet thang nay", today.AddMonths(2), today.AddMonths(3).AddDays(-1), today.AddMonths(2).AddDays(15));
        Assert.Equal(invoice.Id, again.Id);

        // Landlord list sees it; tenant history sees it.
        Assert.Contains(await landlordService.ListForLandlordAsync(), item => item.Id == request.Id);
        var mine = await service.ListMineAsync();
        Assert.Contains(mine, item => item.Id == request.Id && item.InvoiceId == invoice.Id);

        // Register notification was written for the tenant.
        await using (var db = world.NewDb())
        {
            Assert.True(await db.AppNotifications.AnyAsync(item => item.RecipientUserId == world.TenantAAsPropertyCaller.UserId));
        }
    }

    [SqlServerFact]
    public async Task Reject_RequiresReason_AndBlocksInvoiceCreation()
    {
        await fixture.ResetAsync();
        await using var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();

        var today = DateOnly.FromDateTime(world.Data.Today);
        var service = CreateService(world, world.TenantAAsPropertyCaller);
        var request = await service.RequestAsync(await InternetCatalogIdAsync(world), note: null);

        var landlordService = CreateService(world, world.OwnerA);
        await Assert.ThrowsAsync<InvalidOperationException>(() => landlordService.RejectAsync(request.Id, "  "));

        var rejected = await landlordService.RejectAsync(request.Id, "Đã đầy phòng");
        Assert.Equal(ServiceRegistrationStatus.Rejected, rejected.Status);
        Assert.Equal("Đã đầy phòng", rejected.RejectReason);

        await Assert.ThrowsAsync<InvalidOperationException>(() => landlordService.CreateInvoiceAsync(
            request.Id, 150_000m, "x", today, today.AddMonths(1).AddDays(-1), today.AddDays(15)));
    }

    [SqlServerFact]
    public async Task Cancel_PendingFree_ApprovedNeedsApproval_RejectKeepsPayment()
    {
        await fixture.ResetAsync();
        await using var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();

        var tenantService = CreateService(world, world.TenantAAsPropertyCaller);
        var landlordService = CreateService(world, world.OwnerA);
        var catalogId = await InternetCatalogIdAsync(world);

        // Pending: tenant cancels directly.
        var first = await tenantService.RequestAsync(catalogId, note: null);
        var cancelledPending = await tenantService.CancelAsync(first.Id, reason: null);
        Assert.Equal(ServiceRegistrationStatus.Cancelled, cancelledPending.Status);

        // New request (cancelled doesn't block new monthly request), approve, then cancel with reason.
        var second = await tenantService.RequestAsync(catalogId, note: null);
        var approved = await landlordService.ApproveAsync(second.Id);
        Assert.Equal(ServiceRegistrationStatus.Approved, approved.Status);

        // Mỗi lần tải trang là một DbContext mới: dùng service mới sau khi chủ trọ thao tác.
        var tenantService2 = CreateService(world, world.TenantAAsPropertyCaller);
        var cancelRequested = await tenantService2.CancelAsync(second.Id, reason: "Không có nhu cầu");
        Assert.Equal(ServiceRegistrationCancelStatus.Pending, cancelRequested.CancelStatus);

        // Landlord rejects cancel → tenant still has approved registration.
        var cancelRejected = await landlordService.RejectCancelAsync(second.Id, "Đã dùng trong tháng");
        Assert.Equal(ServiceRegistrationStatus.Approved, cancelRejected.Status);
        Assert.Equal(ServiceRegistrationCancelStatus.Rejected, cancelRejected.CancelStatus);

        // Tenant retries; landlord approves → cancelled and removed from P5 registrations.
        // (Mỗi lần tải trang là một DbContext mới; mô phỏng bằng service mới.)
        var tenantService3 = CreateService(world, world.TenantAAsPropertyCaller);
        await tenantService3.CancelAsync(second.Id, reason: "Đổi plan");
        var cancelApproved = await landlordService.ApproveCancelAsync(second.Id);
        Assert.Equal(ServiceRegistrationStatus.Cancelled, cancelApproved.Status);
        await using (var db = world.NewDb())
        {
            Assert.False(await db.ContractServiceRegistrations.AnyAsync(item =>
                item.ContractId == world.Data.ContractAId && item.ServiceCatalogId == catalogId));
        }
    }

    private static async Task<int> InternetCatalogIdAsync(PermissionMatrixWorld world)
    {
        await using var db = world.NewDb();
        return await db.ServiceCatalogs
            .Where(item => item.PropertyId != null)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .FirstAsync();
    }

    private static async Task<int?> RegistrationIdAsync(PermissionMatrixWorld world, int invoiceId)
    {
        await using var db = world.NewDb();
        return await db.Invoices
            .Where(item => item.Id == invoiceId)
            .Select(item => item.RegistrationId)
            .SingleAsync();
    }

    private static ServiceRegistrationService CreateService(
        PermissionMatrixWorld world,
        PropertyCaller caller)
    {
        var db = world.NewDb();
        var callerProvider = new P2CallerProvider(new FixedAuthenticationStateProvider(caller));
        return new ServiceRegistrationService(
            db,
            callerProvider,
            world.NewInvoices(),
            world.NewInvoiceQuery(),
            new NotificationService(db),
            NullLogger<ServiceRegistrationService>.Instance);
    }

    private sealed class FixedAuthenticationStateProvider(PropertyCaller caller) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, caller.UserId),
                    new Claim(ClaimTypes.Role, caller.Role),
                ],
                authenticationType: "IntegrationTest");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
