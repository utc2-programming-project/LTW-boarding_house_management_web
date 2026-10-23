using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Properties.Entities;

namespace Web.Data.Configurations.Properties;

/// <summary>
/// VHT-24 — bảng phân công Quản lý theo tòa nhà.
/// Giữ lịch sử: chỉ một phân công đang hiệu lực cho mỗi cặp (tòa, Quản lý);
/// các dòng đã bỏ gán (RevokedAt khác null) được phép tồn tại.
/// </summary>
public class PropertyManagerConfiguration : IEntityTypeConfiguration<PropertyManager>
{
    public void Configure(EntityTypeBuilder<PropertyManager> builder)
    {
        builder.Property(a => a.ManagerUserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.AssignedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.RevokedByUserId).HasMaxLength(450);
        builder.Property(a => a.AssignedAt).IsRequired();

        // Một Quản lý tối đa 1 phân công đang hiệu lực trên mỗi tòa.
        builder.HasIndex(a => new { a.PropertyId, a.ManagerUserId })
            .IsUnique()
            .HasFilter("[RevokedAt] IS NULL");

        builder.HasIndex(a => a.ManagerUserId);
        builder.HasIndex(a => a.PropertyId);

        builder.HasOne<Web.Features.Properties.Entities.Property>()
            .WithMany()
            .HasForeignKey(a => a.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Data.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.ManagerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Data.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Data.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
