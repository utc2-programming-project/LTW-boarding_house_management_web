using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;

namespace Web.Data.Configurations.Contracts;

public sealed class ContractVersionConfiguration : IEntityTypeConfiguration<ContractVersion>
{
    public void Configure(EntityTypeBuilder<ContractVersion> builder)
    {
        builder.ToTable("ContractVersions");
        builder.HasKey(version => version.Id);

        builder.Property(version => version.SnapshotJson).IsRequired();
        builder.Property(version => version.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(version => version.CreatedByUserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(version => new { version.ContractId, version.VersionNo }).IsUnique();
        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(version => version.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
