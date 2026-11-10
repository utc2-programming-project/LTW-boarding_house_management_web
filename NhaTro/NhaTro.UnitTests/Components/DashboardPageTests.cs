using System.Reflection;
using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Web.Common.Models;
using Web.Components.Shared.Feedback;
using Web.Features.Accounts;
using Web.Features.Dashboard.Components;
using Web.Features.Dashboard.Pages;
using Web.Features.Dashboard.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;

namespace NhaTro.UnitTests.Components;

public sealed class DashboardPageTests : BunitContext
{
    private readonly List<int?> RoomRequests = [];
    private readonly List<int?> TenantRequests = [];
    private bool DenyRooms;

    private void Configure(string role)
    {
        Services.AddLogging();
        Services.AddLocalization(o => o.ResourcesPath = "Resources");
        Services.AddSingleton<ToastService>();
        Services.AddSingleton(TimeProvider.System);
        var properties = Proxy<IPropertyService>((method, args) => Task.FromResult(new PagedResult<PropertyListItem>
        {
            Items = [new() { Id = 7, Name = "House 7", IsActive = true }], Page = 1, PageSize = 100, TotalCount = 1,
        }));
        var rooms = Proxy<IRoomReportService>((method, args) =>
        {
            RoomRequests.Add(((RoomReportFilter)args![0]!).PropertyId);
            return DenyRooms ? Task.FromException<RoomReportResult>(new UnauthorizedAccessException()) :
                Task.FromResult(new RoomReportResult { Kpis = new() { Total = 3, Rented = 2, Available = 1 }, Rooms = new() { Items = [], Page = 1, PageSize = 1, TotalCount = 3 } });
        });
        var tenants = Proxy<ITenantReportService>((method, args) =>
        {
            TenantRequests.Add(((TenantReportFilter)args![0]!).PropertyId);
            return Task.FromResult(new TenantReportResult
            {
                Kpis = new() { Total = 15, Active = 12, Renting = 8, LinkedAccountCount = 5 }
            });
        });
        Services.AddSingleton(new DashboardPageService(new P2CallerProvider(new Authentication(role)), properties, rooms, tenants, TimeProvider.System));
        ComponentFactories.AddStub<RoomStructureChart>();
    }

    [Fact]
    public void Property_filter_refresh_and_period_use_shared_state()
    {
        Configure(Roles.ChuTro);
        var page = Render<DashboardOverview>();
        page.WaitForAssertion(() => Assert.Single(RoomRequests));
        page.FindAll("select")[0].Change("7");
        page.WaitForAssertion(() => Assert.Equal(7, RoomRequests.Last()));
        page.WaitForAssertion(() => Assert.Equal(new int?[] { null, 7 }, TenantRequests));
        Assert.Equal("15", page.FindAll("dd")[0].TextContent);
        Assert.All(page.FindAll(".kpi-card a"), a => Assert.Equal("/rooms?propertyId=7", a.GetAttribute("href")));
        var count = RoomRequests.Count;
        page.FindAll("select")[1].Change("Year");
        Assert.Equal(count, RoomRequests.Count);
        page.Find(".page-header button").Click();
        page.WaitForAssertion(() => Assert.Equal(count + 1, RoomRequests.Count));
        Assert.Equal(7, RoomRequests.Last());
        page.WaitForAssertion(() => Assert.Equal(new int?[] { null, 7, 7 }, TenantRequests));
        Assert.Equal("Year", page.FindAll("select")[1].GetAttribute("value"));
    }

    [Fact]
    public void Refresh_updates_additional_work_without_resetting_property_filter()
    {
        Configure(Roles.ChuTro);
        var refreshes = 0;
        var page = Render<DashboardOverview>(parameters => parameters
            .Add(component => component.RefreshAdditionalContent, () => { refreshes++; }));
        page.WaitForAssertion(() => Assert.Single(RoomRequests));
        Assert.Equal(0, refreshes);

        page.FindAll("select")[0].Change("7");
        page.WaitForAssertion(() => Assert.Equal(7, TenantRequests.Last()));
        page.Find(".page-header button").Click();

        page.WaitForAssertion(() => Assert.Equal(1, refreshes));
        page.WaitForAssertion(() => Assert.Equal(new int?[] { null, 7, 7 }, RoomRequests));
        Assert.Equal("7", page.FindAll("select")[0].GetAttribute("value"));
    }

    [Fact]
    public void Room_denial_preserves_tenant_source()
    {
        DenyRooms = true;
        Configure(Roles.ChuTro);
        var page = Render<DashboardOverview>();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll("dd").Count));
        Assert.Empty(page.FindAll(".kpi-card a"));
        Assert.Equal("15", page.FindAll("dd")[0].TextContent);
    }

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
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "dashboard-user"), new Claim(ClaimTypes.Role, role)], "test"))));
    }
}
