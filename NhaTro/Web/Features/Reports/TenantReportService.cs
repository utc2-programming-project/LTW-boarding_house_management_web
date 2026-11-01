using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Features.Tenants;

namespace Web.Features.Reports;

public sealed class TenantReportService(ITenantService tenants, IManagerTogglePolicy togglePolicy)
    : ITenantReportService
{
    public async Task<TenantReportKpis> GetAsync(TenantCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager || string.IsNullOrWhiteSpace(caller.UserId))
            throw new UnauthorizedAccessException();

        // Quản lý qua cả hai công tắc, không nhận số P3 toàn hệ thống.
        if (caller.IsQuanLy)
        {
            foreach (var code in new[] { "SW-15", "SW-15b" })
                if (await togglePolicy.EvaluateAsync(code, caller.Role, ct) != RoomChangeDecision.Allowed)
                    throw new UnauthorizedAccessException();
            return new TenantReportKpis();
        }

        var summary = await tenants.GetSummaryAsync(caller, ct);
        return new TenantReportKpis
        {
            Total = summary.TotalCount,
            Active = summary.ActiveCount,
            LinkedAccountCount = summary.LinkedAccountCount,
        };
    }
}
