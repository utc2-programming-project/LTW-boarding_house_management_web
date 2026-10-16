using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Mapping;
using Web.Features.Storage.Rules;
using Web.Infrastructure.Storage;

namespace Web.Features.Storage.Services;

/// <summary>
/// Storage dùng chung (VHT-13): lưu/tải/xóa mềm tệp ngoài <c>wwwroot</c> kèm kiểm quyền.
/// Là nơi duy nhất quyết định quyền storage; endpoint chỉ chuyển tiếp kết quả.
/// </summary>
public sealed class FileService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IFileStorage fileStorage,
    IOptions<StorageOptions> options,
    TimeProvider timeProvider,
    ILogger<FileService> logger)
{
    private StorageOptions Options => options.Value;

    /// <summary>
    /// Lưu tệp: kiểm quyền → kiểm loại/cặp đối tượng → đọc stream có giới hạn → kiểm nội dung thật
    /// → kiểm số tệp tối đa → ghi đĩa → lưu metadata. Tệp sai loại/quá giới hạn không được ghi đĩa.
    /// </summary>
    public async Task<UploadResult> UploadAsync(
        UploadFileRequest request,
        IPrincipal user,
        CancellationToken cancellationToken = default)
    {
        BusinessGuard.EnsureBusinessAllowed(user);

        var role = RoleOf(user);
        var userId = user.UserId() ?? string.Empty;

        if (request.File is null
            || !Enum.IsDefined(request.FileCategory)
            || !Enum.IsDefined(request.RelatedEntityType))
        {
            return UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ.");
        }

        if (!StoragePermissionRules.CanUpload(role, request.FileCategory))
        {
            return UploadResult.Failure(
                StorageErrorCodes.Forbidden,
                "Bạn không có quyền thực hiện thao tác này.");
        }

        if (!FileTypeRules.IsPairValid(request.FileCategory, request.RelatedEntityType))
        {
            return UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ.");
        }

        var relatedEntityId = request.RelatedEntityId?.Trim() ?? string.Empty;
        if (relatedEntityId.Length == 0 || relatedEntityId.Length > 450)
        {
            return UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ.");
        }

        var extension = FileTypeRules.GetExtension(request.File.FileName);
        if (!FileTypeRules.IsExtensionAllowed(request.FileCategory, extension))
        {
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
                return TooLarge(maxBytes);
            }
            catch (StorageEmptyFileException)
            {
                return UploadResult.Failure(StorageErrorCodes.EmptyFile, "File không có nội dung.");
            }
        }

        if (!FileContentSniffer.IsAllowedContent(request.FileCategory, extension, content))
        {
            return UploadResult.Failure(
                StorageErrorCodes.InvalidFileType,
                $"{FileTypeRules.DisplayName(request.FileCategory)} chỉ chấp nhận file {FileTypeRules.DescribeAllowedExtensions(request.FileCategory)}");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (!await RelatedEntityExistsAsync(db, request.RelatedEntityType, relatedEntityId, cancellationToken))
        {
            return UploadResult.Failure(
                StorageErrorCodes.RelatedEntityNotFound,
                "Không tìm thấy đối tượng liên quan.");
        }

        // Khách thuê chỉ được gắn tệp vào đối tượng thuộc phạm vi của mình.
        if (role == Roles.KhachThue
            && !await IsRelatedEntityInScopeAsync(db, request.RelatedEntityType, relatedEntityId, userId, cancellationToken))
        {
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
            UploadedByUserId = userId,
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
            return UploadResult.Failure(
                StorageErrorCodes.UploadFailed,
                "Không thể tải lên, vui lòng thử lại.");
        }

        return UploadResult.Success(StoredFileMapping.ToMetadata(stored));
    }

    /// <summary>
    /// Tải tệp: kiểm quyền trước, chỉ mở đĩa khi được phép. Trả <c>null</c> khi không tìm thấy, khi tệp
    /// vật lý mất, hoặc khi người gọi ngoài phạm vi — đều là <c>404</c> để không lộ tệp có tồn tại
    /// (VHT-12 mục 5). Không ném ngoại lệ quyền để endpoint không phân biệt "bị chặn" với "không có".
    /// </summary>
    public async Task<FileDownloadResponse?> DownloadAsync(
        Guid id,
        IPrincipal user,
        CancellationToken cancellationToken = default)
    {
        BusinessGuard.EnsureBusinessAllowed(user);

        var role = RoleOf(user);
        var userId = user.UserId() ?? string.Empty;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (file is null)
        {
            return null;
        }

        var isUploader = file.UploadedByUserId == userId;
        var inScope = role != Roles.KhachThue
            || await IsRelatedEntityInScopeAsync(db, file.RelatedEntityType, file.RelatedEntityId, userId, cancellationToken);

        if (!StoragePermissionRules.CanDownload(role, isUploader, inScope))
        {
            // Cố ý trả 404 (không phải 403) để không lộ tệp có tồn tại với người ngoài phạm vi.
            return null;
        }

        var stream = fileStorage.TryOpenRead(file.RelativePath);
        if (stream is null)
        {
            // Metadata còn nhưng tệp vật lý mất: coi như không tìm thấy, không lộ đường dẫn.
            return null;
        }

        return new FileDownloadResponse
        {
            FileStream = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.FileSizeBytes,
        };
    }

    /// <summary>Liệt kê tệp chưa xóa của một đối tượng liên quan, đã lọc theo phạm vi người gọi.</summary>
    public async Task<IReadOnlyList<FileMetadataDto>> ListAsync(
        RelatedEntityType relatedEntityType,
        string relatedEntityId,
        IPrincipal user,
        CancellationToken cancellationToken = default)
    {
        BusinessGuard.EnsureBusinessAllowed(user);

        var role = RoleOf(user);
        var userId = user.UserId() ?? string.Empty;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.StoredFiles.AsNoTracking()
            .Where(f => f.RelatedEntityType == relatedEntityType && f.RelatedEntityId == relatedEntityId);

        if (role == Roles.KhachThue)
        {
            var inScope = await IsRelatedEntityInScopeAsync(
                db, relatedEntityType, relatedEntityId, userId, cancellationToken);

            if (!inScope)
            {
                // Không thuộc phạm vi: chỉ thấy tệp do chính mình upload.
                query = query.Where(f => f.UploadedByUserId == userId);
            }
        }

        var files = await query
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(cancellationToken);

        return files.Select(StoredFileMapping.ToMetadata).ToList();
    }

    /// <summary>
    /// Xóa mềm: đặt <c>IsDeleted</c>, giữ tệp vật lý. Trả <c>false</c> khi không tìm thấy hoặc khi
    /// người gọi ngoài phạm vi (đều là <c>404</c> để không lộ tệp có tồn tại); không ném ngoại lệ quyền.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, IPrincipal user, CancellationToken cancellationToken = default)
    {
        BusinessGuard.EnsureBusinessAllowed(user);

        var role = RoleOf(user);
        var userId = user.UserId() ?? string.Empty;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.StoredFiles.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (file is null)
        {
            return false;
        }

        if (!StoragePermissionRules.CanDelete(role, file.UploadedByUserId == userId))
        {
            // Cố ý trả 404 (không phải 403) để không lộ tệp có tồn tại với người ngoài phạm vi.
            return false;
        }

        file.IsDeleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Đối tượng liên quan đang có bảng: Room, Tenant. Các loại P4/P6/P7 chưa có bảng nên bỏ qua
    // kiểm tra tồn tại; VHT-26/VHT-33/VHT-54 bổ sung khi bảng tương ứng được tạo.
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

    // Khách thuê có thuộc đối tượng liên quan không. Hiện chỉ suy ra được từ hồ sơ khách thuê;
    // phòng/hợp đồng/hóa đơn/sự cố/thanh toán chờ P4/P6/P7 nên mặc định từ chối để không mở quyền sớm.
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

    private static string RoleOf(IPrincipal user)
    {
        foreach (var role in Roles.All)
        {
            if (user.IsInRole(role))
            {
                return role;
            }
        }

        return string.Empty;
    }
}
