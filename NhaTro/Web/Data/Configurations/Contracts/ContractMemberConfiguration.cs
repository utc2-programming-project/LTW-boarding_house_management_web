using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;
using Web.Features.Tenants.Entities;

namespace Web.Data.Configurations.Contracts;

public sealed class ContractMemberConfiguration : IEntityTypeConfiguration<ContractMember>
{
    public void Configure(EntityTypeBuilder<ContractMember> builder)
    {
        builder.HasKey(member => new { member.ContractId, member.TenantId });

        builder.HasOne<Contract>()
            .WithMany(contract => contract.Members)
            .HasForeignKey(member => member.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(member => member.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
