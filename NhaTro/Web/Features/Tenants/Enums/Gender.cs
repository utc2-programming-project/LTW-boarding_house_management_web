using System.Text.Json.Serialization;

namespace Web.Features.Tenants.Enums;

/// <summary>
/// Giới tính khách thuê. Serialize thành chuỗi (ví dụ "Male").
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 2.
/// </summary>
/// <remarks>Giá trị là đề xuất, cần nhóm chốt (mục 11 câu hỏi 10).</remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Gender
{
    Male = 0,
    Female = 1,
    Other = 2
}
