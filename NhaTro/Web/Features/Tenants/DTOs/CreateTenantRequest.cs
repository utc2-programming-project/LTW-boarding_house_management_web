using System.ComponentModel.DataAnnotations;
using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Tạo hồ sơ khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.5.
/// </summary>
public sealed class CreateTenantRequest
{
    [Required(ErrorMessage = "Nhập họ tên.")]
    [MaxLength(200, ErrorMessage = "Họ tên tối đa 200 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập số điện thoại.")]
    [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string? Email { get; set; }

    /// <summary>Số CCCD/CMND. Bắt buộc khi tạo hay trước khi ký hợp đồng: cần chốt.</summary>
    [MaxLength(20, ErrorMessage = "Số giấy tờ tối đa 20 ký tự.")]
    public string? IdNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [EnumDataType(typeof(Gender), ErrorMessage = "Giới tính không hợp lệ.")]
    public Gender? Gender { get; set; }

    [MaxLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự.")]
    public string? PermanentAddress { get; set; }

    public DateOnly? IdIssuedDate { get; set; }

    [MaxLength(200, ErrorMessage = "Nơi cấp tối đa 200 ký tự.")]
    public string? IdIssuedPlace { get; set; }

    /// <summary>Ghi chú nội bộ của Chủ trọ/Quản lý.</summary>
    [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
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
