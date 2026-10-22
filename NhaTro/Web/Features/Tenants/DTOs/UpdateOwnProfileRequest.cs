using System.ComponentModel.DataAnnotations;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Khách thuê tự sửa hồ sơ của mình (chỉ các trường ở mục 5.2).
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.7.
/// </summary>
/// <remarks>
/// Không có ConfirmDuplicatePhone: khi khách tự sửa, service không kiểm trùng số điện thoại
/// và không trả PossibleDuplicates (để khách không thấy hồ sơ của người khác).
/// </remarks>
public sealed class UpdateOwnProfileRequest
{
    [Required(ErrorMessage = "Nhập số điện thoại.")]
    [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string? Email { get; set; }

    [MaxLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự.")]
    public string? PermanentAddress { get; set; }

    /// <summary>Bắt buộc: chống ghi đè đồng thời (mục 5.3).</summary>
    [Required(ErrorMessage = "Thiếu RowVersion.")]
    public string RowVersion { get; set; } = string.Empty;

    public void Normalize()
    {
        PhoneNumber = TenantNormalizer.NormalizePhone(PhoneNumber);
        Email = TenantNormalizer.NormalizeOptional(Email);
        PermanentAddress = TenantNormalizer.NormalizeOptional(PermanentAddress);
    }
}
