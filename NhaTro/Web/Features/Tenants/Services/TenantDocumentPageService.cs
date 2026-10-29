using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Enums;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Entities;

namespace Web.Features.Tenants.Services;

/// <summary>
/// Adapter cho UI Blazor Server của phân hệ hồ sơ giấy tờ (VHT-26/2B) và phòng đang thuê (VHT-25/2D).
/// </summary>
public sealed class TenantDocumentPageService(
    AuthenticationStateProvider authenticationState,
    ITenantDocumentService documentService,
    ApplicationDbContext db)
{
    public const long MaxUploadFileSizeBytes = 10L * 1024 * 1024; // 10 MB

    public static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];

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

        if (browserFile.Size > MaxUploadFileSizeBytes)
        {
            return UploadResult.Failure(
                StorageErrorCodes.FileSizeExceeded,
                "Kích thước tệp vượt quá giới hạn 10 MB cho phép.");
        }

        if (browserFile.Size == 0)
        {
            return UploadResult.Failure(
                StorageErrorCodes.EmptyFile,
                "Tệp không có nội dung.");
        }

        var ext = Path.GetExtension(browserFile.Name).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return UploadResult.Failure(
                StorageErrorCodes.InvalidFileType,
                "Chỉ chấp nhận các tệp định dạng PDF, JPG, JPEG hoặc PNG.");
        }

        // Đọc stream từ IBrowserFile và wrap vào IFormFile
        var memoryStream = new MemoryStream();
        await using (var readStream = browserFile.OpenReadStream(MaxUploadFileSizeBytes, ct))
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

    public async Task<List<TenantPickerItemDto>> GetAccessibleTenantsAsync(CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        if (caller.IsAdmin || (!caller.IsChuTro && !caller.IsQuanLy))
        {
            return [];
        }

        // Chủ trọ chỉ chọn khách thuê có hợp đồng tại nhà thuộc sở hữu của mình.
        // Chưa có nguồn danh sách phân công Quản lý trong VHT-24 nên vai trò này mặc định không thấy hồ sơ.
        var query = from tenant in db.Tenants.AsNoTracking()
                    join member in db.ContractMembers.AsNoTracking() on tenant.Id equals member.TenantId
                    join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                    join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                    join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                    where !tenant.IsDeleted && tenant.IsActive
                        && !property.IsDeleted
                        && caller.IsChuTro && property.OwnerId == caller.UserId
                    select tenant;

        return await query
            .Distinct()
            .OrderBy(t => t.FullName)
            .Take(100)
            .Select(t => new TenantPickerItemDto
            {
                Id = t.Id,
                FullName = t.FullName,
                PhoneNumber = t.Phone,
                IdNumberMasked = null,
                HasActiveContract = false,
            })
            .ToListAsync(ct);
    }

    // TODO (2D): Move to separate service in phase 2D.
    /// <summary>
    /// Lấy phòng đang thuê qua liên kết hợp đồng thật của khách thuê hiện tại (không suy từ Room.Status).
    /// </summary>
    public async Task<LeasedRoomDetailDto?> GetMyLeasedRoomAsync(CancellationToken ct = default)
    {
        var caller = await GetCallerAsync();
        if (!caller.IsTenant || string.IsNullOrWhiteSpace(caller.UserId))
        {
            return null;
        }

        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == caller.UserId && !t.IsDeleted, ct);

        if (tenant is null)
        {
            return null;
        }

        // Tìm hợp đồng của tenant qua ContractMember
        var contractMemberQuery = db.ContractMembers.AsNoTracking()
            .Where(cm => cm.TenantId == tenant.Id);

        var contractIds = await contractMemberQuery.Select(cm => cm.ContractId).ToListAsync(ct);
        if (contractIds.Count == 0)
        {
            return null;
        }

        var contracts = await db.Contracts.AsNoTracking()
            .Where(c => contractIds.Contains(c.ContractId))
            .OrderByDescending(c => c.EndDate)
            .ToListAsync(ct);

        // Ưu tiên hợp đồng Approved (đã duyệt/hiệu lực) hoặc Pending gần nhất
        var contract = contracts.FirstOrDefault(c => c.Status == ContractStatus.Approved)
            ?? contracts.FirstOrDefault(c => c.Status == ContractStatus.Pending)
            ?? contracts.FirstOrDefault();

        if (contract is null)
        {
            return null;
        }

        var room = await db.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == contract.RoomId, ct);

        if (room is null)
        {
            return null;
        }

        var property = await db.Properties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == room.PropertyId, ct);

        // Lấy bạn cùng phòng
        var memberTenantIds = await db.ContractMembers.AsNoTracking()
            .Where(cm => cm.ContractId == contract.ContractId)
            .Select(cm => cm.TenantId)
            .ToListAsync(ct);

        var memberTenants = await db.Tenants.AsNoTracking()
            .Where(t => memberTenantIds.Contains(t.Id))
            .ToListAsync(ct);

        var roommates = memberTenants.Select(m => new RoommateDto
        {
            TenantId = m.Id,
            FullName = m.FullName,
            PhoneNumber = m.Phone,
            IsCurrentUser = m.Id == tenant.Id
        }).ToList();

        var statusLabel = contract.Status switch
        {
            ContractStatus.Approved => "Đang hiệu lực",
            ContractStatus.Pending => "Chờ duyệt",
            ContractStatus.Rejected => "Đã từ chối",
            _ => contract.Status.ToString()
        };

        return new LeasedRoomDetailDto
        {
            ContractId = contract.ContractId,
            ContractNumber = contract.ContractNumber,
            RoomId = room.Id,
            RoomCode = room.Code,
            Floor = room.Floor,
            AreaM2 = room.AreaM2,
            PropertyName = property?.Name ?? "Nhà trọ",
            PropertyAddress = property?.Address,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MonthlyRent = contract.SnapshotPrice,
            DepositAmount = contract.DepositAmount,
            StatusLabel = statusLabel,
            IsActive = contract.Status == ContractStatus.Approved,
            Roommates = roommates
        };
    }
}

public sealed record TenantDocumentPageResult(
    Tenant? Tenant,
    TenantDocumentListDto? Documents,
    bool NoTenantLinked);
