using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Tạo hồ sơ khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.5.
/// </summary>
public class CreateTenantRequest
{
    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    /// <summary>Số CCCD/CMND. Bắt buộc khi tạo hay trước khi ký hợp đồng: cần chốt.</summary>
    public string? IdNumber { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public Gender? Gender { get; init; }

    public string? PermanentAddress { get; init; }

    public DateOnly? IdIssuedDate { get; init; }

    public string? IdIssuedPlace { get; init; }

    /// <summary>Ghi chú nội bộ của Chủ trọ/Quản lý.</summary>
    public string? Note { get; init; }

    /// <summary>Mặc định false; true để tạo dù nghi trùng số điện thoại (mục 3).</summary>
    public bool ConfirmDuplicatePhone { get; init; }
}
