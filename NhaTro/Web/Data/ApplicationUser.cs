using Microsoft.AspNetCore.Identity;

namespace Web.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        // VHT-8: ho ten hien thi (trang ca nhan, danh sach tai khoan).
        // Luu y: ERD VHT-5 khong co cot nay; them theo dac ta VHT-8 muc "Ho so ca nhan: Ho ten".
        // Nullable de tai khoan cu (seed, du lieu that) khong bi vo khi nang cap.
        public string? FullName { get; set; }

        // VHT-95: null = Cho duyet; co gia tri = da duoc duyet (Hoạt động neu khong bi khoa).
        // Trang thai hien thi suy tu cot nay + LockoutEnd, khong them cot Status rieng.
        public DateTime? ApprovedAt { get; set; }
    }

}
