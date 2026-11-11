using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Data;
using Web.Features.Utilities.Entities;

namespace Web.Data.Configurations.Utilities;

public sealed class ServicePriceHistoryConfiguration : IEntityTypeConfiguration<ServicePriceHistory>
{
    public void Configure(EntityTypeBuilder<ServicePriceHistory> builder)
    {
        builder.ToTable("ServicePriceHistory", table =>
        {
            table.HasCheckConstraint("CK_ServicePriceHistory_UnitPrice_Positive", "[UnitPrice] > 0");
            table.HasCheckConstraint("CK_ServicePriceHistory_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint("CK_ServicePriceHistory_Unit_NotBlank", "LEN(LTRIM(RTRIM([Unit]))) > 0");
            table.HasCheckConstraint("CK_ServicePriceHistory_CalculationMethod_NotBlank", "LEN(LTRIM(RTRIM([CalculationMethod]))) > 0");
            table.HasCheckConstraint("CK_ServicePriceHistory_EffectiveTo_After_EffectiveDate", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveDate]");
        });

        builder.HasKey(version => version.Id);
        builder.Property(version => version.Name).HasMaxLength(150).IsRequired();
        builder.Property(version => version.Unit).HasMaxLength(30).IsRequired();
        builder.Property(version => version.CalculationMethod).HasMaxLength(100).IsRequired();
        builder.Property(version => version.UnitPrice).HasPrecision(18, 4);
        builder.Property(version => version.EffectiveDate).HasColumnType("date");
        builder.Property(version => version.EffectiveTo).HasColumnType("date");
        builder.Property(version => version.CreatedAt).HasColumnType("datetime2");
        builder.Property(version => version.CreatedByUserId).HasMaxLength(450);
        builder.Property(version => version.Note).HasMaxLength(500);
        builder.HasIndex(version => new { version.ServiceCatalogId, version.EffectiveDate }).IsUnique();
        builder.HasIndex(version => new { version.EffectiveDate, version.ServiceCatalogId });
        builder.HasIndex(version => version.CreatedByUserId);

        // Đồng bộ filter với ServiceCatalog (xóa mềm): giữ dòng lịch sử nhưng ẩn theo danh mục.
        builder.HasQueryFilter(version => !version.ServiceCatalog.IsDeleted);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(version => version.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
