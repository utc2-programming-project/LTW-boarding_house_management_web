using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;
using Web.Features.Rooms.Entities;

namespace Web.Data.Configurations.Contracts;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.HasKey(contract => contract.ContractId);
        builder.Property(contract => contract.ContractNumber).HasMaxLength(40).IsRequired();
        builder.Property(contract => contract.StartDate).HasColumnType("date");
        builder.Property(contract => contract.EndDate).HasColumnType("date");
        builder.Property(contract => contract.SnapshotPrice).HasPrecision(18, 2);
        builder.Property(contract => contract.DepositAmount).HasPrecision(18, 2);
        builder.Property(contract => contract.Status).HasConversion<int>();

        builder.HasIndex(contract => contract.ContractNumber).IsUnique();
        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(contract => contract.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
