using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Web.Common.Models;
using Web.Data;
using Web.Components.Layout;
using Web.Components.Shared.Feedback;
using Web.Features.Accounts;
using Web.Features.Dashboard.Components;
using Web.Features.Dashboard.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Pages;
using Web.Features.Reports.Services;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants;

namespace NhaTro.UnitTests.Components;

public sealed class BusinessReportsPageTests : BunitContext
{
    private readonly List<RoomReportFilter> RoomRequests = [];
    private readonly List<OccupancyFilter> OccupancyRequests = [];
    private readonly List<TenantReportFilter> TenantRequests = [];
    private readonly List<string> CallerRoles = [];
    private bool DenyRooms;
    private int RoomCount = 21;

    private void Configure(string role)
    {
        Services.AddLogging();
        Services.AddLocalization(o => o.ResourcesPath = "Resources");
        Services.AddSingleton<ToastService>();
        Services.AddSingleton(TimeProvider.System);
        var properties = Proxy<IPropertyService>((_, _) => Task.FromResult(new PagedResult<PropertyListItem>
        {
            Items = [new() { Id = 7, Name = "House 7", IsActive = true }],
            Page = 1, PageSize = 100, TotalCount = 1,
        }));
        var rooms = Proxy<IRoomReportService>((method, args) =>
        {
            if (method?.Name == nameof(IRoomReportService.GetOccupancyAsync))
            {
                var occupancyFilter = (OccupancyFilter)args![0]!;
                OccupancyRequests.Add(occupancyFilter);
                return Task.FromResult(new OccupancyReportResult(
                [
                    new OccupancyMonthDto("2026-10", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 3, 2, 1, 66.7m),
                ]));
            }

            var filter = (RoomReportFilter)args![0]!;
            RoomRequests.Add(filter);
            CallerRoles.Add(((PropertyCaller)args[1]!).Role);
            if (DenyRooms) return Task.FromException<RoomReportResult>(new UnauthorizedAccessException());
            return Task.FromResult(new RoomReportResult
            {
                Kpis = new() { Total = RoomCount, Available = RoomCount },
                Rooms = new()
                {
                    Items = [new RoomListItem { Code = $"R-{filter.PropertyId}-{filter.Page}", PropertyName = "House 7", Status = RoomStatus.Available }],
                    Page = filter.Page, PageSize = filter.PageSize, TotalCount = RoomCount,
                },
            });
        });
        var tenants = Proxy<ITenantReportService>((_, args) =>
        {
            var filter = (TenantReportFilter)args![0]!;
            TenantRequests.Add(filter);
            CallerRoles.Add(((TenantCaller)args[1]!).Role);
            return Task.FromResult(new TenantReportResult
            {
                // Mô phỏng cả nguồn trả sai quyền để kiểm tra UI vẫn giấu trạng thái tài khoản với Quản lý.
                Kpis = new() { Total = 21, Active = 12, Renting = 8, LinkedAccountCount = 5 },
                Tenants = new()
                {
                    Items = [new TenantReportItem
                    {
                        Id = 1, FullName = $"T-{filter.PropertyId}-{filter.Page}", PhoneNumber = "0900000000",
                        IsActive = true, HasAccount = true, HasActiveContract = true,
                    }],
                    Page = filter.Page, PageSize = filter.PageSize, TotalCount = 21,
                },
            });
        });
        var revenue = new RevenueReportService(NewDbFactory());
        var contracts = Proxy<IContractReportService>((_, _) => Task.FromResult(new ContractReportResult()));
        var costs = Proxy<ICostReportService>((_, _) => Task.FromResult(new CostReportResult()));
        var incidents = Proxy<IIncidentReportService>((_, _) => Task.FromResult(new IncidentReportResult()));
        var export = new ReportExportService(revenue, rooms, tenants, contracts, costs, incidents, NewDb());
        Services.AddSingleton(new ReportsPageService(new P2CallerProvider(new Authentication(role)),
            properties, rooms, tenants, revenue, contracts, costs, incidents, export));
        ComponentFactories.AddStub<RoomStructureChart>();
    }

