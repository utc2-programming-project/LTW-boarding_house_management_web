using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Utilities.Entities;
using Web.Data.Seeding;

namespace Web.Data.Configurations.Utilities;

public sealed class UtilityTariffConfiguration : IEntityTypeConfiguration<UtilityTariff>
{
    public void Configure(EntityTypeBuilder<UtilityTariff> builder)
    {
        builder.ToTable("UtilityTariffs", table =>
            table.HasCheckConstraint("CK_UtilityTariffs_ReadOnly", "[IsReadOnly] = 1"));
        builder.HasKey(tariff => tariff.Id);
        builder.Property(tariff => tariff.Type).HasConversion<int>();
        builder.Property(tariff => tariff.ScopeCode).HasMaxLength(50).IsRequired();
        builder.Property(tariff => tariff.Unit).HasMaxLength(20).IsRequired();
        builder.Property(tariff => tariff.EffectiveDate).HasColumnType("date");
        builder.Property(tariff => tariff.SourceName).HasMaxLength(200).IsRequired();
        builder.Property(tariff => tariff.SourceReference).HasMaxLength(500).IsRequired();
        builder.Property(tariff => tariff.IsReadOnly).HasDefaultValue(true);
        builder.HasIndex(tariff => new { tariff.Type, tariff.ScopeCode, tariff.EffectiveDate }).IsUnique();
        builder.HasData(UtilityTariffSeedData.Tariffs);
        builder.HasMany(tariff => tariff.Tiers)
            .WithOne(tier => tier.UtilityTariff)
            .HasForeignKey(tier => tier.UtilityTariffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
