using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Features.Accounts;
using Web.Features.Tenants.DTOs;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Adapter riêng cho màn hình P3: tạo TenantCaller từ phiên đăng nhập và gọi service nghiệp vụ.
/// Component không tự dựng danh tính, truy cập DbContext hay chuyển đổi DTO phân trang.
/// </summary>
public sealed class TenantPageService(AuthenticationStateProvider authenticationState, ITenantService tenants)
{
    public async Task<string> GetCurrentRoleAsync()
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;
        return Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
    }

    public async Task<Web.Common.Models.PagedResult<TenantListItemDto>> ListAsync(
        TenantFilterRequest filter, CancellationToken ct = default)
    {
        var result = await tenants.ListAsync(filter, await GetCallerAsync(), ct);
        return new Web.Common.Models.PagedResult<TenantListItemDto>
        {
            Items = result.Items,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<TenantListSummaryDto> GetSummaryAsync(CancellationToken ct = default) =>
        await tenants.GetSummaryAsync(await GetCallerAsync(), ct);

    public Task<TenantResult> GetAsync(int id, CancellationToken ct = default) =>
        CallAsync(caller => tenants.GetAsync(id, caller, ct));

    public Task<TenantResult> GetOwnAsync(CancellationToken ct = default) =>
        CallAsync(caller => tenants.GetOwnAsync(caller, ct));

    public Task<TenantResult> CreateAsync(CreateTenantRequest request, CancellationToken ct = default) =>
        CallAsync(caller => tenants.CreateAsync(request, caller, ct));

    public Task<TenantResult> UpdateAsync(int id, UpdateTenantRequest request, CancellationToken ct = default) =>
        CallAsync(caller => tenants.UpdateAsync(id, request, caller, ct));

    public Task<TenantResult> UpdateOwnAsync(UpdateOwnProfileRequest request, CancellationToken ct = default) =>
        CallAsync(caller => tenants.UpdateOwnAsync(request, caller, ct));

    public Task<TenantResult> SetActiveAsync(
        int id, bool isActive, string rowVersion, CancellationToken ct = default) =>
        CallAsync(caller => tenants.SetActiveAsync(id, isActive, rowVersion, caller, ct));

    public Task<TenantResult> LinkAccountAsync(
        int id, string accountUserId, string rowVersion, CancellationToken ct = default) =>
        CallAsync(caller => tenants.LinkAccountAsync(id, accountUserId, rowVersion, caller, ct));

    public Task<TenantResult> UnlinkAccountAsync(
        int id, string rowVersion, CancellationToken ct = default) =>
        CallAsync(caller => tenants.UnlinkAccountAsync(id, rowVersion, caller, ct));

    public async Task<List<TenantAccountOptionDto>> SearchUnlinkedAccountsAsync(
        string? keyword, int limit = 20, CancellationToken ct = default) =>
        await tenants.SearchUnlinkedAccountsAsync(keyword, limit, await GetCallerAsync(), ct);

    // 2B (VHT-25) — trang khách/trang chi tiết đọc phòng đang thuê từ hợp đồng thật.
    public Task<TenantRentalResult> GetRentalOverviewAsync(int id, CancellationToken ct = default) =>
        CallRentalAsync(caller => tenants.GetRentalOverviewAsync(id, caller, ct));

    public Task<TenantRentalResult> GetOwnRentalOverviewAsync(CancellationToken ct = default) =>
        CallRentalAsync(caller => tenants.GetOwnRentalOverviewAsync(caller, ct));

    private async Task<TenantResult> CallAsync(Func<TenantCaller, Task<TenantResult>> operation) =>
        await operation(await GetCallerAsync());

    private async Task<TenantRentalResult> CallRentalAsync(
        Func<TenantCaller, Task<TenantRentalResult>> operation) =>
        await operation(await GetCallerAsync());

    private async Task<TenantCaller> GetCallerAsync()
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var role = Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
        return new TenantCaller(userId, role);
    }
}
