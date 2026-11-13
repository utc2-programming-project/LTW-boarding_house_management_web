using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Web.Features.Accounts;
using Web.Features.Audit.DTOs;
using Web.Features.Audit.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;

namespace Web.Features.Audit.Services;

public sealed record AuditLogPageRequest(
    DateOnly? From = null,
    DateOnly? To = null,
    string? ActorUserId = null,
    string? Action = null,
    int? PropertyId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record AuditPropertyOption(int Id, string Name);

public sealed record AuditLogPageResult(
    bool IsOwner,
    AuditLogPageDto? Page,
    AuditLogFilterOptionsDto? Options,
    IReadOnlyList<AuditPropertyOption> Properties,
    string? ErrorCode)
{
    public bool Succeeded => ErrorCode is null;
}

/// <summary>
/// Adapter UI cho màn hình nhật ký thao tác (VHT-104): xác định người gọi, tạo scope ngắn
/// để không giữ DbContext của circuit, và chuyển lỗi quyền thành trạng thái cho UI.
/// </summary>
public sealed class AuditLogPageService(
    AuthenticationStateProvider authStateProvider,
    IServiceScopeFactory scopeFactory)
{
    public async Task<AuditLogPageResult> LoadAsync(
        AuditLogPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        var role = Roles.ResolvePrimaryRole(user);
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var caller = new PropertyCaller(userId, role);
        var isOwner = caller.IsChuTro;

        var query = new AuditLogQuery(
            ToOffsetStartOfDay(request.From),
            ToOffsetEndOfDay(request.To),
            string.IsNullOrWhiteSpace(request.ActorUserId) ? null : request.ActorUserId,
            string.IsNullOrWhiteSpace(request.Action) ? null : request.Action,
            isOwner ? request.PropertyId : null,
            request.Page,
            request.PageSize);

        await using var scope = scopeFactory.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditLogService>();

        try
        {
            var page = await audit.QueryAsync(caller, query, cancellationToken);
            var options = await audit.GetFilterOptionsAsync(caller, cancellationToken);
            var properties = isOwner ? await LoadPropertiesAsync(scope.ServiceProvider, caller, cancellationToken) : [];

            return new AuditLogPageResult(isOwner, page, options, properties, ErrorCode: null);
        }
        catch (AuditLogAuthorizationException ex)
        {
            return new AuditLogPageResult(isOwner, null, null, [], ex.ErrorCode);
        }
        catch (UnauthorizedAccessException)
        {
            return new AuditLogPageResult(isOwner, null, null, [], AuditLogErrorCodes.Forbidden);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new AuditLogPageResult(isOwner, null, null, [], "Error");
        }
    }

    private static async Task<IReadOnlyList<AuditPropertyOption>> LoadPropertiesAsync(
        IServiceProvider provider,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var properties = provider.GetRequiredService<IPropertyService>();
        var page = await properties.ListAsync(
            new PropertyFilter { PageSize = 200, SortBy = "name" },
            caller,
            cancellationToken);

        return page.Items
            .Select(item => new AuditPropertyOption(item.Id, item.Name))
            .ToList();
    }

    private static DateTimeOffset? ToOffsetStartOfDay(DateOnly? date) =>
        date is { } value ? ToOffset(value.ToDateTime(TimeOnly.MinValue)) : null;

    private static DateTimeOffset? ToOffsetEndOfDay(DateOnly? date) =>
        date is { } value ? ToOffset(value.ToDateTime(TimeOnly.MaxValue)) : null;

    private static DateTimeOffset ToOffset(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }
}
