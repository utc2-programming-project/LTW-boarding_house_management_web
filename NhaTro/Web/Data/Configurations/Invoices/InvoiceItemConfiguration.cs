using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Invoices.Entities;

namespace Web.Data.Configurations.Invoices;

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems", table =>
        {
            table.HasCheckConstraint("CK_InvoiceItems_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_InvoiceItems_UnitPrice", "[UnitPrice] >= 0");
            // Discount = 4: số tiền âm để tổng hóa đơn trừ đúng khoản giảm; loại khác không âm.
            table.HasCheckConstraint(
                "CK_InvoiceItems_AmountSign",
                "([ItemType] = 4 AND [Amount] <= 0) OR ([ItemType] <> 4 AND [Amount] >= 0)");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.ItemType).HasConversion<int>();
        builder.Property(item => item.Description).HasMaxLength(250).IsRequired();
        builder.Property(item => item.Unit).HasMaxLength(30);
        builder.Property(item => item.Quantity).HasPrecision(18, 4);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 4);
        builder.Property(item => item.Amount).HasPrecision(18, 2);

        builder.HasIndex(item => new { item.InvoiceId, item.DisplayOrder });
    }
}
