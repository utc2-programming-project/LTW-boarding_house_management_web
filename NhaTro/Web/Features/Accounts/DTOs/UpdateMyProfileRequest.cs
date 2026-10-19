namespace Web.Features.Accounts.DTOs;

/// <summary>
/// VHT-8: du lieu cap nhat ho so ca nhan. Email KHONG nam o day - doi email la thao tac rieng,
/// can xac thuc lai (chua lam trong VHT-8).
/// </summary>
public sealed class UpdateMyProfileRequest
{
    /// <summary>Ho ten hien thi, toi da 200 ky tu.</summary>
    public string? FullName { get; set; }

    /// <summary>So dien thoai Viet Nam 10 chu so (dac ta VHT-8).</summary>
    public string? PhoneNumber { get; set; }
}
