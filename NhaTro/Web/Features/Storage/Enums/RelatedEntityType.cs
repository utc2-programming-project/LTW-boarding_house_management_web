using System.Text.Json.Serialization;

namespace Web.Features.Storage.Enums;

/// <summary>
/// Loại đối tượng liên quan của tệp. Serialize thành chuỗi (ví dụ "MaintenanceIncident").
/// Nguồn: docs/task/de_xuat_hop_dong_storage.md mục 1.
/// </summary>
/// <remarks>
/// <see cref="MaintenanceIncident"/> và <see cref="Payment"/> là tên đề xuất, cần đối chiếu schema P6/P7
/// (có thể là PaymentRequest) — xem mục 9 câu hỏi 3 của tài liệu.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RelatedEntityType
{
    Room = 0,
    Contract = 1,
    Invoice = 2,
    Tenant = 3,
    MaintenanceIncident = 4,
    Payment = 5
}
