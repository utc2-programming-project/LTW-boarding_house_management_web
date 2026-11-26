using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Enums;
using Web.Features.Accounts;
using Web.Features.Properties;

namespace Web.Features.AccessControl.Services;

/// <summary>Adapter UI: giữ caller lấy từ principal, không nhận userId/role từ client.</summary>
public sealed class GatePageService(
    AuthenticationStateProvider authentication,
    IGatePassService passes,
    IGatePolicyService policies,
    IGateAccessService access,
    IStayReportService stays,
    IBiometricEnrollmentService biometrics,
    IGateTokenService tokens,
    TimeProvider clock)
{
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

    public async Task<IReadOnlyList<GatePropertyOption>> PropertiesAsync() => await policies.PropertiesAsync(await CallerAsync());

    public async Task<(GatePolicyDto Policy, IReadOnlyList<GateDeviceDto> Devices)> SettingsAsync(int propertyId) =>
        (await policies.GetAsync(propertyId, await CallerAsync()),
         await policies.DevicesAsync(propertyId, await CallerAsync()));

    public async Task SavePolicyAsync(GatePolicySaveRequest request) =>
        await policies.SaveAsync(request, await CallerAsync());

    public async Task AddDeviceAsync(GateDeviceSaveRequest request) =>
        await policies.SaveDeviceAsync(request, await CallerAsync());

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
