namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Dữ liệu đề xuất lưu trong hàng đợi duyệt cho SW-06 (xóa hồ sơ khách thuê).
/// Applier chỉ cần khóa hồ sơ; tên được giữ để Chủ trọ đọc lịch sử dễ hơn.
/// </summary>
public sealed record TenantDeletePayload(int TenantId, string FullName);
