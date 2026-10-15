namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Một dòng trong danh sách khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.3.
/// </summary>
public class TenantListItemDto
{
    public required Guid Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    /// <summary>Chỉ 4 số cuối (ví dụ ********1234).</summary>
    public string? IdNumberMasked { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>UserId != null.</summary>
    public required bool HasAccount { get; init; }

    /// <summary>Cần chốt như mục 6.1.</summary>
    public required bool HasActiveContract { get; init; }
}
