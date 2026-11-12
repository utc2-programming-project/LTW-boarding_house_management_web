using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Invoices.Entities;

namespace Web.Data.Configurations.Invoices;

public sealed class InvoiceRevisionConfiguration : IEntityTypeConfiguration<InvoiceRevision>
{
    public void Configure(EntityTypeBuilder<InvoiceRevision> builder)
    {
        builder.ToTable("InvoiceRevisions");

        builder.HasKey(revision => revision.Id);

        builder.Property(revision => revision.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(revision => revision.PreviousTotalAmount).HasPrecision(18, 2);
        builder.Property(revision => revision.NewTotalAmount).HasPrecision(18, 2);
        builder.Property(revision => revision.ChangedByUserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(revision => revision.InvoiceId);

        // KHÔNG khai báo FK tới Invoices: khi Invoice là "required end" của một quan hệ
        // mà dependent không có query filter tương ứng, EF sẽ BỎ QUA query filter xoá mềm
        // của Invoice — hoá đơn đã xoá vẫn hiện trong danh sách. Vì vậy ở đây chỉ giữ
        // quan hệ logic (InvoiceId); service dọn bản ghi khi xoá hoá đơn.
    }
}
