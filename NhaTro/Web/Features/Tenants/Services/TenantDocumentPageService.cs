using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Entities;
using Web.Features.Contracts.Enums;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Adapter cho UI Blazor Server của phân hệ hồ sơ giấy tờ (VHT-26/2B) và phòng đang thuê (VHT-25/2D).
/// </summary>
public sealed class TenantDocumentPageService(
    AuthenticationStateProvider authenticationState,
    ITenantDocumentService documentService,
    ApplicationDbContext db,
    TenantPageService? tenantPageService = null)
{
    public async Task<string> GetCurrentRoleAsync()
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;
        return Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
    }

    public async Task<TenantCaller> GetCallerAsync()
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var role = Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
        return new TenantCaller(userId, role);
    }

    public async Task<Tenant?> GetCurrentTenantAsync(CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        if (!caller.IsTenant || string.IsNullOrWhiteSpace(caller.UserId))
        {
            return null;
        }

        return await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == caller.UserId && !t.IsDeleted, ct);
    }

    public async Task<TenantDocumentListDto?> GetDocumentsAsync(int tenantId, CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        return await documentService.ListAsync(tenantId, caller, ct);
    }

    public async Task<TenantDocumentPageResult> GetMyDocumentPageAsync(CancellationToken ct = default)
    {
        var tenant = await GetCurrentTenantAsync(ct);
        if (tenant is null)
        {
            return new TenantDocumentPageResult(null, null, NoTenantLinked: true);
        }

        var documents = await GetDocumentsAsync(tenant.Id, ct);
        return new TenantDocumentPageResult(tenant, documents, NoTenantLinked: false);
    }

    public async Task<UploadResult> UploadDocumentAsync(
        int tenantId,
        IBrowserFile browserFile,
        string documentType,
        string? description,
        CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();

        // [VHT-36-2B] Removed duplicate file validation (size, extension). Handled by Backend Service
        // [VHT-36-2B] Removed hardcoded Vietnamese messages. Returning ErrorCodes for UI i18n mapping
        // Đọc stream từ IBrowserFile và wrap vào IFormFile
        var memoryStream = new MemoryStream();
        await using (var readStream = browserFile.OpenReadStream(browserFile.Size, ct))
        {
            await readStream.CopyToAsync(memoryStream, ct);
        }
        memoryStream.Position = 0;

        var formFile = new FormFile(memoryStream, 0, memoryStream.Length, "file", browserFile.Name)
        {
            Headers = new HeaderDictionary(),
            ContentType = string.IsNullOrWhiteSpace(browserFile.ContentType) ? "application/octet-stream" : browserFile.ContentType
        };

        var request = new UploadFileRequest
        {
            File = formFile,
            FileCategory = FileCategory.IdentityDocument,
            RelatedEntityType = RelatedEntityType.Tenant,
            RelatedEntityId = tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DocumentType = documentType,
            Description = description,
        };

        return await documentService.UploadAsync(tenantId, request, caller, ct);
    }

    public async Task<bool> DeleteDocumentAsync(int tenantId, Guid fileId, CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        return await documentService.DeleteAsync(tenantId, fileId, caller, ct);
    }

    public async Task<FileDownloadResponse?> DownloadDocumentAsync(int tenantId, Guid fileId, CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        return await documentService.DownloadAsync(tenantId, fileId, caller, ct);
    }

    // [VHT-36-2B] Removed GetAccessibleTenantsAsync (Dead code, logic cũ đã được thay thế bởi TenantScopeService)

    // TODO (2D): Move to separate service in phase 2D.
    /// <summary>
    /// Lấy phòng đang thuê qua liên kết hợp đồng thật của khách thuê hiện tại (không suy từ Room.Status).
    /// </summary>
    public async Task<LeasedRoomDetailDto?> GetMyLeasedRoomAsync(CancellationToken ct = default)
    {
        if (tenantPageService is null)
        {
            return null;
        }

        var result = await tenantPageService.GetOwnRentalOverviewAsync(ct);
        var overview = result.Succeeded ? result.Rental : null;
        var rental = overview?.CurrentRental;
        if (rental is null)
        {
            return null;
        }

        return new LeasedRoomDetailDto
        {
            ContractId = rental.ContractId,
            ContractNumber = rental.ContractNumber,
            RoomId = rental.RoomId,
            RoomCode = rental.RoomCode,
            Floor = null,
            AreaM2 = rental.AreaM2,
            PropertyName = rental.PropertyName,
            PropertyAddress = rental.PropertyAddress,
            StartDate = rental.StartDate.ToDateTime(TimeOnly.MinValue),
            EndDate = rental.EndDate.ToDateTime(TimeOnly.MinValue),
            MonthlyRent = rental.SnapshotPrice,
            DepositAmount = rental.DepositAmount,
            StatusLabel = rental.Status.ToString(),
            IsActive = rental.IsCurrent,
            Roommates = rental.Roommates.Select(roommate => new RoommateDto
            {
                TenantId = roommate.TenantId,
                FullName = roommate.FullName,
                PhoneNumber = roommate.PhoneNumber,
                IsCurrentUser = roommate.TenantId == overview!.TenantId,
            }).ToList()
        };
    }
}

public sealed record TenantDocumentPageResult(
    Tenant? Tenant,
    TenantDocumentListDto? Documents,
    bool NoTenantLinked);
