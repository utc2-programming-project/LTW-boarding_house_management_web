using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Data.Tariffs;

namespace Web.Data.Configurations.Tariffs;

public sealed class UtilityBillingCostLineSnapshotConfiguration : IEntityTypeConfiguration<UtilityBillingCostLineSnapshot>
{
    public void Configure(EntityTypeBuilder<UtilityBillingCostLineSnapshot> builder)
    {
        builder.ToTable("UtilityBillingCostLineSnapshots", table =>
        {
            table.HasCheckConstraint("CK_UtilityBillingCostLineSnapshots_TierNumber", "[TierNumber] > 0");
            table.HasCheckConstraint("CK_UtilityBillingCostLineSnapshots_Quantity", "[Quantity] >= 0");
            table.HasCheckConstraint("CK_UtilityBillingCostLineSnapshots_UnitPrice", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_UtilityBillingCostLineSnapshots_Amount", "[Amount] >= 0");
            table.HasCheckConstraint("CK_UtilityBillingCostLineSnapshots_Range", "[To] IS NULL OR [To] > [From]");
        });

        builder.HasKey(line => line.Id);
        builder.Property(line => line.From).HasPrecision(18, 4);
        builder.Property(line => line.To).HasPrecision(18, 4);
        builder.Property(line => line.EffectiveDate).HasColumnType("date");
        builder.Property(line => line.Quantity).HasPrecision(18, 4);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 4);
        builder.Property(line => line.Amount).HasPrecision(18, 4);
        builder.HasIndex(line => new { line.UtilityBillingPeriodSnapshotId, line.TariffVersionId, line.TierNumber }).IsUnique();
        builder.HasOne(line => line.TariffVersion)
            .WithMany()
            .HasForeignKey(line => line.TariffVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
