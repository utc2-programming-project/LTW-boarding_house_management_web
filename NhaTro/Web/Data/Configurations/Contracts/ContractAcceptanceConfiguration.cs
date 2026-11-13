using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;

namespace Web.Data.Configurations.Contracts;

public sealed class ContractAcceptanceConfiguration : IEntityTypeConfiguration<ContractAcceptance>
{
    public void Configure(EntityTypeBuilder<ContractAcceptance> builder)
    {
        builder.ToTable("ContractAcceptances");
        builder.HasKey(acceptance => acceptance.Id);

        builder.Property(acceptance => acceptance.UserId).HasMaxLength(450).IsRequired();
        builder.Property(acceptance => acceptance.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(acceptance => acceptance.IpAddress).HasMaxLength(64);
        builder.Property(acceptance => acceptance.UserAgent).HasMaxLength(256);
        builder.Property(acceptance => acceptance.SignatureMethod).HasMaxLength(20).IsRequired();

        builder.HasIndex(acceptance => new { acceptance.ContractId, acceptance.ContractVersionId }).IsUnique();
        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(acceptance => acceptance.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(acceptance => acceptance.ContractVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
