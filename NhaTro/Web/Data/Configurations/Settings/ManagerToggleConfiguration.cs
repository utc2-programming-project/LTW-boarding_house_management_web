using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Settings;
using Web.Features.Settings.Entities;

namespace Web.Data.Configurations.Settings;

/// <summary>
/// VHT-94 — bảng công tắc Quản lý. 21 dòng seed theo danh mục, mặc định Duyệt
/// (riêng SW-15 và công tắc phụ SW-15a…e mặc định Cho phép).
/// </summary>
public class ManagerToggleConfiguration : IEntityTypeConfiguration<ManagerToggle>
{
    /// <summary>Mốc seed cố định để migration ổn định (không phụ thuộc giờ chạy).</summary>
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<ManagerToggle> builder)
    {
        builder.ToTable("ManagerToggles", t =>
        {
            // SW-15 và công tắc phụ chỉ có Cho phép (0) / Không thể (2).
            t.HasCheckConstraint(
                "CK_ManagerToggles_SW15",
                "[Code] <> 'SW-15' OR [Mode] IN (0, 2)");
            t.HasCheckConstraint(
                "CK_ManagerToggles_SW15Sub",
                "[Code] NOT LIKE 'SW-15_' OR [Mode] IN (0, 2)");
            t.HasCheckConstraint(
                "CK_ManagerToggles_Mode",
                "[Mode] IN (0, 1, 2)");
        });

        builder.HasKey(t => t.Code);
        builder.Property(t => t.Code).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Mode).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();
        builder.Property(t => t.UpdatedByUserId).HasMaxLength(450);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(ManagerToggleCatalog.All.Select(d => new ManagerToggle
        {
            Code = d.Code,
            Mode = d.DefaultMode,
            UpdatedAt = SeedTimestamp,
            UpdatedByUserId = null,
        }));
    }
}
