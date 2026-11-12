using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Web.Features.Dashboard;
using Web.Features.Dashboard.Components;
using Web.Features.Dashboard.Models;
using Web.Features.Reports.DTOs;

namespace NhaTro.UnitTests.Components;

public sealed class DashboardComponentTests : BunitContext
{
    public DashboardComponentTests() { Services.AddLogging(); Services.AddLocalization(options => options.ResourcesPath = "Resources"); }

    [Theory]
    [InlineData(true, 5)]
    [InlineData(false, 3)]
    public void Financial_cards_only_render_for_owner(bool owner, int count)
    {
        var component = Render<KPICardGrid>(p => p.Add(c => c.IsOwner, owner)
            .Add(c => c.State, DashboardLoadState.Loaded).Add(c => c.Kpis, new RoomReportKpis { Total = 8, Rented = 5, Maintenance = 2 })
            .Add(c => c.RoomsHref, "/rooms?propertyId=7")
            .Add(c => c.Revenue, new RevenueReportDto(1000m, 250m, 2, 1, []))
            .Add(c => c.Incidents, new IncidentReportKpis { Total = 2 })
            .Add(c => c.RevenueHref, "/reports?propertyId=7")
            .Add(c => c.IncidentsHref, "/reports?propertyId=7"));
        Assert.Equal(count, component.FindAll("article").Count);
        Assert.DoesNotContain("unwired", component.Markup);
        var hrefs = component.FindAll("a").Select(a => a.GetAttribute("href")).Distinct().ToList();
        Assert.Contains("/rooms?propertyId=7", hrefs);
        Assert.Contains("/reports?propertyId=7", hrefs);
        Assert.Contains("83,3 %", component.Markup);
    }

    [Fact]
    public void Denial_does_not_render_room_values()
    {
        var component = Render<KPICardGrid>(p => p.Add(c => c.State, DashboardLoadState.Denied)
            .Add(c => c.Kpis, new RoomReportKpis { Total = 987 })
            .Add(c => c.Incidents, new IncidentReportKpis { Total = 0 })
            .Add(c => c.RoomsHref, "/rooms")
            .Add(c => c.IncidentsHref, "/reports"));
        Assert.DoesNotContain("987", component.Markup);
        Assert.DoesNotContain(component.FindAll("a"), a => a.GetAttribute("href")!.StartsWith("/rooms"));
        Assert.Equal(2, component.FindAll("button").Count);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(4, 4)]
    public void Tenant_summary_only_shows_linked_accounts_when_available(int? linked, int count)
    {
        var component = Render<TenantOverviewCard>(p => p.Add(c => c.Kpis,
            new TenantReportKpis { Total = 12, Active = 10, Renting = 8, LinkedAccountCount = linked }));
        var values = component.FindAll("dd").Select(e => e.TextContent).ToArray();
        Assert.Equal(count, values.Length);
        Assert.Equal(new[] { "12", "10", "8" }, values[..3]);
        if (linked.HasValue)
            Assert.Equal("4", values[3]);
    }

    [Fact]
    public void Empty_room_source_renders_empty_state_without_chart()
    {
        var component = Render<RoomStructureChart>(p => p.Add(c => c.Kpis, new RoomReportKpis()));
        Assert.Single(component.FindAll(".empty-state"));
        Assert.Empty(component.FindAll(".room-chart"));
    }
}
