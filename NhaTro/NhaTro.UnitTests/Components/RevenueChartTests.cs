using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Web.Features.Reports.Components;
using Web.Features.Reports.DTOs;

namespace NhaTro.UnitTests.Components;

public sealed class RevenueChartTests : BunitContext
{
    public RevenueChartTests()
    {
        Services.AddLogging();
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static readonly RevenuePeriodGroupDto[] Periods =
    [
        new("2026-08", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), 3_000_000m, 2_500_000m, 500_000m, 3, 2),
        new("2026-09", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 4_000_000m, 3_400_000m, 600_000m, 4, 3),
        new("2026-10", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 2_000_000m, 1_800_000m, 200_000m, 2, 1),
    ];

    [Fact]
    public void Empty_source_renders_empty_state_without_chart()
    {
        var component = Render<RevenueChart>(p => p.Add(c => c.Periods, []));
        Assert.Single(component.FindAll(".empty-state"));
        Assert.Empty(component.FindAll(".revenue-chart"));
    }

    [Fact]
    public void Column_mode_draws_two_gradient_bars_per_period()
    {
        var component = Render<RevenueChart>(p => p.Add(c => c.Periods, Periods));
        Assert.Equal(Periods.Length * 2, component.FindAll("rect.chart-bar").Count);
        Assert.Equal(2, component.FindAll("linearGradient[id$='-bar-0'],linearGradient[id$='-bar-1']").Count);
        Assert.All(component.FindAll("rect.chart-bar"), bar => Assert.StartsWith("url(#", bar.GetAttribute("filter")));
        Assert.Empty(component.FindAll("path.chart-line"));
    }

    [Fact]
    public void Line_button_switches_to_line_and_area_with_point_per_period()
    {
        var component = Render<RevenueChart>(p => p.Add(c => c.Periods, Periods));
        var buttons = component.FindAll(".revenue-chart__toolbar button");
        Assert.Equal(2, buttons.Count);
        buttons[1].Click();

        Assert.Empty(component.FindAll("rect.chart-bar"));
        Assert.Equal(2, component.FindAll("path.chart-line").Count);
        Assert.Equal(2, component.FindAll("path.chart-area").Count);
        Assert.Equal(Periods.Length * 2, component.FindAll("circle.chart-point").Count);
        Assert.Equal(Periods.Length * 2, component.FindAll("circle.chart-point-halo").Count);
        Assert.All(component.FindAll("path.chart-line"), line => Assert.Contains("stroke:url(#", line.GetAttribute("style")));

        component.FindAll(".revenue-chart__toolbar button")[0].Click();
        Assert.Equal(Periods.Length * 2, component.FindAll("rect.chart-bar").Count);
    }

    [Fact]
    public void Axis_labels_use_invariant_coordinates_and_compact_money()
    {
        var component = Render<RevenueChart>(p => p.Add(c => c.Periods, Periods));
        var yLabels = component.FindAll("text.chart-axis-y");
        Assert.Equal(5, yLabels.Count);
        Assert.Contains(yLabels, label => label.TextContent.Contains("tr"));
        Assert.All(yLabels, label => Assert.DoesNotContain(",", label.GetAttribute("y")));
        Assert.All(component.FindAll("text.chart-axis-x"), label => Assert.DoesNotContain(",", label.GetAttribute("x")));
    }

    [Fact]
    public void Data_table_lists_every_period_with_both_series()
    {
        var component = Render<RevenueChart>(p => p.Add(c => c.Periods, Periods));
        var rows = component.FindAll("table tbody tr");
        Assert.Equal(Periods.Length, rows.Count);
        Assert.Contains("2026-09", rows[1].TextContent);
        Assert.Contains("3.400.000", rows[1].TextContent);
        Assert.Contains("600.000", rows[1].TextContent);
    }

    [Fact]
    public void Unknown_period_key_falls_back_to_localized_label_in_table()
    {
        var component = Render<RevenueChart>(p => p.Add(c => c.Periods,
            [Periods[0] with { PeriodKey = "unknown" }]));
        Assert.DoesNotContain("unknown", component.Find("table tbody tr").TextContent);
    }
}
