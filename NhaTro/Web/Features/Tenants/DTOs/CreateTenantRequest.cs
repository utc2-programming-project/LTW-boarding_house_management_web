using System.ComponentModel.DataAnnotations;
using Web.Features.Tenants.Enums;
using Web.Resources;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Tạo hồ sơ khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.5.
/// </summary>
public sealed class CreateTenantRequest
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

    /// <summary>Số CCCD/CMND. Bắt buộc khi tạo hay trước khi ký hợp đồng: cần chốt.</summary>
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

    /// <summary>Ghi chú nội bộ của Chủ trọ/Quản lý.</summary>
    [MaxLength(500,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.TenantNoteMaxLength))]
    public string? Note { get; set; }

    /// <summary>Mặc định false; true để tạo dù nghi trùng số điện thoại (mục 3).</summary>
    public bool ConfirmDuplicatePhone { get; set; }

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
