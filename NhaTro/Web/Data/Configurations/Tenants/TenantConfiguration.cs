using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Tenants.Entities;

namespace Web.Data.Configurations.Tenants;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(t => t.FullName).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Phone).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Email).HasMaxLength(256);
        builder.Property(t => t.CitizenId).HasMaxLength(20);

        builder.HasIndex(t => t.CitizenId)
            .IsUnique()
            .HasFilter("[CitizenId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasOne<Data.ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
