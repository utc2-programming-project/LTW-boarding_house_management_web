using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Data;
using Web.Features.Accounts.Avatar;

namespace Web.Data.Configurations.Accounts;

// PLAN-030 bổ sung: bảng ảnh đại diện, một dòng một tài khoản.
// Cascade để ảnh bị xoá cùng tài khoản (xoá vĩnh viễn, xoá 1-1) — tránh blob mồ côi.
public class UserAvatarConfiguration : IEntityTypeConfiguration<UserAvatar>
{
    public void Configure(EntityTypeBuilder<UserAvatar> builder)
    {
        builder.HasKey(avatar => avatar.UserId);

        builder.Property(avatar => avatar.UserId).HasMaxLength(450);
        builder.Property(avatar => avatar.ContentType).HasMaxLength(20).IsRequired();
        builder.Property(avatar => avatar.Data).IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<UserAvatar>(avatar => avatar.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
