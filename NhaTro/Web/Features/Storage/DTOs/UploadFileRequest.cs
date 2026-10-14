namespace Web.Features.Storage.DTOs;

/// <summary>
/// Dữ liệu gửi lên khi upload file.
/// </summary>
public class UploadFileRequest
{
    /// <summary>File gốc gửi lên.</summary>
    public required IFormFile File { get; init; }

    /// <summary>ID đối tượng liên quan (phòng, hợp đồng, hóa đơn, khách thuê).</summary>
    public required string RelatedEntityId { get; init; }

    /// <summary>Loại đối tượng liên quan: "Room", "Contract", "Invoice", "Tenant".</summary>
    public required string RelatedEntityType { get; init; }

    /// <summary>Ghi chú tùy chọn.</summary>
    public string? Description { get; init; }
}
