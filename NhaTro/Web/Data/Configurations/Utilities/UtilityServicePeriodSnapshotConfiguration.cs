using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Utilities.Entities;

namespace Web.Data.Configurations.Utilities;

public sealed class UtilityServicePeriodSnapshotConfiguration : IEntityTypeConfiguration<UtilityServicePeriodSnapshot>
{
    public void Configure(EntityTypeBuilder<UtilityServicePeriodSnapshot> builder)
    {
        builder.ToTable("UtilityServicePeriodSnapshots", table =>
        {
            table.HasCheckConstraint("CK_UtilityServicePeriodSnapshots_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_UtilityServicePeriodSnapshots_UnitPrice", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_UtilityServicePeriodSnapshots_Amount", "[Amount] >= 0");
        });

        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Description).HasMaxLength(200).IsRequired();
        builder.Property(snapshot => snapshot.Unit).HasMaxLength(30).IsRequired();
        builder.Property(snapshot => snapshot.Quantity).HasPrecision(18, 4);
        // Giá dịch vụ nguồn cho phép 4 chữ số thập phân; snapshot phải giữ nguyên để P6 nhận
        // đúng dữ liệu đã chốt, còn Amount vẫn là VND đã làm tròn.
        builder.Property(snapshot => snapshot.UnitPrice).HasPrecision(18, 4);
        builder.Property(snapshot => snapshot.Amount).HasPrecision(18, 0);
        builder.HasIndex(snapshot => new { snapshot.UtilityReadingPeriodId, snapshot.SourceServicePriceHistoryId })
            .IsUnique();
        builder.HasOne<UtilityReadingPeriod>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.UtilityReadingPeriodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
