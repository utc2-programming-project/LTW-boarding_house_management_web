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

    // PLAN-028: tai khoan nhan tien. Gui ca 3 rong = xoa cau hinh; gui thieu 1 trong 3 = loi.
    public string? BankCode { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BankAccountName { get; set; }
}
