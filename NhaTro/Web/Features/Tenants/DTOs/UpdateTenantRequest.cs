using System.ComponentModel.DataAnnotations;
using Web.Features.Tenants.Enums;
using Web.Resources;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Chủ trọ / Quản lý sửa hồ sơ khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.6.
/// </summary>
/// <remarks>
/// Ngữ nghĩa PUT: form sửa phải gửi ĐỦ mọi trường; trường không gửi bị ghi null/thay thế.
/// Không có trường nào để bỏ trống ngoài các trường nullable ở mục 2.
/// </remarks>
public sealed class UpdateTenantRequest
{
    [Required(
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantFullNameRequired))]
    [MaxLength(200,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantFullNameMaxLength))]
    public string FullName { get; set; } = string.Empty;

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

    [MaxLength(20,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantIdNumberMaxLength))]
    public string? IdNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [EnumDataType(typeof(Gender),
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantGenderInvalid))]
    public Gender? Gender { get; set; }

    [MaxLength(300,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantAddressMaxLength))]
    public string? PermanentAddress { get; set; }

    public DateOnly? IdIssuedDate { get; set; }

    [MaxLength(200,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantIdIssuedPlaceMaxLength))]
    public string? IdIssuedPlace { get; set; }

    [MaxLength(500,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantNoteMaxLength))]
    public string? Note { get; set; }

    /// <summary>Mặc định false; true để sửa dù nghi trùng số điện thoại (mục 3).</summary>
    public bool ConfirmDuplicatePhone { get; set; }

    /// <summary>Bắt buộc: chống ghi đè đồng thời (mục 5.3).</summary>
    [Required(
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantRowVersionRequired))]
    public string RowVersion { get; set; } = string.Empty;

    public void Normalize()
    {
        FullName = TenantNormalizer.NormalizeFullName(FullName);
        PhoneNumber = TenantNormalizer.NormalizePhone(PhoneNumber);
        IdNumber = NullIfEmpty(TenantNormalizer.NormalizeIdNumber(IdNumber));
        Email = TenantNormalizer.NormalizeOptional(Email);
        PermanentAddress = TenantNormalizer.NormalizeOptional(PermanentAddress);
        IdIssuedPlace = TenantNormalizer.NormalizeOptional(IdIssuedPlace);
        Note = TenantNormalizer.NormalizeOptional(Note);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
