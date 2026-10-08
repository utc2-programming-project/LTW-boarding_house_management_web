using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Properties.Entities;

namespace Web.Data.Configurations.Properties;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Address).HasMaxLength(500);
        builder.Property(p => p.OwnerId).HasMaxLength(450).IsRequired();

        builder.HasIndex(p => p.OwnerId);
        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasOne<Data.ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
