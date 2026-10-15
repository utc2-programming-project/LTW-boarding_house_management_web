using System.Text.Json.Serialization;

namespace Web.Features.Tenants.Enums;

/// <summary>
/// Trường sắp xếp danh sách khách thuê. Serialize thành chuỗi (ví dụ "FullName").
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TenantSortBy
{
    FullName = 0,
    CreatedAt = 1
}
