using Web.Features.Storage.DTOs;
using Web.Features.Tenants.DTOs;

namespace Web.Features.Tenants.Documents;

/// <summary>
/// Service giấy tờ P3 (VHT-26): gắn metadata/tệp vào hồ sơ khách thuê, tải có kiểm quyền,
/// chặn ngoài phạm vi và ghi audit không chứa nội dung giấy tờ.
/// Mọi hàm tự xác định phạm vi từ <see cref="TenantCaller"/>, không nhận phạm vi từ client.
/// </summary>
public interface ITenantDocumentService
{
    /// <summary>
    /// Danh sách giấy tờ của hồ sơ. Trả <c>null</c> khi hồ sơ không tồn tại hoặc người gọi
    /// ngoài phạm vi (endpoint trả 404 để không lộ hồ sơ/tệp có tồn tại).
    /// </summary>
    Task<TenantDocumentListDto?> ListAsync(
        int tenantId,
        TenantCaller caller,
        CancellationToken cancellationToken = default);

    Task<UploadResult> UploadAsync(
        int tenantId,
        UploadFileRequest request,
        TenantCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thay tệp: upload tệp mới trước, chỉ xóa mềm tệp cũ khi tệp mới đã lưu thành công.
    /// Nếu bước xóa tệp cũ lỗi thì xóa mềm luôn tệp mới để không còn hai tệp hiện hành.
    /// </summary>
    Task<UploadResult> ReplaceAsync(
        int tenantId,
        Guid fileId,
        UploadFileRequest request,
        TenantCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Tải/xem tệp; trả <c>null</c> (404) khi ngoài phạm vi hoặc tệp không thuộc hồ sơ.</summary>
    Task<FileDownloadResponse?> DownloadAsync(
        int tenantId,
        Guid fileId,
        TenantCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Xóa mềm; trả <c>false</c> (404) khi ngoài phạm vi hoặc tệp không thuộc hồ sơ.</summary>
    Task<bool> DeleteAsync(
        int tenantId,
        Guid fileId,
        TenantCaller caller,
        CancellationToken cancellationToken = default);
}
