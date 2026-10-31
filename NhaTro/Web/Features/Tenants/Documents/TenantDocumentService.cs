using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;
using Web.Features.Storage.Services;
using Web.Features.Tenants.DTOs;

namespace Web.Features.Tenants.Documents;

/// <summary>
/// VHT-26: lớp nghiệp vụ giấy tờ P3. Toàn bộ tệp giấy tờ dùng <c>FileCategory.IdentityDocument</c>
/// gắn với <c>RelatedEntityType.Tenant</c>; service này bổ sung phạm vi hồ sơ, quy tắc thay/xóa
/// và audit. Tầng storage dùng chung vẫn kiểm quyền lần hai khi tải trực tiếp <c>/api/files/{id}</c>.
/// </summary>
public sealed class TenantDocumentService(
    ApplicationDbContext db,
    FileService fileService,
    ITenantDocumentScopeService scopeService,
    IOptions<StorageOptions> storageOptions,
    IFileAccessAuditWriter audit,
    ILogger<TenantDocumentService> logger) : ITenantDocumentService
{
    private static readonly RelatedEntityType TenantEntity = RelatedEntityType.Tenant;
    private static readonly FileCategory IdentityDocument = FileCategory.IdentityDocument;

    public async Task<TenantDocumentListDto?> ListAsync(
        int tenantId,
        TenantCaller caller,
        CancellationToken cancellationToken = default)
    {
        var access = await scopeService.ResolveAsync(tenantId, caller, cancellationToken);
        if (!TenantDocumentPermissionRules.CanView(access))
        {
            await AuditAsync(caller, FileAccessAction.List, tenantId, null, false,
                StorageErrorCodes.FileNotFound, cancellationToken);
            return null;
        }

        var files = await fileService.ListAsync(
            TenantEntity,
            TenantId(tenantId),
            ToStorageCaller(caller),
            cancellationToken);

        var options = storageOptions.Value;
        var maxBytes = StorageLimits.MaxBytes(options, IdentityDocument);

        return new TenantDocumentListDto
        {
            TenantId = tenantId,
            Items = files
                .Where(file => file.FileCategory == IdentityDocument)
                .Select(ToMetadata)
                .ToList(),
            CanUpload = TenantDocumentPermissionRules.CanUpload(access),
            CanDownload = TenantDocumentPermissionRules.CanDownload(access),
            CanDelete = TenantDocumentPermissionRules.CanDelete(access),
            CanReplace = TenantDocumentPermissionRules.CanReplace(access),
            CanExport = TenantDocumentPermissionRules.CanExport(access),
            MaxFileSizeBytes = maxBytes,
            AllowedExtensions = FileTypeRules.AllowedExtensions(IdentityDocument),
        };
    }

    public async Task<UploadResult> UploadAsync(
        int tenantId,
        UploadFileRequest request,
        TenantCaller caller,
        CancellationToken cancellationToken = default)
    {
        var access = await scopeService.ResolveAsync(tenantId, caller, cancellationToken);
        if (!TenantDocumentPermissionRules.CanUpload(access))
        {
            await AuditAsync(caller, FileAccessAction.Upload, tenantId, null, false,
                StorageErrorCodes.Forbidden, cancellationToken);
            return ForbiddenUpload();
        }

        var documentType = TenantDocumentTypeRules.NormalizeTypeCode(request.DocumentType);
        if (documentType is null)
        {
            await AuditAsync(caller, FileAccessAction.Upload, tenantId, null, false,
                StorageErrorCodes.InvalidRequest, cancellationToken);
            return InvalidRequest();
        }

        var duplicateExists = await HasActiveDocumentTypeAsync(tenantId, documentType, cancellationToken);
        if (duplicateExists)
        {
            await AuditAsync(caller, FileAccessAction.Upload, tenantId, null, false,
                StorageErrorCodes.DuplicateDocumentType, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.DuplicateDocumentType,
                "Loại giấy tờ này đã có tệp. Hãy xóa tệp cũ trước khi tải tệp mới.");
        }

        var normalized = NormalizeForTenant(tenantId, request);
        if (normalized is null)
        {
            await AuditAsync(caller, FileAccessAction.Upload, tenantId, null, false,
                StorageErrorCodes.InvalidRequest, cancellationToken);
            return InvalidRequest();
        }

        return await fileService.UploadAsync(normalized, ToStorageCaller(caller), cancellationToken);
    }

    public async Task<UploadResult> ReplaceAsync(
        int tenantId,
        Guid fileId,
        UploadFileRequest request,
        TenantCaller caller,
        CancellationToken cancellationToken = default)
    {
        var access = await scopeService.ResolveAsync(tenantId, caller, cancellationToken);
        if (!TenantDocumentPermissionRules.CanReplace(access))
        {
            var errorCode = access == TenantDocumentAccessLevel.None
                ? StorageErrorCodes.FileNotFound
                : StorageErrorCodes.Forbidden;
            await AuditAsync(caller, FileAccessAction.Replace, tenantId, fileId, false, errorCode, cancellationToken);
            return access == TenantDocumentAccessLevel.None ? FileNotFound() : ForbiddenUpload();
        }

        if (!await IsTenantFileAsync(tenantId, fileId, cancellationToken))
        {
            await AuditAsync(caller, FileAccessAction.Replace, tenantId, fileId, false,
                StorageErrorCodes.FileNotFound, cancellationToken);
            return FileNotFound();
        }

        var normalized = NormalizeForTenant(tenantId, request);
        if (normalized is null)
        {
            await AuditAsync(caller, FileAccessAction.Replace, tenantId, fileId, false,
                StorageErrorCodes.InvalidRequest, cancellationToken);
            return InvalidRequest();
        }

        UploadResult uploaded;
        try
        {
            uploaded = await fileService.UploadAsync(normalized, ToStorageCaller(caller), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Lỗi khi upload tệp thay thế {OldFileId} của khách thuê {TenantId}.", fileId, tenantId);
            await AuditAsync(caller, FileAccessAction.Replace, tenantId, fileId, false,
                StorageErrorCodes.UploadFailed, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.UploadFailed,
                "Không thể thay tệp, vui lòng thử lại.");
        }

        if (!uploaded.Succeeded || uploaded.File is null)
        {
            await AuditAsync(caller, FileAccessAction.Replace, tenantId, fileId, false,
                uploaded.ErrorCode ?? StorageErrorCodes.UploadFailed, cancellationToken);
            return uploaded;
        }

        // Upload mới thành công rồi mới xóa mềm tệp cũ: không mất liên kết nếu upload lỗi.
        bool deletedOld;
        try
        {
            deletedOld = await fileService.DeleteAsync(fileId, ToStorageCaller(caller), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Lỗi khi xóa mềm tệp cũ {OldFileId} của khách thuê {TenantId}.", fileId, tenantId);
            deletedOld = false;
        }

        if (!deletedOld)
        {
            // Bù trừ: bỏ tệp mới để hồ sơ không còn hai tệp hiện hành. Tệp vật lý vẫn giữ theo
            // chính sách xóa mềm của storage, không tạo tệp mồ côi không có metadata.
            try
            {
                await fileService.DeleteAsync(uploaded.File.Id, ToStorageCaller(caller), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Không bù trừ được tệp mới {NewFileId} sau khi thay tệp thất bại.", uploaded.File.Id);
            }

            await AuditAsync(caller, FileAccessAction.Replace, tenantId, fileId, false,
                StorageErrorCodes.UploadFailed, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.UploadFailed,
                "Không thể thay tệp, vui lòng thử lại.");
        }

        await AuditAsync(caller, FileAccessAction.Replace, tenantId, uploaded.File.Id, true,
            null, cancellationToken);
        return uploaded;
    }

    public async Task<FileDownloadResponse?> DownloadAsync(
        int tenantId,
        Guid fileId,
        TenantCaller caller,
        CancellationToken cancellationToken = default)
    {
        // Kiểm tệp thuộc đúng hồ sơ trước; sau đó storage kiểm phạm vi lần nữa cho đường tải chung.
        var access = await scopeService.ResolveAsync(tenantId, caller, cancellationToken);
        if (!TenantDocumentPermissionRules.CanDownload(access)
            || !await IsTenantFileAsync(tenantId, fileId, cancellationToken))
        {
            await AuditAsync(caller, FileAccessAction.Download, tenantId, fileId, false,
                StorageErrorCodes.FileNotFound, cancellationToken);
            return null;
        }

        return await fileService.DownloadAsync(fileId, ToStorageCaller(caller), cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        int tenantId,
        Guid fileId,
        TenantCaller caller,
        CancellationToken cancellationToken = default)
    {
        var access = await scopeService.ResolveAsync(tenantId, caller, cancellationToken);
        if (!TenantDocumentPermissionRules.CanDelete(access)
            || !await IsTenantFileUploadedByCallerAsync(tenantId, fileId, caller.UserId, cancellationToken))
        {
            await AuditAsync(caller, FileAccessAction.Delete, tenantId, fileId, false,
                StorageErrorCodes.FileNotFound, cancellationToken);
            return false;
        }

        return await fileService.DeleteAsync(fileId, ToStorageCaller(caller), cancellationToken);
    }

    private async Task<bool> IsTenantFileAsync(
        int tenantId,
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var tenantIdText = TenantId(tenantId);
        return await db.StoredFiles.AsNoTracking().AnyAsync(
            f => f.Id == fileId
                && f.FileCategory == IdentityDocument
                && f.RelatedEntityType == TenantEntity
                && f.RelatedEntityId == tenantIdText,
            cancellationToken);
    }

    private async Task<bool> IsTenantFileUploadedByCallerAsync(
        int tenantId,
        Guid fileId,
        string callerUserId,
        CancellationToken cancellationToken)
    {
        var tenantIdText = TenantId(tenantId);
        return await db.StoredFiles.AsNoTracking().AnyAsync(
            f => f.Id == fileId
                && f.FileCategory == IdentityDocument
                && f.RelatedEntityType == TenantEntity
                && f.RelatedEntityId == tenantIdText
                && f.UploadedByUserId == callerUserId,
            cancellationToken);
    }

    private async Task<bool> HasActiveDocumentTypeAsync(
        int tenantId,
        string documentType,
        CancellationToken cancellationToken)
    {
        var tenantIdText = TenantId(tenantId);
        var descriptions = await db.StoredFiles.AsNoTracking()
            .Where(file => !file.IsDeleted
                && file.FileCategory == IdentityDocument
                && file.RelatedEntityType == TenantEntity
                && file.RelatedEntityId == tenantIdText)
            .Select(file => file.Description)
            .ToListAsync(cancellationToken);

        return descriptions.Any(description => TenantDocumentTypeRules.DescriptionMatches(description, documentType));
    }

    // Ép mọi yêu cầu qua service P3 đúng loại tệp/đối tượng; không cho client đổi relatedEntityId
    // sang hồ sơ khác để vượt kiểm phạm vi.
    private static UploadFileRequest? NormalizeForTenant(int tenantId, UploadFileRequest request)
    {
        var documentType = TenantDocumentTypeRules.NormalizeTypeCode(request.DocumentType);
        if (request.File is null
            || request.FileCategory != IdentityDocument
            || request.RelatedEntityType != TenantEntity
            || documentType is null)
        {
            return null;
        }

        var expectedId = TenantId(tenantId);
        var actualId = request.RelatedEntityId?.Trim();
        if (!string.IsNullOrEmpty(actualId) && actualId != expectedId)
        {
            return null;
        }

        return new UploadFileRequest
        {
            File = request.File,
            FileCategory = IdentityDocument,
            RelatedEntityId = expectedId,
            RelatedEntityType = TenantEntity,
            DocumentType = documentType,
            Description = TenantDocumentTypeRules.BuildDescription(documentType, request.Description),
        };
    }

    private Task AuditAsync(
        TenantCaller caller,
        FileAccessAction action,
        int tenantId,
        Guid? fileId,
        bool succeeded,
        string? errorCode,
        CancellationToken cancellationToken) =>
        audit.WriteAsync(
            new FileAccessAuditEntry(
                caller.UserId,
                caller.Role,
                action,
                fileId,
                IdentityDocument,
                TenantEntity,
                TenantId(tenantId),
                succeeded,
                errorCode),
            cancellationToken);

    private static TenantDocumentMetadataDto ToMetadata(FileMetadataDto file) => new()
    {
        Id = file.Id,
        FileName = file.FileName,
        ContentType = file.ContentType,
        FileSizeBytes = file.FileSizeBytes,
        FileCategory = file.FileCategory,
        UploadedAt = file.UploadedAt,
        UploadedByUserId = file.UploadedByUserId,
        Description = file.Description,
    };

    private static StorageCaller ToStorageCaller(TenantCaller caller) =>
        new(caller.UserId, caller.Role);

    private static string TenantId(int tenantId) =>
        tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static UploadResult ForbiddenUpload() =>
        UploadResult.Failure(
            StorageErrorCodes.Forbidden,
            "Bạn không có quyền thao tác giấy tờ của hồ sơ này.");

    private static UploadResult FileNotFound() =>
        UploadResult.Failure(StorageErrorCodes.FileNotFound, "File không tồn tại hoặc đã bị xóa");

    private static UploadResult InvalidRequest() =>
        UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ.");
}
