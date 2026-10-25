using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Mapping;
using Web.Features.Storage.Rules;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;
using Web.Infrastructure.Storage;

namespace Web.Features.Storage.Services;

/// <summary>
/// Storage dùng chung (VHT-13): lưu/tải/xóa mềm tệp ngoài <c>wwwroot</c> kèm kiểm quyền.
/// Là nơi duy nhất quyết định quyền storage; endpoint chỉ chuyển tiếp kết quả.
/// VHT-26 bổ sung phạm vi giấy tờ P3 (theo hồ sơ khách thuê) và audit truy cập tệp.
/// </summary>
public sealed class FileService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IFileStorage fileStorage,
    IOptions<StorageOptions> options,
    TimeProvider timeProvider,
    ITenantDocumentScopeService tenantDocumentScope,
    IFileAccessAuditWriter audit,
    ILogger<FileService> logger)
{
    private StorageOptions Options => options.Value;

    /// <summary>Overload cho HTTP endpoint (ClaimsPrincipal của request).</summary>
    public Task<UploadResult> UploadAsync(
        UploadFileRequest request,
        IPrincipal user,
        CancellationToken cancellationToken = default) =>
        UploadAsync(request, StorageCaller.FromPrincipal(user), cancellationToken);

    /// <summary>
    /// Lưu tệp: kiểm quyền → kiểm loại/cặp đối tượng → đọc stream có giới hạn → kiểm nội dung thật
    /// → kiểm số tệp tối đa → ghi đĩa → lưu metadata. Tệp sai loại/quá giới hạn không được ghi đĩa.
    /// </summary>
    public async Task<UploadResult> UploadAsync(
        UploadFileRequest request,
        StorageCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureBusinessAllowed(caller);

        if (request.File is null
            || !Enum.IsDefined(request.FileCategory)
            || !Enum.IsDefined(request.RelatedEntityType))
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, null, null, null, false,
                StorageErrorCodes.InvalidRequest, cancellationToken);
            return UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ.");
        }

        if (!StoragePermissionRules.CanUpload(caller.Role, request.FileCategory))
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, request.RelatedEntityId, false,
                StorageErrorCodes.Forbidden, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.Forbidden,
                "Bạn không có quyền thực hiện thao tác này.");
        }

        if (!FileTypeRules.IsPairValid(request.FileCategory, request.RelatedEntityType))
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, request.RelatedEntityId, false,
                StorageErrorCodes.InvalidRequest, cancellationToken);
            return UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ.");
        }

        var relatedEntityId = request.RelatedEntityId?.Trim() ?? string.Empty;
        if (relatedEntityId.Length == 0 || relatedEntityId.Length > 450)
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, relatedEntityId, false,
                StorageErrorCodes.InvalidRequest, cancellationToken);
            return UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ.");
        }

        // Room/Tenant dùng khóa int: chuẩn hóa để "001" và "1" không tạo hai liên kết khác nhau.
        if (!TryCanonicalizeRelatedEntityId(request.RelatedEntityType, relatedEntityId, out relatedEntityId))
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, relatedEntityId, false,
                StorageErrorCodes.InvalidRequest, cancellationToken);
            return UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ.");
        }

        var extension = FileTypeRules.GetExtension(request.File.FileName);
        if (!FileTypeRules.IsExtensionAllowed(request.FileCategory, extension))
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, relatedEntityId, false,
                StorageErrorCodes.InvalidFileType, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.InvalidFileType,
                $"{FileTypeRules.DisplayName(request.FileCategory)} chỉ chấp nhận file {FileTypeRules.DescribeAllowedExtensions(request.FileCategory)}");
        }

        var maxBytes = StorageLimits.MaxBytes(Options, request.FileCategory);

        byte[] content;
        await using (var uploadStream = request.File.OpenReadStream())
        {
            try
            {
                content = await LimitedStreamReader.ReadAllBytesAsync(uploadStream, maxBytes, cancellationToken);
            }
            catch (StorageFileTooLargeException)
            {
                await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                    request.RelatedEntityType, relatedEntityId, false,
                    StorageErrorCodes.FileSizeExceeded, cancellationToken);
                return TooLarge(maxBytes);
            }
            catch (StorageEmptyFileException)
            {
                await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                    request.RelatedEntityType, relatedEntityId, false,
                    StorageErrorCodes.EmptyFile, cancellationToken);
                return UploadResult.Failure(StorageErrorCodes.EmptyFile, "File không có nội dung.");
            }
        }

        if (!FileContentSniffer.IsAllowedContent(request.FileCategory, extension, content))
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, relatedEntityId, false,
                StorageErrorCodes.InvalidFileType, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.InvalidFileType,
                $"{FileTypeRules.DisplayName(request.FileCategory)} chỉ chấp nhận file {FileTypeRules.DescribeAllowedExtensions(request.FileCategory)}");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (!await RelatedEntityExistsAsync(db, request.RelatedEntityType, relatedEntityId, cancellationToken))
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, relatedEntityId, false,
                StorageErrorCodes.RelatedEntityNotFound, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.RelatedEntityNotFound,
                "Không tìm thấy đối tượng liên quan.");
        }

        // Giấy tờ P3: phạm vi theo hồ sơ khách thuê (Admin đã bị chặn, Quản lý theo tòa nhà được giao).
        if (request.RelatedEntityType == RelatedEntityType.Tenant)
        {
            var access = await ResolveTenantAccessAsync(relatedEntityId, caller, cancellationToken);
            if (!TenantDocumentPermissionRules.CanUpload(access))
            {
                await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                    request.RelatedEntityType, relatedEntityId, false,
                    StorageErrorCodes.Forbidden, cancellationToken);
                return UploadResult.Failure(
                    StorageErrorCodes.Forbidden,
                    "Bạn không có quyền thao tác giấy tờ của hồ sơ này.");
            }
        }
        // Các loại khác: khách thuê chỉ được gắn tệp vào đối tượng thuộc phạm vi của mình.
        else if (caller.IsKhachThue
            && !await IsRelatedEntityInScopeAsync(db, request.RelatedEntityType, relatedEntityId, caller.UserId, cancellationToken))
        {
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, relatedEntityId, false,
                StorageErrorCodes.Forbidden, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.Forbidden,
                "Bạn không có quyền thực hiện thao tác này.");
        }

        var maxFiles = StorageLimits.MaxFilesPerEntity(Options, request.FileCategory);
        if (maxFiles is { } limit)
        {
            var currentCount = await db.StoredFiles.CountAsync(
                f => f.FileCategory == request.FileCategory
                    && f.RelatedEntityType == request.RelatedEntityType
                    && f.RelatedEntityId == relatedEntityId,
                cancellationToken);

            if (currentCount >= limit)
            {
                await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                    request.RelatedEntityType, relatedEntityId, false,
                    StorageErrorCodes.FileCountExceeded, cancellationToken);
                return UploadResult.Failure(
                    StorageErrorCodes.FileCountExceeded,
                    "Đã đạt số file tối đa cho mục này.");
            }
        }

        var uploadedAt = timeProvider.GetUtcNow();
        StoredFileWriteResult written;
        try
        {
            written = await fileStorage.SaveAsync(content, extension, uploadedAt, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Không ghi được tệp lên đĩa cho loại {FileCategory}.", request.FileCategory);
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, relatedEntityId, false,
                StorageErrorCodes.UploadFailed, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.UploadFailed,
                "Không thể tải lên, vui lòng thử lại.");
        }

        var stored = new StoredFile
        {
            Id = Guid.NewGuid(),
            FileName = SanitizeFileName(request.File.FileName, extension),
            StoredFileName = written.StoredFileName,
            RelativePath = written.RelativePath,
            ContentType = FileContentSniffer.DetectContentType(content) ?? FileTypeRules.ContentTypeForExtension(extension),
            FileSizeBytes = written.SizeBytes,
            FileCategory = request.FileCategory,
            UploadedByUserId = caller.UserId,
            UploadedAt = uploadedAt,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = request.RelatedEntityType,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        };

        try
        {
            db.StoredFiles.Add(stored);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Metadata không lưu được thì bỏ tệp vừa ghi để không còn tệp mồ côi trên đĩa.
            logger.LogError(ex, "Lưu metadata tệp thất bại, xóa tệp vừa ghi.");
            TryDeletePhysical(written.RelativePath);
            await AuditAsync(caller, FileAccessAction.Upload, null, request.FileCategory,
                request.RelatedEntityType, relatedEntityId, false,
                StorageErrorCodes.UploadFailed, cancellationToken);
            return UploadResult.Failure(
                StorageErrorCodes.UploadFailed,
                "Không thể tải lên, vui lòng thử lại.");
        }

        await AuditAsync(caller, FileAccessAction.Upload, stored.Id, stored.FileCategory,
            stored.RelatedEntityType, stored.RelatedEntityId, true, null, cancellationToken);

        return UploadResult.Success(StoredFileMapping.ToMetadata(stored));
    }

    /// <summary>Overload cho HTTP endpoint.</summary>
    public Task<FileDownloadResponse?> DownloadAsync(
        Guid id,
        IPrincipal user,
        CancellationToken cancellationToken = default) =>
        DownloadAsync(id, StorageCaller.FromPrincipal(user), cancellationToken);

    /// <summary>
    /// Tải tệp: kiểm quyền trước, chỉ mở đĩa khi được phép. Trả <c>null</c> khi không tìm thấy, khi tệp
    /// vật lý mất, hoặc khi người gọi ngoài phạm vi — đều là <c>404</c> để không lộ tệp có tồn tại
    /// (VHT-12 mục 5). Không ném ngoại lệ quyền để endpoint không phân biệt "bị chặn" với "không có".
    /// </summary>
    public async Task<FileDownloadResponse?> DownloadAsync(
        Guid id,
        StorageCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureBusinessAllowed(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (file is null)
        {
            await AuditAsync(caller, FileAccessAction.Download, id, null, null, null, false,
                StorageErrorCodes.FileNotFound, cancellationToken);
            return null;
        }

        var allowed = await CanDownloadAsync(db, file, caller, cancellationToken);
        if (!allowed)
        {
            // Cố ý trả 404 (không phải 403) để không lộ tệp có tồn tại với người ngoài phạm vi.
            await AuditAsync(caller, FileAccessAction.Download, file.Id, file.FileCategory,
                file.RelatedEntityType, file.RelatedEntityId, false,
                StorageErrorCodes.Forbidden, cancellationToken);
            return null;
        }

        var stream = fileStorage.TryOpenRead(file.RelativePath);
        if (stream is null)
        {
            // Metadata còn nhưng tệp vật lý mất: coi như không tìm thấy, không lộ đường dẫn.
            await AuditAsync(caller, FileAccessAction.Download, file.Id, file.FileCategory,
                file.RelatedEntityType, file.RelatedEntityId, false,
                StorageErrorCodes.FileNotFound, cancellationToken);
            return null;
        }

        await AuditAsync(caller, FileAccessAction.Download, file.Id, file.FileCategory,
            file.RelatedEntityType, file.RelatedEntityId, true, null, cancellationToken);

        return new FileDownloadResponse
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.FileSizeBytes,
        };
    }

    /// <summary>Overload cho HTTP endpoint.</summary>
    public Task<IReadOnlyList<FileMetadataDto>> ListAsync(
        RelatedEntityType relatedEntityType,
        string relatedEntityId,
        IPrincipal user,
        CancellationToken cancellationToken = default) =>
        ListAsync(relatedEntityType, relatedEntityId, StorageCaller.FromPrincipal(user), cancellationToken);

    /// <summary>Liệt kê tệp chưa xóa của một đối tượng liên quan, đã lọc theo phạm vi người gọi.</summary>
    public async Task<IReadOnlyList<FileMetadataDto>> ListAsync(
        RelatedEntityType relatedEntityType,
        string relatedEntityId,
        StorageCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureBusinessAllowed(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Giấy tờ P3: kiểm phạm vi hồ sơ trước; ngoài phạm vi trả danh sách rỗng (không lộ tệp).
        if (relatedEntityType == RelatedEntityType.Tenant)
        {
            var access = await ResolveTenantAccessAsync(relatedEntityId, caller, cancellationToken);
            if (!TenantDocumentPermissionRules.CanView(access))
            {
                await AuditAsync(caller, FileAccessAction.List, null, FileCategory.IdentityDocument,
                    relatedEntityType, relatedEntityId, false,
                    StorageErrorCodes.Forbidden, cancellationToken);
                return [];
            }

            var tenantFiles = await db.StoredFiles.AsNoTracking()
                .Where(f => f.RelatedEntityType == relatedEntityType && f.RelatedEntityId == relatedEntityId)
                .OrderByDescending(f => f.UploadedAt)
                .ToListAsync(cancellationToken);

            await AuditAsync(caller, FileAccessAction.List, null, FileCategory.IdentityDocument,
                relatedEntityType, relatedEntityId, true, null, cancellationToken);

            return tenantFiles.Select(StoredFileMapping.ToMetadata).ToList();
        }

        var query = db.StoredFiles.AsNoTracking()
            .Where(f => f.RelatedEntityType == relatedEntityType && f.RelatedEntityId == relatedEntityId);

        if (caller.IsKhachThue)
        {
            var inScope = await IsRelatedEntityInScopeAsync(
                db, relatedEntityType, relatedEntityId, caller.UserId, cancellationToken);

            if (!inScope)
            {
                // Không thuộc phạm vi: chỉ thấy tệp do chính mình upload.
                query = query.Where(f => f.UploadedByUserId == caller.UserId);
            }
        }

        var files = await query
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(cancellationToken);

        return files.Select(StoredFileMapping.ToMetadata).ToList();
    }

    /// <summary>Overload cho HTTP endpoint.</summary>
    public Task<bool> DeleteAsync(
        Guid id,
        IPrincipal user,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(id, StorageCaller.FromPrincipal(user), cancellationToken);

    /// <summary>
    /// Xóa mềm: đặt <c>IsDeleted</c>, giữ tệp vật lý. Trả <c>false</c> khi không tìm thấy hoặc khi
    /// người gọi ngoài phạm vi (đều là <c>404</c> để không lộ tệp có tồn tại); không ném ngoại lệ quyền.
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid id,
        StorageCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureBusinessAllowed(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.StoredFiles.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (file is null)
        {
            await AuditAsync(caller, FileAccessAction.Delete, id, null, null, null, false,
                StorageErrorCodes.FileNotFound, cancellationToken);
            return false;
        }

        var allowed = file.RelatedEntityType == RelatedEntityType.Tenant
            ? TenantDocumentPermissionRules.CanDelete(
                await ResolveTenantAccessAsync(file.RelatedEntityId, caller, cancellationToken))
            : StoragePermissionRules.CanDelete(caller.Role);

        if (!allowed)
        {
            // Cố ý trả 404 (không phải 403) để không lộ tệp có tồn tại với người ngoài phạm vi.
            await AuditAsync(caller, FileAccessAction.Delete, file.Id, file.FileCategory,
                file.RelatedEntityType, file.RelatedEntityId, false,
                StorageErrorCodes.Forbidden, cancellationToken);
            return false;
        }

        file.IsDeleted = true;
        await db.SaveChangesAsync(cancellationToken);

        await AuditAsync(caller, FileAccessAction.Delete, file.Id, file.FileCategory,
            file.RelatedEntityType, file.RelatedEntityId, true, null, cancellationToken);
        return true;
    }

    private async Task<bool> CanDownloadAsync(
        ApplicationDbContext db,
        StoredFile file,
        StorageCaller caller,
        CancellationToken cancellationToken)
    {
        // Giấy tờ P3: phạm vi hồ sơ khách thuê (Admin = None, Quản lý ngoài tòa nhà = None).
        if (file.RelatedEntityType == RelatedEntityType.Tenant)
        {
            var access = await ResolveTenantAccessAsync(file.RelatedEntityId, caller, cancellationToken);
            return TenantDocumentPermissionRules.CanView(access);
        }

        var isUploader = file.UploadedByUserId == caller.UserId;
        var inScope = !caller.IsKhachThue
            || await IsRelatedEntityInScopeAsync(
                db, file.RelatedEntityType, file.RelatedEntityId, caller.UserId, cancellationToken);

        return StoragePermissionRules.CanDownload(caller.Role, isUploader, inScope);
    }

    private async Task<TenantDocumentAccessLevel> ResolveTenantAccessAsync(
        string relatedEntityId,
        StorageCaller caller,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(relatedEntityId, out var tenantId))
        {
            return TenantDocumentAccessLevel.None;
        }

        return await tenantDocumentScope.ResolveAsync(
            tenantId,
            new TenantCaller(caller.UserId, caller.Role),
            cancellationToken);
    }

    private Task AuditAsync(
        StorageCaller caller,
        FileAccessAction action,
        Guid? fileId,
        FileCategory? category,
        RelatedEntityType? relatedEntityType,
        string? relatedEntityId,
        bool succeeded,
        string? errorCode,
        CancellationToken cancellationToken) =>
        audit.WriteAsync(
            new FileAccessAuditEntry(
                caller.UserId,
                caller.Role,
                action,
                fileId,
                category,
                relatedEntityType,
                relatedEntityId,
                succeeded,
                errorCode),
            cancellationToken);

    // Đối tượng liên quan đang có bảng: Room, Tenant. Các loại P4/P6/P7 chưa có bảng nên bỏ qua
    // kiểm tra tồn tại; VHT-33/VHT-54 bổ sung khi bảng tương ứng được tạo.
    private static async Task<bool> RelatedEntityExistsAsync(
        ApplicationDbContext db,
        RelatedEntityType type,
        string entityId,
        CancellationToken cancellationToken) => type switch
    {
        RelatedEntityType.Room => int.TryParse(entityId, out var roomId)
            && await db.Rooms.AnyAsync(r => r.Id == roomId, cancellationToken),
        RelatedEntityType.Tenant => int.TryParse(entityId, out var tenantId)
            && await db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken),
        _ => true,
    };

    // Khách thuê có thuộc đối tượng liên quan không (các loại P4/P6/P7 chưa có bảng; Tenant dùng
    // TenantDocumentScopeService). Mặc định từ chối để không mở quyền sớm.
    private static async Task<bool> IsRelatedEntityInScopeAsync(
        ApplicationDbContext db,
        RelatedEntityType type,
        string entityId,
        string userId,
        CancellationToken cancellationToken) => type switch
    {
        RelatedEntityType.Tenant when userId.Length > 0 && int.TryParse(entityId, out var tenantId) =>
            await db.Tenants.AnyAsync(t => t.Id == tenantId && t.UserId == userId, cancellationToken),
        _ => false,
    };

    private UploadResult TooLarge(long maxBytes) =>
        UploadResult.Failure(
            StorageErrorCodes.FileSizeExceeded,
            $"File vượt quá dung lượng cho phép (tối đa {FileTypeRules.DescribeMegabytes(maxBytes)} MB)");

    // Room/Tenant dùng khóa int: chuẩn hóa về dạng không có số 0 thừa trước khi lưu/tra cứu.
    private static bool TryCanonicalizeRelatedEntityId(
        RelatedEntityType type,
        string raw,
        out string canonical)
    {
        canonical = raw;
        if (type is not (RelatedEntityType.Room or RelatedEntityType.Tenant))
        {
            return true;
        }

        if (!int.TryParse(raw, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var id)
            || id <= 0)
        {
            return false;
        }

        canonical = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    private void TryDeletePhysical(string relativePath)
    {
        try
        {
            fileStorage.Delete(relativePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StoragePathViolationException)
        {
            logger.LogWarning(ex, "Không xóa được tệp vừa ghi sau khi lưu metadata thất bại.");
        }
    }

    // Bỏ mọi ký tự đường dẫn client gửi lên; chỉ giữ tên tệp trần.
    private static string SanitizeFileName(string? originalFileName, string extension)
    {
        var name = Path.GetFileName(originalFileName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return $"file{extension}";
        }

        return name.Length > 260 ? name[..260] : name;
    }

    private static void EnsureBusinessAllowed(StorageCaller caller)
    {
        if (caller.IsAdmin)
        {
            throw new UnauthorizedAccessException(
                "Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu.");
        }
    }
}
