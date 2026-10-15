using System.Text.Json.Serialization;

namespace Web.Features.Tenants.Enums;

/// <summary>
/// Chiều sắp xếp. Serialize thành chuỗi (ví dụ "Asc").
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SortDirection
{
    Asc = 0,
    Desc = 1
}
