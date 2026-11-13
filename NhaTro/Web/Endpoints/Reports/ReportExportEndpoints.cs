using System.Security.Claims;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Services;
using Web.Infrastructure.Time;

namespace Web.Endpoints.Reports;

/// <summary>Endpoint tai tep Excel P8; quyen va pham vi nam trong service xuat.</summary>
public static class ReportExportEndpoints
{
    public static IEndpointRouteBuilder MapReportExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reports/export/{section}", ExportAsync)
            .RequireAuthorization("Business");
        return endpoints;
    }

    private static async Task<IResult> ExportAsync(
        HttpContext http,
        string section,
        string? returnUrl,
        ReportExportService service,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
        if (int.TryParse(section, out _)
            || !Enum.TryParse<ReportExportSection>(section, true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "InvalidSection");
        }

        var caller = new PropertyCaller(userId, Roles.ResolvePrimaryRole(http.User));
        try
        {
            var bytes = parsed switch
            {
                ReportExportSection.Revenue => await service.ExportRevenueExcelAsync(caller, cancellationToken: cancellationToken),
                ReportExportSection.Rooms => await service.ExportRoomsExcelAsync(caller, cancellationToken: cancellationToken),
                ReportExportSection.Tenants => await service.ExportTenantsExcelAsync(caller, cancellationToken: cancellationToken),
                ReportExportSection.Contracts => await service.ExportContractsExcelAsync(caller, cancellationToken: cancellationToken),
                ReportExportSection.Costs => await service.ExportCostsExcelAsync(caller, cancellationToken: cancellationToken),
                ReportExportSection.Incidents => await service.ExportIncidentsExcelAsync(caller, cancellationToken: cancellationToken),
                _ => throw new InvalidOperationException(),
            };
            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            var name = $"bao-cao-{parsed.ToSheetName()}-{VietnamTime.ToVietnam(clock.GetUtcNow().UtcDateTime):yyyyMMdd-HHmm}.xlsx";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
        }
        catch (ReportExportAuthorizationException exception)
        {
            return ExportFailure(returnUrl, exception.ErrorCode, StatusCodes.Status403Forbidden);
        }
        catch (UnauthorizedAccessException)
        {
            return ExportFailure(returnUrl, ReportExportErrorCodes.Forbidden, StatusCodes.Status403Forbidden);
        }
        catch (ReportExportException exception)
        {
            var status = exception.ErrorCode == ReportExportErrorCodes.VolumeExceeded
                ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status409Conflict;
            return ExportFailure(returnUrl, exception.ErrorCode, status);
        }
    }

    private static IResult ExportFailure(string? returnUrl, string code, int statusCode)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            var separator = returnUrl.Contains('?') ? '&' : '?';
            return Results.Redirect($"{returnUrl}{separator}exportError={Uri.EscapeDataString(code)}");
        }
        return Results.Problem(statusCode: statusCode, title: code);
    }
}
