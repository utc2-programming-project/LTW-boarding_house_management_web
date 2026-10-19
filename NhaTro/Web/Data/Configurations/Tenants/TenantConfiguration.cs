using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Tenants.Entities;

namespace Web.Data.Configurations.Tenants;

// Cau hinh bang Tenants. Cac diem KHAC ERD VHT-5 (ly do o muc 0.1/muc 8 tai lieu de_xuat_hop_dong_khach_thue):
//  - IsActive + RowVersion + Gender/DateOfBirth/IdIssuedDate/IdIssuedPlace/Note/IsActive: cot them moi (ERD chua co).
//  - Chi muc CitizenId doi loc (them [IsActive] = 1); chi muc UserId doi sang unique filtered (1-1, cho chot).
//  - FK UserId -> AspNetUsers voi Restrict: khong cho xoa tai khoan khi con ho so tham chieu.
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(t => t.FullName).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Phone).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Email).HasMaxLength(256);
        builder.Property(t => t.CitizenId).HasMaxLength(20);
        builder.Property(t => t.PermanentAddress).HasMaxLength(300);
        builder.Property(t => t.IdIssuedPlace).HasMaxLength(200);
        builder.Property(t => t.Note).HasMaxLength(500);
        // Gender: ERD chua co. Luu chuoi cho de doc/on dinh khi doi thu tu enum (DTO da serialize chuoi).
        builder.Property(t => t.Gender).HasConversion<string>().HasMaxLength(10);
        // IsActive: ERD chua co; mac dinh 1 (dang su dung) theo muc 8. Khac IsDeleted (thung rac).
        builder.Property(t => t.IsActive).HasDefaultValue(true);
        // RowVersion: ERD chua co; them de chong ghi de dong thoi (muc 5.3).
        builder.Property(t => t.RowVersion).IsRowVersion();

        // CCCD khong trung voi ho so dang su dung: khop logic service (chi kiem trung khi IsActive).
        // Bo qua ho so trong va ho so da xoa mem.
        // KHAC ERD: loc goc chi co [IsDeleted] = 0, them [IsActive] = 1 cho khop service (muc 0.1).
        builder.HasIndex(t => t.CitizenId)
            .IsUnique()
            .HasFilter("[CitizenId] IS NOT NULL AND [IsDeleted] = 0 AND [IsActive] = 1");

        // 1 tai khoan toi da 1 ho so (muc 1). Chi ap khi UserId khac null va chua xoa mem.
        // KHAC ERD: ERD de index thuong; doi sang unique filtered theo huong 1-1 — can Dao/Phat xac nhan.
        builder.HasIndex(t => t.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL AND [IsDeleted] = 0");

        // Tim kiem theo Keyword (muc 8): ERD chua co, them index tren FullName va Phone.
        builder.HasIndex(t => t.FullName);
        builder.HasIndex(t => t.Phone);

        // Thung rac (VHT-99): ho so da xoa mem khong hien ra truy van thuong.
        builder.HasQueryFilter(t => !t.IsDeleted);

        // FK toi tai khoan dang nhap; Restrict de khong xoa nham user khi con ho so (chong lien ket nham).
        builder.HasOne<Data.ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
