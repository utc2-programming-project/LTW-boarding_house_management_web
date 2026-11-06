using System.ComponentModel.DataAnnotations;
using Web.Resources;

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
    [Required(
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantPhoneRequired))]
    [MaxLength(20,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantPhoneMaxLength))]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantEmailInvalid))]
    [MaxLength(256,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantEmailMaxLength))]
    public string? Email { get; set; }

    [MaxLength(300,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantAddressMaxLength))]
    public string? PermanentAddress { get; set; }

    /// <summary>Bắt buộc: chống ghi đè đồng thời (mục 5.3).</summary>
    [Required(
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantRowVersionRequired))]
    public string RowVersion { get; set; } = string.Empty;

    public void Normalize()
    {
        PhoneNumber = TenantNormalizer.NormalizePhone(PhoneNumber);
        Email = TenantNormalizer.NormalizeOptional(Email);
        PermanentAddress = TenantNormalizer.NormalizeOptional(PermanentAddress);
    }
}
