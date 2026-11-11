using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Features.PermissionMatrix;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Services;

namespace NhaTro.IntegrationTests.Features.Utilities;

public sealed class MonthlyUtilityCalculationPageServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task LoadAsync_ScopesRowsByRole_AndCalculatesFromSavedReading()
    {
        await fixture.ResetAsync();
        await using var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();

        var today = DateOnly.FromDateTime(world.Data.Today);
        var periodEnd = today.AddMonths(1).AddDays(-1);
        var service = CreateService(world, world.Manager);
        var saved = await service.SaveAsync(
            new MonthlyUtilitySaveRequest(
                world.Data.RoomAId,
                world.Data.ContractAId,
                ReadingId: null,
                today,
                periodEnd,
                150m,
                70m,
                Reason: null));
        Assert.True(saved.Success, saved.ErrorCode);

        var managerData = await service.LoadAsync(null, today, periodEnd);
        var managerRoom = Assert.Single(managerData.Rooms);
        Assert.Equal(world.Data.RoomAId, managerRoom.RoomId);
        Assert.Equal(world.Data.ContractAId, managerRoom.ContractId);
        Assert.True(managerRoom.HasReadings);
        Assert.Equal(120m, managerRoom.ElectricStart);
        Assert.Equal(150m, managerRoom.ElectricEnd);
        Assert.Equal(30m, managerRoom.ElectricUsage);
        Assert.Equal(60m, managerRoom.WaterStart);
        Assert.Equal(70m, managerRoom.WaterEnd);
        Assert.Equal(10m, managerRoom.WaterUsage);
        Assert.Contains(managerRoom.ServiceLines, line => line.Name == "Internet");
        Assert.Equal(world.Data.InvoiceDraftId, managerRoom.InvoiceId);
        Assert.False(managerRoom.IsInvoiced);
        Assert.False(managerRoom.CanCorrectPrevious);

        await using (var db = world.NewDb())
        {
            var invoiceTotal = await db.Invoices
                .Where(invoice => invoice.Id == world.Data.InvoiceDraftId)
                .Select(invoice => invoice.TotalAmount)
                .SingleAsync();
            Assert.Equal(invoiceTotal, managerRoom.TotalAmount);
        }

        var tenantData = await CreateService(world, world.TenantAAsPropertyCaller).LoadAsync(null, today, periodEnd);
        Assert.Equal(world.Data.RoomAId, Assert.Single(tenantData.Rooms).RoomId);
        Assert.DoesNotContain(tenantData.Rooms, room => room.RoomId == world.Data.RoomBId);

        var ownerBData = await CreateService(world, world.OwnerB).LoadAsync(null, today, periodEnd);
        Assert.Equal(world.Data.RoomBId, Assert.Single(ownerBData.Rooms).RoomId);
        Assert.DoesNotContain(ownerBData.Rooms, room => room.RoomId == world.Data.RoomAId);
    }

    [SqlServerFact]
    public async Task CanCorrectPrevious_IsHiddenWhenPreviousPeriodAlreadyHasIssuedInvoice()
    {
        await fixture.ResetAsync();
        await using var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();

        var today = DateOnly.FromDateTime(world.Data.Today);
        var periodEnd = today.AddMonths(1).AddDays(-1);
        var previousStart = today.AddMonths(-1);
        var previousEnd = today.AddDays(-1);
        var service = CreateService(world, world.Manager);

        var before = await service.LoadAsync(null, today, periodEnd);
        var beforeRoom = Assert.Single(before.Rooms);
        Assert.NotNull(beforeRoom.PreviousReading);
        Assert.True(beforeRoom.CanCorrectPrevious);

        await using (var db = world.NewDb())
        {
            db.Invoices.Add(new Invoice
            {
                ContractId = world.Data.ContractAId,
                PeriodStart = previousStart,
                PeriodEnd = previousEnd,
                Status = InvoiceStatus.Issued,
                TotalAmount = 0m,
                CreatedAt = world.Data.Today,
                IssuedAt = world.Data.Today,
            });
            await db.SaveChangesAsync();
        }

        var after = await service.LoadAsync(null, today, periodEnd);
        var afterRoom = Assert.Single(after.Rooms);
        Assert.False(afterRoom.IsInvoiced);
        Assert.False(afterRoom.CanCorrectPrevious);
    }

    [SqlServerFact]
    public async Task CreateInvoiceAsync_CreatesDraftFromCurrentPeriod_AndTenantForbidden()
    {
        await fixture.ResetAsync();
        await using var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();

        var today = DateOnly.FromDateTime(world.Data.Today);
        var periodEnd = today.AddMonths(1).AddDays(-1);

        // World đã gieo sẵn hóa đơn nháp cho kỳ này; xóa để tạo mới không trùng.
        await using (var db = world.NewDb())
        {
            var seeded = await db.Invoices
                .Include(item => item.Items)
                .Where(item => item.ContractId == world.Data.ContractAId)
                .ToListAsync();
            db.InvoiceItems.RemoveRange(seeded.SelectMany(item => item.Items));
            db.Invoices.RemoveRange(seeded);
            await db.SaveChangesAsync();
        }

        // P5 chỉ lập hóa đơn khi phòng đã có chỉ số trong kỳ: ghi chỉ số kỳ hiện tại trước.
        await world.NewUtilityReadings().RecordAsync(
            new RecordUtilityReadingRequest(
                world.Data.RoomAId,
                today,
                periodEnd,
                ElectricEnd: 200m,
                WaterEnd: 90m),
            world.OwnerA);

        var managerService = CreateService(world, world.OwnerA);
        var created = await managerService.CreateInvoiceAsync(world.Data.ContractAId, today, periodEnd);
        Assert.True(created.Success, created.ErrorCode);
        Assert.NotNull(created.InvoiceId);

        await using (var db = world.NewDb())
        {
            var invoice = await db.Invoices.SingleAsync(item => item.Id == created.InvoiceId);
            Assert.Equal(Web.Features.Invoices.Enums.InvoiceStatus.Draft, invoice.Status);
            Assert.Equal(today, invoice.PeriodStart);
            Assert.True(invoice.TotalAmount > 0m);
        }

        var tenantService = CreateService(world, world.TenantAAsPropertyCaller);
        var forbidden = await tenantService.CreateInvoiceAsync(world.Data.ContractAId, today, periodEnd);
        Assert.False(forbidden.Success);
    }

    [SqlServerFact]
    public async Task ServiceRegistration_FiltersP5Lines_ToRegisteredOnly_AndTenantCanToggle()
    {
        await fixture.ResetAsync();
        await using var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();

        // Fixture dùng chung DB: dọn sạch đăng ký còn sót từ test chạy song song trước khi assert.
        await using (var resetDb = world.NewDb())
        {
            await resetDb.ServiceRegistrationRequests.ExecuteDeleteAsync();
            await resetDb.ContractServiceRegistrations.ExecuteDeleteAsync();
        }

        var today = DateOnly.FromDateTime(world.Data.Today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var periodEnd = today.AddMonths(1).AddDays(-1);
        var service = CreateService(world, world.Manager);

        int propertyId;
        int internetCatalogId;
        await using (var db = world.NewDb())
        {
            var catalog = await db.ServiceCatalogs.SingleAsync(item => item.PropertyId != null);
            propertyId = catalog.PropertyId!.Value;
            internetCatalogId = catalog.Id;
        }

        var second = await world.NewServiceCatalog().CreateOrUpdateServicePriceAsync(
            new CreateServiceCatalogDto
            {
                PropertyId = propertyId,
                Name = "Fitness",
                Unit = "người",
                CalculationMethod = "FixedMonthly",
                UnitPrice = 30_000m,
                EffectiveDate = monthStart,
            },
            world.OwnerA);

        var before = Assert.Single((await service.LoadAsync(null, monthStart, periodEnd)).Rooms);
        Assert.Equal(2, before.ServiceLines.Count);

        // Tenant registers only Fitness via toggle service.
        var registrationService = new ContractServiceRegistrationService(
            world.NewDb(),
            new P2CallerProvider(new FixedAuthenticationStateProvider(world.TenantAAsPropertyCaller)),
            NullLogger<ContractServiceRegistrationService>.Instance,
            TimeProvider.System);
        var registered = await registrationService.ToggleAsync(second.Id);
        Assert.True(registered);

        var after = Assert.Single((await service.LoadAsync(null, monthStart, periodEnd)).Rooms);
        var only = Assert.Single(after.ServiceLines);
        Assert.Equal("Fitness", only.Name);
        _ = internetCatalogId;

        // Tenant toggles back off; registrations empty again → all services applied.
        var registeredAgain = await registrationService.ToggleAsync(second.Id);
        Assert.False(registeredAgain);
        var reloaded = Assert.Single((await service.LoadAsync(null, monthStart, periodEnd)).Rooms);
        Assert.True(reloaded.ServiceLines.Count == 2,
            "Lines after untoggle: " + string.Join(", ", reloaded.ServiceLines.Select(line => line.Name)));
    }

    private static MonthlyUtilityCalculationPageService CreateService(
        PermissionMatrixWorld world,
        PropertyCaller caller)
    {
        var db = world.NewDb();
        var callerProvider = new P2CallerProvider(new FixedAuthenticationStateProvider(caller));
        var tariffs = new UtilityTariffService(db);
        var snapshots = new UtilityPeriodCostSnapshotService(tariffs);
        return new MonthlyUtilityCalculationPageService(
            db,
            new UtilityReadingService(db, TimeProvider.System, snapshots),
            snapshots,
            tariffs,
            callerProvider,
            world.NewInvoices(),
            new FixedLocalizer(),
            NullLogger<MonthlyUtilityCalculationPageService>.Instance);
    }

    private sealed class FixedLocalizer : IStringLocalizer<Web.Features.Utilities.UtilityResources>
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name] =>
            new(name, name, resourceNotFound: true);

        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments), resourceNotFound: true);

        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCulture) => [];
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

