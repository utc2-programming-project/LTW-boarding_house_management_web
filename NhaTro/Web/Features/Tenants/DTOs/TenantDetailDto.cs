using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Chi tiết một khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.4.
/// </summary>
public class TenantDetailDto
{
    public required Guid Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    /// <summary>Đầy đủ, theo quy tắc che ở mục 4.</summary>
    public string? IdNumber { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public Gender? Gender { get; init; }

    public string? PermanentAddress { get; init; }

    public DateOnly? IdIssuedDate { get; init; }

    public string? IdIssuedPlace { get; init; }

    /// <summary>Chỉ trả cho Chủ trọ/Quản lý.</summary>
    public string? Note { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>Không trả UserId thô cho khách thuê.</summary>
    public required bool HasAccount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Base64, gửi lại khi sửa.</summary>
    public string? RowVersion { get; init; }
}
