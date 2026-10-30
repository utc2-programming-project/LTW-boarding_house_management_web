using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Utilities.Entities;
using Web.Data.Seeding;

namespace Web.Data.Configurations.Utilities;

public sealed class UtilityTariffTierConfiguration : IEntityTypeConfiguration<UtilityTariffTier>
{
    public void Configure(EntityTypeBuilder<UtilityTariffTier> builder)
    {
        builder.ToTable("UtilityTariffTiers", table =>
        {
            table.HasCheckConstraint("CK_UtilityTariffTiers_TierNumber_Positive", "[TierNumber] > 0");
            table.HasCheckConstraint("CK_UtilityTariffTiers_From_NonNegative", "[From] >= 0");
            table.HasCheckConstraint("CK_UtilityTariffTiers_To_After_From", "[To] IS NULL OR [To] > [From]");
            table.HasCheckConstraint("CK_UtilityTariffTiers_UnitPrice_NonNegative", "[UnitPrice] >= 0");
        });

        builder.HasKey(tier => tier.Id);
        builder.Property(tier => tier.From).HasPrecision(18, 4);
        builder.Property(tier => tier.To).HasPrecision(18, 4);
        builder.Property(tier => tier.UnitPrice).HasPrecision(18, 4);
        builder.HasIndex(tier => new { tier.UtilityTariffId, tier.TierNumber }).IsUnique();
        builder.HasData(UtilityTariffSeedData.Tiers);
    }
}
