using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Tenants.Entities;

namespace Web.Data.Configurations.Tenants;

/// <summary>PLAN-005: hồ sơ khách ↔ chủ trọ (nhiều-nhiều).</summary>
public class TenantOwnerLinkConfiguration : IEntityTypeConfiguration<TenantOwnerLink>
{
    public void Configure(EntityTypeBuilder<TenantOwnerLink> builder)
    {
        builder.HasKey(link => new { link.TenantId, link.OwnerId });

        builder.Property(link => link.OwnerId).HasMaxLength(450).IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(link => link.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(link => link.OwnerId);
    }
}
