using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Features.Accounts;
using Web.Features.Accounts.Owners;
using Web.Features.Properties;
using Web.Features.Tenants.Invitations;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Enums;
using Web.Infrastructure.Storage;

namespace Web.Features.Vehicles.Services;

/// <summary>
/// Adapter UI cho các trang /vehicles/* và /my/vehicles:
/// lấy PropertyCaller từ AuthenticationStateProvider (server-side, không tin client)
/// và tuần tự hóa các lời gọi DB trên cùng circuit bằng SemaphoreSlim để tránh lỗi
/// EF Core "A second operation was started on this context instance".
/// </summary>
public sealed class VehiclePageService(
    AuthenticationStateProvider authentication,
    IVehicleSettingService settings,
    IVehicleService vehicles,
    IParkingZoneService zones,
    IVehicleBillingService billing,
    IBlockingAlertService blockingAlerts,
    IGuestVehicleNoticeService guestNotices,
    IVehicleAuditService audits,
    QrCodeRenderer qrRenderer,
    OwnerAccessGuard ownerAccess,
    IFileStorage fileStorage) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public void Dispose() => _gate.Dispose();

    private async Task<T> RunExclusiveAsync<T>(Func<Task<T>> action)
    {
        await _gate.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunExclusiveAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task<T> RunGuardedAsync<T>(Func<Task<T>> action) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await action();
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public async Task<PropertyCaller> CallerAsync(CancellationToken ct = default)
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var role = Roles.ResolvePrimaryRole(user);
        if (role is not (Roles.ChuTro or Roles.QuanLy or Roles.KhachThue))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var caller = new PropertyCaller(userId, role);
        // Đồng bộ với API: Quản lý/Khách thuê phải loại dữ liệu của chủ trọ đang bị khoá.
        if (role is Roles.QuanLy or Roles.KhachThue)
        {
            var blocked = await ownerAccess.GetBlockedOwnerIdsAsync(ct);
            if (blocked.Count > 0)
            {
                caller = caller with { BlockedOwnerIds = blocked.ToList() };
            }
        }

        return caller;
    }

    public string RenderQrDataUrl(string token) => qrRenderer.ToPngDataUrl(token);

    /// <summary>
    /// Lưu ảnh xe (tùy chọn) vào kho tệp ngoài wwwroot, trả đường dẫn tương đối để lưu vào PhotoRef.
    /// </summary>
    public Task<string> UploadVehiclePhotoAsync(byte[] content, string extension) =>
        RunExclusiveAsync(async () =>
        {
            await CallerAsync();

            var ext = extension.ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            {
                throw new VehiclePageException(VehicleErrorCodes.PhotoInvalid);
            }

            if (content.Length == 0 || content.Length > 2 * 1024 * 1024)
            {
                throw new VehiclePageException(VehicleErrorCodes.PhotoInvalid);
            }

            var saved = await fileStorage.SaveAsync(content, ext, DateTimeOffset.UtcNow);
            return saved.RelativePath;
        });

    /// <summary>Đọc ảnh xe đã lưu và trả về data URL để hiển thị trong form (null nếu chưa có).</summary>
    public Task<string?> GetVehiclePhotoDataUrlAsync(string? relativePath) =>
        RunExclusiveAsync(async () =>
        {
            await CallerAsync();

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            await using var stream = fileStorage.TryOpenRead(relativePath);
            if (stream is null)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            var contentType = Path.GetExtension(relativePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg",
            };
            return $"data:{contentType};base64,{Convert.ToBase64String(buffer.ToArray())}";
        });

    public Task<IReadOnlyList<VehiclePropertyOptionDto>> PropertiesAsync() =>
        RunGuardedAsync(async () => await settings.PropertiesAsync(await CallerAsync()));

    public Task<VehiclePropertySettingDto> GetSettingAsync(int propertyId) =>
        RunGuardedAsync(async () => await settings.GetOrCreateAsync(propertyId, await CallerAsync()));

    public Task<VehiclePropertySettingDto> SaveSettingAsync(VehiclePropertySettingSaveRequest request) =>
        RunGuardedAsync(async () => await settings.SaveAsync(request, await CallerAsync()));

    public Task<IReadOnlyList<VehicleRoomOptionDto>> RoomsAsync(int? propertyId = null) =>
        RunGuardedAsync(async () => await vehicles.RoomsWithActiveContractAsync(propertyId, await CallerAsync()));

    public Task<VehicleListResultDto> ListVehiclesAsync(VehicleFilter filter) =>
        RunGuardedAsync(async () => await vehicles.ListAsync(filter, await CallerAsync()));

    public Task<IReadOnlyList<VehicleLookupItemDto>> LookupAsync(int? propertyId, string query) =>
        RunGuardedAsync(async () => await vehicles.LookupAsync(propertyId, query, await CallerAsync()));

    public Task<VehicleDto> CreateVehicleAsync(VehicleCreateRequest request) =>
        RunExclusiveAsync(async () =>
        {
            var result = await vehicles.CreateAsync(request, await CallerAsync());
            if (!result.Succeeded || result.Vehicle is null)
            {
                throw new VehiclePageException(result.ErrorCode ?? VehicleErrorCodes.SaveFailed);
            }

            return result.Vehicle;
        });

    public Task<VehicleDto> UpdateVehicleAsync(VehicleUpdateRequest request) =>
        RunExclusiveAsync(async () =>
        {
            var result = await vehicles.UpdateAsync(request, await CallerAsync());
            if (!result.Succeeded || result.Vehicle is null)
            {
                throw new VehiclePageException(result.ErrorCode ?? VehicleErrorCodes.SaveFailed);
            }

            return result.Vehicle;
        });

    public Task<VehicleDto> DeactivateVehicleAsync(int vehicleId, string reason) =>
        RunExclusiveAsync(async () =>
        {
            var result = await vehicles.DeactivateAsync(vehicleId, reason, await CallerAsync());
            if (!result.Succeeded || result.Vehicle is null)
            {
                throw new VehiclePageException(result.ErrorCode ?? VehicleErrorCodes.SaveFailed);
            }

            return result.Vehicle;
        });

    public Task<VehicleDto> ReactivateVehicleAsync(int vehicleId, int? zoneId = null, int? slotId = null) =>
        RunExclusiveAsync(async () =>
        {
            var result = await vehicles.ReactivateAsync(vehicleId, zoneId, slotId, await CallerAsync());
            if (!result.Succeeded || result.Vehicle is null)
            {
                throw new VehiclePageException(result.ErrorCode ?? VehicleErrorCodes.SaveFailed);
            }

            return result.Vehicle;
        });

    public Task<int> ReportIncidentAsync(VehicleIncidentCreateRequest request) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await vehicles.ReportVehicleIncidentAsync(request, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<IReadOnlyList<ParkingZoneDto>> ListZonesAsync(int propertyId) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await zones.ListZonesAsync(propertyId, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<ParkingZoneDto> SaveZoneAsync(ParkingZoneSaveRequest request) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await zones.SaveZoneAsync(request, await CallerAsync());
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
        });

    public Task DeleteZoneAsync(int zoneId) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                await zones.DeleteZoneAsync(zoneId, await CallerAsync());
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
        });

    public Task<IReadOnlyList<ParkingSlotDto>> GenerateSlotsAsync(int zoneId, string prefix, int count) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await zones.GenerateSlotsAsync(zoneId, prefix, count, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task SetSlotStatusAsync(int slotId, ParkingSlotStatus status) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                await zones.SetSlotStatusAsync(slotId, status, await CallerAsync());
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<int> SyncPropertyDraftInvoicesAsync(int propertyId) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await billing.SyncPropertyDraftInvoicesAsync(propertyId, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<BlockingAlertResult> SendBlockingAlertAsync(BlockingAlertRequest request) =>
        RunExclusiveAsync(async () =>
        {
            var result = await blockingAlerts.SendBlockingAlertAsync(request, await CallerAsync());
            if (!result.Succeeded)
            {
                throw new VehiclePageException(result.ErrorCode ?? VehicleErrorCodes.SaveFailed);
            }

            return result;
        });

    public Task<IReadOnlyList<GuestVehicleNoticeDto>> ListGuestNoticesAsync(int? propertyId = null, GuestVehicleNoticeStatus? status = null) =>
        RunGuardedAsync(async () => await guestNotices.ListAsync(propertyId, status, await CallerAsync()));

    public Task<GuestVehicleNoticeDto> CreateGuestNoticeAsync(GuestVehicleNoticeCreateRequest request) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await guestNotices.CreateAsync(request, await CallerAsync());
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
        });

    public Task UpdateGuestNoticeStatusAsync(int noticeId, GuestVehicleNoticeStatus status) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                await guestNotices.UpdateStatusAsync(noticeId, status, await CallerAsync());
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<IReadOnlyList<VehicleAuditSessionDto>> ListAuditSessionsAsync(int propertyId) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await audits.ListSessionsAsync(propertyId, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<VehicleAuditSessionDto> StartAuditSessionAsync(int propertyId, string? note = null) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await audits.StartSessionAsync(propertyId, note, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<VehicleAuditSessionDto> RecordAuditItemAsync(int itemId, VehicleAuditItemResult result, string? note = null) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await audits.RecordItemResultAsync(itemId, result, note, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<VehicleAuditSessionDto> QuickAddVehicleDuringAuditAsync(int sessionId, VehicleCreateRequest request) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await audits.QuickAddVehicleAsync(sessionId, request, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });

    public Task<VehicleAuditSessionDto> CompleteAuditSessionAsync(int sessionId, string? note = null) =>
        RunExclusiveAsync(async () =>
        {
            try
            {
                return await audits.CompleteSessionAsync(sessionId, note, await CallerAsync());
            }
            catch (UnauthorizedAccessException)
            {
                throw new VehiclePageException(VehicleErrorCodes.Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                throw new VehiclePageException(ex.Message);
            }
        });
}

public sealed class VehiclePageException(string message) : Exception(message);
