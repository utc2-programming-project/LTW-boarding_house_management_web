namespace Web.Features.Accounts;

// Ten vai tro chot theo docs/tech-stack (VHT-4/VHT-6).
// Dung ten ASCII on dinh de lưu trong AspNetRoles, hien thi tieng Viet o UI.
public static class Roles
{
    public const string Admin = "Admin";
    public const string ChuTro = "ChuTro";
    public const string QuanLy = "QuanLy";
    public const string KhachThue = "KhachThue";

    public static readonly string[] All = [Admin, ChuTro, QuanLy, KhachThue];
}
