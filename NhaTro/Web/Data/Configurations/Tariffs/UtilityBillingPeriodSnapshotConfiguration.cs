using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Data.Tariffs;

namespace Web.Data.Configurations.Tariffs;

public sealed class UtilityBillingPeriodSnapshotConfiguration : IEntityTypeConfiguration<UtilityBillingPeriodSnapshot>
{
    public void Configure(EntityTypeBuilder<UtilityBillingPeriodSnapshot> builder)
    {
        builder.ToTable("UtilityBillingPeriodSnapshots", table =>
        {
            table.HasCheckConstraint("CK_UtilityBillingPeriodSnapshots_Period", "[PeriodEnd] >= [PeriodStart]");
            table.HasCheckConstraint("CK_UtilityBillingPeriodSnapshots_BillingPeriodId", "[BillingPeriodId] > 0");
            table.HasCheckConstraint("CK_UtilityBillingPeriodSnapshots_Quantity", "[Quantity] >= 0");
            table.HasCheckConstraint("CK_UtilityBillingPeriodSnapshots_ResidentCount", "[ResidentCount] > 0");
            table.HasCheckConstraint("CK_UtilityBillingPeriodSnapshots_Total", "[Total] >= 0");
        });

        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Type).HasConversion<int>();
        builder.Property(snapshot => snapshot.ScopeCode).HasMaxLength(50).IsRequired();
        builder.Property(snapshot => snapshot.Unit).HasMaxLength(20).IsRequired();
        builder.Property(snapshot => snapshot.EffectiveDate).HasColumnType("date");
        builder.Property(snapshot => snapshot.PeriodStart).HasColumnType("date");
        builder.Property(snapshot => snapshot.PeriodEnd).HasColumnType("date");
        builder.Property(snapshot => snapshot.Quantity).HasPrecision(18, 4);
        builder.Property(snapshot => snapshot.Total).HasPrecision(18, 4);
        builder.Property(snapshot => snapshot.SourceName).HasMaxLength(200).IsRequired();
        builder.Property(snapshot => snapshot.SourceReference).HasMaxLength(500).IsRequired();
        builder.Property(snapshot => snapshot.CreatedAt).HasColumnType("datetime2");
        builder.HasIndex(snapshot => new { snapshot.BillingPeriodId, snapshot.Type }).IsUnique();
        builder.HasMany(snapshot => snapshot.CostLines)
            .WithOne(line => line.PeriodSnapshot)
            .HasForeignKey(line => line.UtilityBillingPeriodSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
