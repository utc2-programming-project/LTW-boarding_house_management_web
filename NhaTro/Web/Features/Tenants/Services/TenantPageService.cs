using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Features.Accounts;
using Web.Features.Tenants.DTOs;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Adapter riêng cho màn hình P3: tạo TenantCaller từ phiên đăng nhập và gọi service nghiệp vụ.
/// Component không tự dựng danh tính, truy cập DbContext hay chuyển đổi DTO phân trang.
/// </summary>
public sealed class TenantPageService(
    AuthenticationStateProvider authenticationState,
    ITenantService tenants,
    Web.Features.Tenants.Invitations.TenantInvitationService invitations,
    Microsoft.AspNetCore.Components.NavigationManager navigation,
    Web.Features.Tenants.Invitations.TenantQrInvitationService? qrInvitations = null,
    Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
{
    /// <summary>Base URL cấu hình cho link mời; fallback BaseUri khi chưa cấu hình.</summary>
    public string InviteBaseUrl
    {
        get
        {
            var configured = configuration?["Invitations:PublicBaseUrl"];
            return string.IsNullOrWhiteSpace(configured) ? navigation.BaseUri : configured;
        }
    }
    public async Task<string> GetCurrentRoleAsync()
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;
        return Roles.ResolvePrimaryRole(user);
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

    // PLAN-006: mời khách thuê qua email (liên kết dựng từ BaseUri hiện tại).
    public async Task<Web.Features.Tenants.Invitations.TenantInvitationResult> CreateInvitationAsync(
        Web.Features.Tenants.Invitations.CreateTenantInvitationRequest request, CancellationToken ct = default) =>
        await invitations.CreateAsync(request, await GetCallerAsync(), InviteBaseUrl, ct);

    public async Task<IReadOnlyList<Web.Features.Tenants.Invitations.TenantInvitationDto>> ListInvitationsAsync(
        CancellationToken ct = default) =>
        await invitations.ListAsync(await GetCallerAsync(), ct);

    public async Task<bool> RevokeInvitationAsync(int id, CancellationToken ct = default) =>
        await invitations.RevokeAsync(id, await GetCallerAsync(), ct);

    // PLAN-024: QR nhiều mốc hạn + mời khách đã có tài khoản.
    public async Task<Web.Features.Tenants.Invitations.TenantInvitationResult> CreateQrInvitationAsync(
        Web.Features.Tenants.Invitations.CreateTenantQrRequest request, CancellationToken ct = default) =>
        await qrInvitations!.CreateQrAsync(request, await GetCallerAsync(), InviteBaseUrl, ct);

    public Task<Web.Features.Tenants.Invitations.TenantInvitationDetails?> PeekInvitationAsync(
        string token, CancellationToken ct = default) =>
        qrInvitations!.PeekDetailsAsync(token, ct);

    public Task<Web.Features.Tenants.Invitations.TenantInvitationResult> BeginNewTenantAccountAsync(
        string token,
        Web.Features.Tenants.Invitations.NewTenantAccountRequest request,
        CancellationToken ct = default) =>
        qrInvitations!.BeginNewAccountAsync(token, request, ct);

    public async Task<Web.Features.Tenants.Invitations.TenantInvitationResult> AcceptExistingInvitationAsync(
        string token, CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        return await qrInvitations!.AcceptExistingAsync(token, caller.UserId, ct);
    }

    public async Task<IReadOnlyList<Web.Features.Tenants.Invitations.ExistingTenantOptionDto>>
        SearchExistingTenantsAsync(string? keyword, CancellationToken ct = default) =>
        await qrInvitations!.SearchExistingTenantsAsync(keyword, await GetCallerAsync(), ct);

    public Task<TenantResult> UpdateAsync(int id, UpdateTenantRequest request, CancellationToken ct = default) =>
        CallAsync(caller => tenants.UpdateAsync(id, request, caller, ct));

    public Task<TenantResult> UpdateOwnAsync(UpdateOwnProfileRequest request, CancellationToken ct = default) =>
        CallAsync(caller => tenants.UpdateOwnAsync(request, caller, ct));

    public Task<TenantResult> SetActiveAsync(
        int id, bool isActive, string rowVersion, CancellationToken ct = default) =>
        CallAsync(caller => tenants.SetActiveAsync(id, isActive, rowVersion, caller, ct));

    public Task<TenantResult> DeleteAsync(
        int id, string rowVersion, CancellationToken ct = default) =>
        CallAsync(caller => tenants.DeleteAsync(id, rowVersion, caller, ct));

    public Task<TenantResult> RestoreAsync(
        int id, string rowVersion, CancellationToken ct = default) =>
        CallAsync(caller => tenants.RestoreAsync(id, rowVersion, caller, ct));

    public async Task<Web.Common.Models.PagedResult<TenantTrashItemDto>> ListTrashAsync(
        TenantTrashFilterRequest filter, CancellationToken ct = default)
    {
        var result = await tenants.ListTrashAsync(filter, await GetCallerAsync(), ct);
        return new Web.Common.Models.PagedResult<TenantTrashItemDto>
        {
            Items = result.Items,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

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
        var role = Roles.ResolvePrimaryRole(user);
        return new TenantCaller(userId, role);
    }
}
