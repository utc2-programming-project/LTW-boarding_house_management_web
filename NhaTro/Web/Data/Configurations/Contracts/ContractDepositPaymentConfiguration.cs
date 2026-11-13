using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;

namespace Web.Data.Configurations.Contracts;

public sealed class ContractDepositPaymentConfiguration : IEntityTypeConfiguration<ContractDepositPayment>
{
    public void Configure(EntityTypeBuilder<ContractDepositPayment> builder)
    {
        builder.ToTable("ContractDepositPayments");
        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Status).HasConversion<int>();
        builder.Property(payment => payment.TransactionCode).HasMaxLength(100);
        builder.Property(payment => payment.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(payment => payment.ReviewedByUserId).HasMaxLength(450);
        builder.Property(payment => payment.RejectionReason).HasMaxLength(500);
        builder.Property(payment => payment.ProofFileIdsJson).HasMaxLength(4000);
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        // Một hợp đồng chỉ có tối đa một phiếu đang chờ xác nhận.
        builder.HasIndex(payment => new { payment.ContractId, payment.Status })
            .IsUnique()
            .HasFilter("[Status] = 0")
            .HasDatabaseName("UX_ContractDepositPayments_Pending");

        builder.HasIndex(payment => new { payment.ContractId, payment.RequestedAt });

        builder.HasOne(payment => payment.Contract)
            .WithMany()
            .HasForeignKey(payment => payment.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
