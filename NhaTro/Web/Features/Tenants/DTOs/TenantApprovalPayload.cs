using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>Hành động được đóng gói trong yêu cầu duyệt SW-04/05/06.</summary>
public static class TenantApprovalActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string SetActive = "SetActive";
    public const string Delete = "Delete";
}

/// <summary>
/// Dữ liệu đề xuất cho hàng đợi duyệt hồ sơ khách thuê. Dùng chung một record để
/// applier của SW-04/05/06 đọc được payload cũ (chỉ có TenantId/FullName) lẫn payload mới.
/// </summary>
public sealed record TenantApprovalPayload
{
    /// <summary>Create/Update/SetActive/Delete; payload cũ không có trường này được hiểu là Delete.</summary>
    public string Action { get; init; } = string.Empty;

    public int TenantId { get; init; }

    public string? FullName { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }

    /// <summary>Chuỗi đã chuẩn hóa; null khi giữ nguyên số giấy tờ hiện tại.</summary>
    public string? IdNumber { get; init; }

    /// <summary>True khi Quản lý chỉ sửa trường khác và số giấy tờ không đổi (D9).</summary>
    public bool IdNumberUnchanged { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public Gender? Gender { get; init; }

    public string? PermanentAddress { get; init; }

    public DateOnly? IdIssuedDate { get; init; }

    public string? IdIssuedPlace { get; init; }

    public string? Note { get; init; }

    public bool? IsActive { get; init; }

    /// <summary>Chủ sở hữu được liên kết khi tạo hồ sơ (payload lưu lúc gửi duyệt).</summary>
    public string? OwnerId { get; init; }
}
