namespace Web.Features.Tenants.Entities;

/// <summary>
/// PLAN-005 (điều chỉnh): liên kết nhiều-nhiều giữa hồ sơ khách và chủ trọ.
/// Một khách có thể thuê phòng của nhiều chủ trọ, hoặc được nhiều chủ trọ mời;
/// mỗi chủ trọ chỉ thấy hồ sơ mà mình có liên kết (do tạo, do mời, hoặc do có hợp đồng).
/// </summary>
public class TenantOwnerLink
{
    public int TenantId { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
