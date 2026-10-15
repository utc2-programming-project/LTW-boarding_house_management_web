namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Khách thuê tự sửa hồ sơ của mình (chỉ các trường ở mục 5.2).
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.7.
/// </summary>
/// <remarks>
/// Không có ConfirmDuplicatePhone: khi khách tự sửa, service không kiểm trùng số điện thoại
/// và không trả PossibleDuplicates (để khách không thấy hồ sơ của người khác).
/// </remarks>
public class UpdateOwnProfileRequest
{
    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string? PermanentAddress { get; init; }

    /// <summary>Bắt buộc: chống ghi đè đồng thời (mục 5.3).</summary>
    public required string RowVersion { get; init; }
}
