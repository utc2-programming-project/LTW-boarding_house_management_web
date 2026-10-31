using Web.Features.Storage.Enums;

namespace Web.Features.Storage.DTOs;

/// <summary>
/// Dữ liệu gửi lên khi upload file (multipart/form-data).
/// </summary>
public class UploadFileRequest
{
    /// <summary>File gốc gửi lên.</summary>
    public required IFormFile File { get; init; }

    /// <summary>Loại tệp: RoomPhoto, Contract, Invoice, IdentityDocument, IncidentPhoto, PaymentProof.</summary>
    public required FileCategory FileCategory { get; init; }

    /// <summary>ID đối tượng liên quan (phòng, hợp đồng, hóa đơn, khách thuê, sự cố, thanh toán).</summary>
    public required string RelatedEntityId { get; init; }

    /// <summary>Loại đối tượng liên quan: Room, Contract, Invoice, Tenant, MaintenanceIncident, Payment.</summary>
    public required RelatedEntityType RelatedEntityType { get; init; }

    /// <summary>Loại giấy tờ cụ thể trong hồ sơ khách thuê (CCCD_Front, CCCD_Back, ...).</summary>
    public string? DocumentType { get; init; }

    /// <summary>Ghi chú tùy chọn.</summary>
    public string? Description { get; init; }
}
