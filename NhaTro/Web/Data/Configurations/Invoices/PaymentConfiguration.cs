using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;

namespace Web.Data.Configurations.Invoices;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Payments_Status", "[Status] IN (0, 1, 2)");
            table.HasCheckConstraint(
                "CK_Payments_ReviewedAt",
                "([Status] = 0 AND [ReviewedAt] IS NULL) OR ([Status] <> 0 AND [ReviewedAt] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Payments_RejectionReason",
                "([Status] <> 2 AND [RejectionReason] IS NULL) OR ([Status] = 2 AND [RejectionReason] IS NOT NULL)");
        });

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Status).HasConversion<int>();
        builder.Property(payment => payment.TransactionCode).HasMaxLength(100);
        builder.Property(payment => payment.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(payment => payment.ReviewedByUserId).HasMaxLength(450);
        builder.Property(payment => payment.RejectionReason).HasMaxLength(500);
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        // Một hóa đơn chỉ có tối đa một yêu cầu đang chờ và tối đa một yêu cầu đã xác nhận;
        // Rejected không bị giới hạn để khách được gửi lại minh chứng mới.
        // Index lọc là chốt chặn cuối khi hai luồng gửi/xác nhận chạy đồng thời.
        builder.HasIndex(payment => new { payment.InvoiceId, payment.Status })
            .IsUnique()
            .HasFilter("[Status] IN (0, 1)")
            .HasDatabaseName("UX_Payments_InvoiceStatus");
        builder.HasIndex(payment => new { payment.InvoiceId, payment.Status, payment.RequestedAt });

        builder.HasOne(payment => payment.Invoice)
            .WithMany()
            .HasForeignKey(payment => payment.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(payment => payment.Proofs)
            .WithOne(proof => proof.Payment)
            .HasForeignKey(proof => proof.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
