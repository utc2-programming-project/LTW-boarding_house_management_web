using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Chủ trọ / Quản lý sửa hồ sơ khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.6.
/// </summary>
public class UpdateTenantRequest
{
    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string? IdNumber { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public Gender? Gender { get; init; }

    public string? PermanentAddress { get; init; }

    public DateOnly? IdIssuedDate { get; init; }

    public string? IdIssuedPlace { get; init; }

    public string? Note { get; init; }

    /// <summary>Mặc định false; true để sửa dù nghi trùng số điện thoại (mục 3).</summary>
    public bool ConfirmDuplicatePhone { get; init; }

    /// <summary>Bắt buộc: chống ghi đè đồng thời (mục 5.3).</summary>
    public required string RowVersion { get; init; }
}
