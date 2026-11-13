using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;

namespace Web.Data.Configurations.Contracts;

public sealed class ContractChangeRequestConfiguration : IEntityTypeConfiguration<ContractChangeRequest>
{
    public void Configure(EntityTypeBuilder<ContractChangeRequest> builder)
    {
        builder.ToTable("ContractChangeRequests");
        builder.HasKey(request => request.Id);

        builder.Property(request => request.Message).HasMaxLength(2000).IsRequired();
        builder.Property(request => request.Status).HasConversion<int>();
        builder.Property(request => request.ResolvedByUserId).HasMaxLength(450);
        builder.Property(request => request.ResolutionNote).HasMaxLength(1000);

        builder.HasIndex(request => new { request.ContractId, request.Status });
        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(request => request.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContractVersion>()
            .WithMany()
            .HasForeignKey(request => request.ContractVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
