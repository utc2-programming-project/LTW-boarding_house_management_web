using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.Entities;

namespace Web.Data.Configurations.Invoices;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", table =>
        {
            table.HasCheckConstraint("CK_Invoices_Period", "[PeriodEnd] >= [PeriodStart]");
            table.HasCheckConstraint("CK_Invoices_Status", "[Status] IN (0, 1, 2)");
            table.HasCheckConstraint("CK_Invoices_TotalAmount", "[TotalAmount] >= 0");
        });

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.PeriodStart).HasColumnType("date");
        builder.Property(invoice => invoice.PeriodEnd).HasColumnType("date");
        builder.Property(invoice => invoice.DueDate).HasColumnType("date");
        builder.Property(invoice => invoice.Status).HasConversion<int>();
        builder.Property(invoice => invoice.TotalAmount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.RowVersion).IsRowVersion();

        // Một hợp đồng chỉ có một hóa đơn cho mỗi kỳ bắt đầu (VHT-34 mục 7).
        // Một hợp đồng chỉ có một hóa đơn theo tháng/P5 (RegistrationId IS NULL);
        // hóa đơn dịch vụ theo yêu cầu đăng ký (RegistrationId NOT NULL) được phép cùng kỳ.
        builder.HasIndex(invoice => new { invoice.ContractId, invoice.PeriodStart })
            .IsUnique()
            .HasFilter("[RegistrationId] IS NULL");
        builder.HasIndex(invoice => new { invoice.PeriodStart, invoice.Status });
        builder.HasIndex(invoice => invoice.RegistrationId);

        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(invoice => invoice.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(invoice => invoice.Items)
            .WithOne(item => item.Invoice)
            .HasForeignKey(item => item.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
