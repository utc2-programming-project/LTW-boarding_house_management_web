using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Invoices.Entities;

namespace Web.Data.Configurations.Invoices;

public sealed class PaymentAuditLogConfiguration : IEntityTypeConfiguration<PaymentAuditLog>
{
    public void Configure(EntityTypeBuilder<PaymentAuditLog> builder)
    {
        builder.ToTable("PaymentAuditLogs");

        builder.HasKey(log => log.Id);

        builder.Property(log => log.Action).HasConversion<int>();
        builder.Property(log => log.FromStatus).HasConversion<int?>();
        builder.Property(log => log.ToStatus).HasConversion<int>();
        builder.Property(log => log.Amount).HasPrecision(18, 2);
        builder.Property(log => log.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(log => log.ActorRole).HasMaxLength(20).IsRequired();
        builder.Property(log => log.Note).HasMaxLength(500);
        builder.Property(log => log.OccurredAt).HasColumnType("datetimeoffset");

        builder.HasIndex(log => new { log.PaymentId, log.OccurredAt });
        builder.HasIndex(log => new { log.InvoiceId, log.OccurredAt });

        builder.HasOne(log => log.Payment)
            .WithMany()
            .HasForeignKey(log => log.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
