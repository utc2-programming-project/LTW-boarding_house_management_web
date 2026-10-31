using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Utilities.Entities;

namespace Web.Data.Configurations.Utilities;

public sealed class ServiceCatalogConfiguration : IEntityTypeConfiguration<ServiceCatalog>
{
    public void Configure(EntityTypeBuilder<ServiceCatalog> builder)
    {
        builder.ToTable("ServiceCatalogs");
        builder.HasKey(catalog => catalog.Id);
        builder.Property(catalog => catalog.IsReadOnly).HasDefaultValue(false);
        builder.Property(catalog => catalog.CreatedAt).HasColumnType("datetime2");
        builder.HasOne<Web.Features.Properties.Entities.Property>()
            .WithMany()
            .HasForeignKey(catalog => catalog.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(catalog => catalog.PriceHistory)
            .WithOne(version => version.ServiceCatalog)
            .HasForeignKey(version => version.ServiceCatalogId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