    [Fact]
    public void Property_change_resets_both_pages_and_updates_source_results()
    {
        Configure(Roles.ChuTro);
        var page = Render<BusinessReportsView>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".room-table tbody tr")));
        GoToPage(page, 2);
        page.WaitForAssertion(() => Assert.Equal(2, RoomRequests.Last().Page));
        page.FindAll(".report-tabs button[role=\"tab\"]")[1].Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".tenant-table tbody tr")));
        GoToPage(page, 2);
        page.WaitForAssertion(() => Assert.Equal(2, TenantRequests.Last().Page));

        page.Find("#report-property").Change("7");

        page.WaitForAssertion(() =>
        {
            Assert.Equal(7, RoomRequests.Last().PropertyId);
            Assert.Equal(7, TenantRequests.Last().PropertyId);
            Assert.Equal(1, RoomRequests.Last().Page);
            Assert.Equal(1, TenantRequests.Last().Page);
            Assert.Contains("T-7-1", page.Find(".tenant-table").TextContent);
            Assert.Equal("21", page.Find(".kpis dd").TextContent);
        });
        page.FindAll(".report-tabs button[role=\"tab\"]")[0].Click();
        page.WaitForAssertion(() => Assert.Contains("R-7-1", page.Find(".room-table").TextContent));
        Assert.All(CallerRoles, value => Assert.Equal(Roles.ChuTro, value));
    }

    [Fact]
    public void Section_filters_requery_their_own_source_and_reset_pagination()
    {
        Configure(Roles.ChuTro);
        var page = Render<BusinessReportsView>();
        page.WaitForAssertion(() => Assert.Single(TenantRequests));
        GoToPage(page, 2);
        page.WaitForAssertion(() => Assert.Equal(2, RoomRequests.Last().Page));
        var tenantCalls = TenantRequests.Count;

        page.Find("#report-room-status").Change("Available");
        page.WaitForAssertion(() =>
        {
            Assert.Equal(RoomStatus.Available, RoomRequests.Last().Status);
            Assert.Equal(1, RoomRequests.Last().Page);
        });
        page.Find("#report-room-activity").Change("false");
        page.WaitForAssertion(() => Assert.False(RoomRequests.Last().IsActive));
        Assert.Equal(tenantCalls, TenantRequests.Count);
        var roomCalls = RoomRequests.Count;

        page.FindAll(".report-tabs button[role=\"tab\"]")[1].Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".tenant-table tbody tr")));
        page.Find("#report-tenant-rental").Change("true");
        page.WaitForAssertion(() => Assert.True(TenantRequests.Last().HasActiveContract));
        page.Find("#report-tenant-activity").Change("false");
        page.WaitForAssertion(() => Assert.False(TenantRequests.Last().IsActive));
        Assert.Equal(roomCalls, RoomRequests.Count);
    }

    [Fact]
    public void Room_denial_keeps_tenant_report_available_and_hides_account_fields_for_manager()
    {
        DenyRooms = true;
        Configure(Roles.QuanLy);
        var page = Render<BusinessReportsView>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll(".report-tabs button[role=\"tab\"]").Count));
        page.FindAll(".report-tabs button[role=\"tab\"]")[1].Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".tenant-table tbody tr")));
        Assert.Empty(page.FindAll(".room-table"));
        Assert.Equal(6, page.FindAll(".tenant-table th").Count);
        Assert.Equal(3, page.FindAll(".kpis dd").Count);
        Assert.All(CallerRoles, value => Assert.Equal(Roles.QuanLy, value));
    }

    [Fact]
    public void Room_tab_shows_monthly_occupancy_from_the_same_filters()
    {
        Configure(Roles.ChuTro);
        var page = Render<BusinessReportsView>();
        page.WaitForAssertion(() => Assert.Single(OccupancyRequests));
        page.WaitForAssertion(() => Assert.Contains("66,7", page.Find(".occupancy-history").TextContent));
        Assert.Null(OccupancyRequests[0].PropertyId);

        page.Find("#report-property").Change("7");
        page.WaitForAssertion(() => Assert.Equal(7, OccupancyRequests.Last().PropertyId));

        page.Find("#report-from").Change("2026-10-01");
        page.WaitForAssertion(() => Assert.Equal(new DateOnly(2026, 10, 1), OccupancyRequests.Last().From));
    }

    [Fact]
    public void Refresh_moves_back_to_last_page_after_source_shrinks()
    {
        Configure(Roles.ChuTro);
        var page = Render<BusinessReportsView>();
        page.WaitForAssertion(() => Assert.Single(RoomRequests));
        GoToPage(page, 2);
        page.WaitForAssertion(() => Assert.Equal(2, RoomRequests.Last().Page));
        RoomCount = 1;
        page.Find(".page-heading-actions button").Click();
        page.WaitForAssertion(() =>
        {
            Assert.Equal(1, RoomRequests.Last().Page);
            Assert.Equal("1", page.Find(".kpis dd").TextContent);
            Assert.Contains("R--1", page.Find(".room-table").TextContent);
        });
    }

    [Fact]
    public void Report_tabs_switch_visible_section_without_requerying_sources()
    {
        Configure(Roles.ChuTro);
        var page = Render<BusinessReportsView>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".room-table tbody tr")));
        var roomCalls = RoomRequests.Count;
        var tenantCalls = TenantRequests.Count;
        Assert.Empty(page.FindAll(".tenant-table"));

        page.FindAll(".report-tabs button[role=\"tab\"]")[1].Click();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".tenant-table tbody tr")));
        Assert.Empty(page.FindAll(".room-table"));
        Assert.Equal(roomCalls, RoomRequests.Count);
        Assert.Equal(tenantCalls, TenantRequests.Count);

        page.FindAll(".report-tabs button[role=\"tab\"]")[0].Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".room-table tbody tr")));
        Assert.Equal(roomCalls, RoomRequests.Count);
        Assert.Equal(tenantCalls, TenantRequests.Count);
    }

    [Fact]
    public void Report_route_and_navigation_allow_only_business_report_roles()
    {
        var authorization = typeof(BusinessReportsView).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("ChuTro,QuanLy", authorization?.Roles);
        var item = Assert.Single(AppNavigation.Items, item => item.Key == "Reports");
        Assert.Equal("/reports", item.Href);
        Assert.Equal("ChuTro,QuanLy", item.Roles);
    }

    private static ApplicationDbContext NewDb() => new(NewDbOptions());

    private static IDbContextFactory<ApplicationDbContext> NewDbFactory() =>
        new PooledDbContextFactory<ApplicationDbContext>(NewDbOptions());

    private static DbContextOptions<ApplicationDbContext> NewDbOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"vht140-reports-{Guid.NewGuid()}")
            .Options;

    private static void GoToPage(IRenderedComponent<BusinessReportsView> page, int number) =>
        page.WaitForAssertion(() => page.FindAll(".table-pager__pages button")
            .Single(button => button.TextContent.Trim() == number.ToString(CultureInfo.InvariantCulture))
            .Click());

    private static T Proxy<T>(Func<MethodInfo?, object?[]?, object?> handler) where T : class
    {
        var instance = DispatchProxy.Create<T, ServiceProxy>();
        ((ServiceProxy)(object)instance).Handler = handler;
        return instance;
    }

    public class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo?, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method, args);
    }

    private sealed class Authentication(string role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "report-user"), new Claim(ClaimTypes.Role, role)], "test"))));
    }
}
