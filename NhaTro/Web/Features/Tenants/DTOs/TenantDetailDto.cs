using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Chi tiết một khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.4.
/// </summary>
public class TenantDetailDto
{
    public required int Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    /// <summary>
    /// Số giấy tờ. Chỉ trả đầy đủ cho Chủ trọ và chính khách thuê đó; với Quản lý (D9) trả chuỗi che
    /// (giống <c>IdNumberMasked</c>). Danh sách và picker luôn chỉ trả <c>IdNumberMasked</c> (mục 4).
    /// </summary>
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

    /// <summary>null khi chưa sửa lần nào.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Base64, gửi lại khi sửa.</summary>
    public required string RowVersion { get; init; }
}
