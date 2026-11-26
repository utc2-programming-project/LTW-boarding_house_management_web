using System.Security.Claims;
using Web.Features.Accounts;
using Web.Features.Accounts.Owners;
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
        OwnerAccessGuard ownerAccess,
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

        // PLAN-001 #7: báo cáo không được chứa dữ liệu tòa của chủ trọ đang bị khoá.
        if (caller.Role is Roles.QuanLy or Roles.KhachThue)
        {
            var blocked = await ownerAccess.GetBlockedOwnerIdsAsync(cancellationToken);
            if (blocked.Count > 0)
            {
                caller = caller with { BlockedOwnerIds = blocked.ToList() };
            }
        }

        try
        {
            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            var name = $"bao-cao-{parsed.ToSheetName()}-{VietnamTime.ToVietnam(clock.GetUtcNow().UtcDateTime):yyyyMMdd-HHmm}.xlsx";
            http.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            http.Response.Headers.ContentDisposition = $"attachment; filename=\"{name}\"";

            // Ghi thẳng vào luồng phản hồi: không dựng cả file trong RAM rồi mới trả.
            switch (parsed)
            {
                case ReportExportSection.Revenue:
                    await service.WriteRevenueExcelAsync(http.Response.Body, caller, cancellationToken: cancellationToken);
                    break;
                case ReportExportSection.Rooms:
                    await service.WriteRoomsExcelAsync(http.Response.Body, caller, cancellationToken: cancellationToken);
                    break;
                case ReportExportSection.Tenants:
                    await service.WriteTenantsExcelAsync(http.Response.Body, caller, cancellationToken: cancellationToken);
                    break;
                case ReportExportSection.Contracts:
                    await service.WriteContractsExcelAsync(http.Response.Body, caller, cancellationToken: cancellationToken);
                    break;
                case ReportExportSection.Costs:
                    await service.WriteCostsExcelAsync(http.Response.Body, caller, cancellationToken: cancellationToken);
                    break;
                case ReportExportSection.Incidents:
                    await service.WriteIncidentsExcelAsync(http.Response.Body, caller, cancellationToken: cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return Results.Empty;
        }
        catch (ReportExportAuthorizationException exception)
        {
            ResetResponse(http);
            return ExportFailure(returnUrl, exception.ErrorCode, StatusCodes.Status403Forbidden);
        }
        catch (UnauthorizedAccessException)
        {
            ResetResponse(http);
            return ExportFailure(returnUrl, ReportExportErrorCodes.Forbidden, StatusCodes.Status403Forbidden);
        }
        catch (ReportExportException exception)
        {
            ResetResponse(http);
            var status = exception.ErrorCode == ReportExportErrorCodes.VolumeExceeded
                ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status409Conflict;
            return ExportFailure(returnUrl, exception.ErrorCode, status);
        }
    }

    private static void ResetResponse(HttpContext http)
    {
        if (!http.Response.HasStarted)
        {
            http.Response.Clear();
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
