using System.ComponentModel.DataAnnotations;
using Web.Features.Tenants.Enums;

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
    [Required(ErrorMessage = "Nhập họ tên.")]
    [MaxLength(200, ErrorMessage = "Họ tên tối đa 200 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập số điện thoại.")]
    [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string? Email { get; set; }

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

    [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Note { get; set; }

    /// <summary>Mặc định false; true để sửa dù nghi trùng số điện thoại (mục 3).</summary>
    public bool ConfirmDuplicatePhone { get; set; }

    /// <summary>Bắt buộc: chống ghi đè đồng thời (mục 5.3).</summary>
    [Required(ErrorMessage = "Thiếu RowVersion.")]
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
