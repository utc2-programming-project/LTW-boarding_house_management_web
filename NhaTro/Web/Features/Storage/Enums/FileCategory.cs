using System.Text.Json.Serialization;

namespace Web.Features.Storage.Enums;

/// <summary>
/// Loại tệp được phép upload. Serialize thành chuỗi (ví dụ "IncidentPhoto").
/// Nguồn: docs/task/de_xuat_hop_dong_storage.md mục 1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FileCategory
{
    /// <summary>Ảnh phòng trọ (.jpg, .jpeg, .png, .webp).</summary>
    RoomPhoto = 0,

    /// <summary>Hợp đồng thuê phòng (.pdf).</summary>
    Contract = 1,

    /// <summary>Hóa đơn do chủ trọ/quản lý phát hành (.pdf, .jpg, .jpeg, .png).</summary>
    Invoice = 2,

    /// <summary>Giấy tờ tùy thân khách thuê (.pdf, .jpg, .jpeg, .png).</summary>
    IdentityDocument = 3,

    /// <summary>Ảnh sự cố P7 (.jpg, .jpeg, .png, .webp).</summary>
    IncidentPhoto = 4,

    /// <summary>Minh chứng thanh toán do khách gửi P6 (.pdf, .jpg, .jpeg, .png).</summary>
    PaymentProof = 5
}
