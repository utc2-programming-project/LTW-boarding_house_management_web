using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Utilities.Entities;

namespace Web.Data.Configurations.Utilities;

public sealed class UtilityReadingPeriodConfiguration : IEntityTypeConfiguration<UtilityReadingPeriod>
{
    public void Configure(EntityTypeBuilder<UtilityReadingPeriod> builder)
    {
        builder.ToTable("UtilityReadingPeriods", table =>
        {
            table.HasCheckConstraint("CK_UtilityReadingPeriods_Period", "[PeriodEnd] >= [PeriodStart]");
            table.HasCheckConstraint(
                "CK_UtilityReadingPeriods_Electric",
                "[ElectricStart] >= 0 AND [ElectricEnd] >= [ElectricStart]");
            table.HasCheckConstraint(
                "CK_UtilityReadingPeriods_Water",
                "[WaterStart] >= 0 AND [WaterEnd] >= [WaterStart]");
            table.HasCheckConstraint("CK_UtilityReadingPeriods_ResidentCount", "[ResidentCount] > 0");
            table.HasCheckConstraint("CK_UtilityReadingPeriods_Status", "[Status] IN (0, 1)");
            // D10: kỳ đã phát hành hóa đơn bắt buộc đã có chỉ số và có hóa đơn để đọc lại tiền.
            table.HasCheckConstraint(
                "CK_UtilityReadingPeriods_InvoicedHasInvoice",
                "[Status] = 0 OR [InvoiceId] IS NOT NULL");
            table.HasCheckConstraint(
                "CK_UtilityReadingPeriods_InvoicedHasReadings",
                "[Status] = 0 OR [HasReadings] = 1");
        });

        builder.HasKey(period => period.Id);
        builder.Property(period => period.PeriodStart).HasColumnType("date");
        builder.Property(period => period.PeriodEnd).HasColumnType("date");
        builder.Property(period => period.ElectricStart).HasPrecision(18, 4);
        builder.Property(period => period.ElectricEnd).HasPrecision(18, 4);
        builder.Property(period => period.WaterStart).HasPrecision(18, 4);
        builder.Property(period => period.WaterEnd).HasPrecision(18, 4);
        builder.Property(period => period.Status).HasConversion<int>();
        builder.Property(period => period.CreatedAt).HasColumnType("datetime2");
        builder.Property(period => period.UpdatedAt).HasColumnType("datetime2");
        builder.Property(period => period.RowVersion).IsRowVersion();

        // Một hợp đồng chỉ có một kỳ cho mỗi ngày bắt đầu (chống ghi trùng kỳ).
        builder.HasIndex(period => new { period.ContractId, period.PeriodStart }).IsUnique();
        builder.HasIndex(period => new { period.ContractId, period.PeriodEnd });

        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(period => period.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(period => period.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(period => period.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
