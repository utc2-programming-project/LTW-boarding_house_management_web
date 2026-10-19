using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.Entities;

// Ho so khach thue (P3). Tach khoi tai khoan dang nhap (ApplicationUser).
//
// KHAC ERD VHT-5 (nguon chuan schema) — ly do chi tiet o docs/task/de_xuat_hop_dong_khach_thue.md muc 0.1 va muc 8:
//  - Id: ERD dung int identity; DTO 1B de xuat Guid -> giu int cho khop PK that va P2 (Property/Room).
//  - IsActive: ERD chua co; them de danh dau "dang su dung" (khac "xoa mem").
//    IsDeleted giu nguyen vai tro thung rac theo ERD (VHT-99).
//  - RowVersion: ERD chua co; them de chong ghi de dong thoi (muc 5.3).
//  - Gender: ERD chua co; them, luu chuoi (xem TenantConfiguration) cho de doc/on dinh khi doi enum.
//  - DateOfBirth/IdIssuedDate/IdIssuedPlace/PermanentAddress/Note: ERD chua co, them theo de xuat muc 2.
//  - Ten cot DB giu theo ERD (Phone/CitizenId); DTO dung PhoneNumber/IdNumber, doi chieu o TenantMapping.
public class Tenant
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    // Cot DB "Phone"; DTO goi la PhoneNumber.
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    // Cot DB "CitizenId" (CCCD/CMND); DTO goi la IdNumber. Nhay cam, chi tra day du o chi tiet.
    public string? CitizenId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public Gender? Gender { get; set; }

    public string? PermanentAddress { get; set; }

    public DateOnly? IdIssuedDate { get; set; }

    public string? IdIssuedPlace { get; set; }

    // Ghi chu noi bo cua ChuTro/QuanLy; khong tra cho khach thue.
    public string? Note { get; set; }

    // Hoat dong (true) / ngung su dung (false). Khac IsDeleted (thung rac VHT-99).
    public bool IsActive { get; set; } = true;

    // Tai khoan dang nhap cua khach (nullable). Toi da 1 ho so / 1 tai khoan (chi muc loc duy nhat).
    public string? UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Chong ghi de dong thoi (muc 5.3). EF map rowversion.
    public byte[] RowVersion { get; set; } = [];

    public bool IsDeleted { get; set; }
}
