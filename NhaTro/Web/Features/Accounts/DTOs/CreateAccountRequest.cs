using System.ComponentModel.DataAnnotations;

namespace Web.Features.Accounts.DTOs;

/// <summary>
/// VHT-95: du lieu tao tai khoan tu man hinh quan tri.
/// Mat khau ban dau do nguoi tao nhap (chua co SMTP gui email dat mat khau).
/// </summary>
public sealed class CreateAccountRequest
{
    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhập họ tên.")]
    [MaxLength(200, ErrorMessage = "Họ tên tối đa 200 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Chọn vai trò.")]
    public string Role { get; set; } = string.Empty;

    /// <summary>Bat buoc khi vai tro la KhachThue: ho so khach chua co tai khoan trong pham vi.</summary>
    public int? TenantId { get; set; }
}
