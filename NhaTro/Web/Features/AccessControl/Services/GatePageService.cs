using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Web.Data;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Enums;
using Web.Features.Accounts;
using Web.Features.Properties;

namespace Web.Features.AccessControl.Services;

/// <summary>Adapter UI: giữ caller lấy từ principal, không nhận userId/role từ client.</summary>
public sealed class GatePageService(
    AuthenticationStateProvider authentication,
    IGatePassService passes,
    IGatePassInviteService invites,
    IGatePolicyService policies,
    IGateAccessService access,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IStayReportService stays,
    IBiometricEnrollmentService biometrics,
    IGateTokenService tokens,
    NavigationManager navigation,
    IConfiguration configuration,
    TimeProvider clock)
{
    /// <summary>Base URL cho link khách tự điền; cùng cấu hình với link mời khách thuê.</summary>
    public string PublicBaseUrl
    {
        get
        {
            var configured = configuration["Invitations:PublicBaseUrl"];
            return string.IsNullOrWhiteSpace(configured)
                ? navigation.BaseUri.TrimEnd('/')
                : configured.TrimEnd('/');
        }
    }

    public string GuestInviteUrl(string rawToken) => $"{PublicBaseUrl}/guest/{Uri.EscapeDataString(rawToken)}";

    public string GuestPassUrl(string rawToken) => $"{PublicBaseUrl}/guest/p/{Uri.EscapeDataString(rawToken)}";

    public async Task<PropertyCaller> CallerAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var role = Roles.ResolvePrimaryRole(user);
        if (role is not (Roles.ChuTro or Roles.QuanLy or Roles.KhachThue))
        {
            throw new UnauthorizedAccessException(GateErrorCodes.Forbidden);
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException(GateErrorCodes.Forbidden);
        }

        return new PropertyCaller(userId, role);
    }

    public async Task<IReadOnlyList<GatePassDto>> PassesAsync(GatePassStatus? status = null) =>
        await passes.ListAsync(await CallerAsync(), status);

    /// <summary>QR động của cư dân; ký theo cửa sổ 60 giây, không lưu DB.</summary>
    public async Task<string> ResidentQrAsync(int propertyId)
    {
        var caller = await CallerAsync();
        if (!caller.IsKhachThue)
        {
            throw new GatePageException(GateErrorCodes.Forbidden);
        }

        return tokens.IssueResidentToken(caller.UserId, propertyId, clock.GetUtcNow());
    }

    /// <summary>
    /// Trạng thái trong/ngoài của chính khách thuê: lượt GateLog thành công mới nhất.
    /// Độc lập với việc chính sách anti-passback có bật hay không (chỉ để hiển thị).
    /// </summary>
    public async Task<ResidentGateStatusDto> ResidentStatusAsync()
    {
        var caller = await CallerAsync();
        if (!caller.IsKhachThue)
        {
            throw new GatePageException(GateErrorCodes.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var last = await db.GateLogs.AsNoTracking()
            .Where(log => log.UserId == caller.UserId && log.IsSuccess)
            .OrderByDescending(log => log.OccurredAt)
            .Select(log => new { log.Direction, log.OccurredAt })
            .FirstOrDefaultAsync();

        return last is null
            ? new ResidentGateStatusDto(false, null, null)
            : new ResidentGateStatusDto(last.Direction == GateDirection.In, last.OccurredAt, last.Direction);
    }

    public async Task<IReadOnlyList<GateRoomOption>> RoomsAsync() => await passes.RoomsAsync(await CallerAsync());

    public async Task<GatePassCreatedDto> CreatePassAsync(GatePassCreateRequest request)
    {
        var result = await passes.CreateAsync(request, await CallerAsync());
        if (!result.Succeeded || result.Created is null)
        {
            throw new GatePageException(result.ErrorCode ?? GateErrorCodes.SaveFailed);
        }

        return result.Created;
    }

    public async Task ApproveAsync(int id) => Ensure(await passes.ApproveAsync(id, await CallerAsync()));
    public async Task RejectAsync(int id, string reason) => Ensure(await passes.RejectAsync(id, reason, await CallerAsync()));
    public async Task RevokeAsync(int id, string reason) => Ensure(await passes.RevokeAsync(id, reason, await CallerAsync()));

    public async Task<GateInviteCreatedDto> CreateInviteAsync(GateInviteCreateRequest request)
    {
        var result = await invites.CreateAsync(request, await CallerAsync());
        if (!result.Succeeded || result.Created is null)
        {
            throw new GatePageException(result.ErrorCode ?? GateErrorCodes.SaveFailed);
        }

        return result.Created;
    }

    public async Task<IReadOnlyList<GateInviteDto>> ActiveInvitesAsync() =>
        await invites.ListActiveAsync(await CallerAsync());

    public async Task CancelInviteAsync(int id)
    {
        var result = await invites.CancelAsync(id, await CallerAsync());
        if (!result.Succeeded)
        {
            throw new GatePageException(result.ErrorCode ?? GateErrorCodes.SaveFailed);
        }
    }

    public async Task<IReadOnlyList<GatePropertyOption>> PropertiesAsync() => await policies.PropertiesAsync(await CallerAsync());

    public async Task<(GatePolicyDto Policy, IReadOnlyList<GateDeviceDto> Devices)> SettingsAsync(int propertyId) =>
        (await policies.GetAsync(propertyId, await CallerAsync()),
         await policies.DevicesAsync(propertyId, await CallerAsync()));

    public async Task SavePolicyAsync(GatePolicySaveRequest request)
    {
        var caller = await CallerAsync();
        await MapValidationAsync(() => policies.SaveAsync(request, caller));
    }

    public async Task AddDeviceAsync(GateDeviceSaveRequest request)
    {
        var caller = await CallerAsync();
        await MapValidationAsync(() => policies.SaveDeviceAsync(request, caller));
    }

    public async Task DeleteDeviceAsync(int id) => await policies.DeleteDeviceAsync(id, await CallerAsync());

    public async Task<IReadOnlyList<GateLogDto>> LogsAsync(GateLogFilter filter) =>
        await access.LogsAsync(await CallerAsync(), filter);

    public async Task<IReadOnlyList<StayReportDto>> StaysAsync(StayReportStatus? status = null) =>
        await stays.ListAsync(await CallerAsync(), status);

    public async Task<int> RefreshStaysAsync() => await stays.RefreshOverdueAsync(await CallerAsync());

    public async Task MarkReportedAsync(int id, StayReportChannel channel, string? evidenceRef, string? note) =>
        Ensure(await stays.MarkReportedAsync(id, channel, evidenceRef, note, await CallerAsync()));

    public async Task CancelStayAsync(int id, string reason) =>
        Ensure(await stays.CancelAsync(id, reason, await CallerAsync()));

    public async Task<GateVerifyResult> VerifyAsync(GateVerifyRequest request) =>
        await access.VerifyAndRecordAsync(request, await CallerAsync());

    public async Task<GateVerifyResult> ManualOpenAsync(int propertyId, int? gateDeviceId, string reason) =>
        await access.ManualOpenAsync(propertyId, gateDeviceId, reason, await CallerAsync());

    public async Task<IReadOnlyList<BiometricEnrollmentDto>> EnrollmentsAsync() =>
        await biometrics.ListAsync(await CallerAsync());

    public async Task<int> EnrollFaceAsync(string templateRef) =>
        await biometrics.EnrollAsync(BiometricMethod.Face, templateRef, null, await CallerAsync());

    public async Task<int> EnrollPasskeyAsync(string publicKey) =>
        await biometrics.EnrollAsync(BiometricMethod.Passkey, null, publicKey, await CallerAsync());

    public async Task ApproveEnrollmentAsync(int id)
    {
        if (!await biometrics.ApproveAsync(id, await CallerAsync()))
        {
            throw new GatePageException(GateErrorCodes.InvalidStatus);
        }
    }

    public async Task RejectEnrollmentAsync(int id, string reason)
    {
        if (!await biometrics.RejectAsync(id, reason, await CallerAsync()))
        {
            throw new GatePageException(GateErrorCodes.InvalidStatus);
        }
    }

    public async Task RevokeEnrollmentAsync(int id, string reason)
    {
        if (!await biometrics.RevokeAsync(id, reason, await CallerAsync()))
        {
            throw new GatePageException(GateErrorCodes.InvalidStatus);
        }
    }

    // Lỗi validate của service ném ArgumentException/InvalidOperationException; đổi sang mã
    // nghiệp vụ để trang dịch được thay vì rơi vào thông báo "tải thất bại" chung.
    private static async Task MapValidationAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new GatePageException(GateErrorCodes.InvalidRequest);
        }
    }

    private static void Ensure(GatePassResult result)
    {
        if (!result.Succeeded) throw new GatePageException(result.ErrorCode ?? GateErrorCodes.SaveFailed);
    }

    private static void Ensure(StayReportResult result)
    {
        if (!result.Succeeded) throw new GatePageException(result.ErrorCode ?? GateErrorCodes.SaveFailed);
    }
}

public sealed class GatePageException(string message) : Exception(message);
