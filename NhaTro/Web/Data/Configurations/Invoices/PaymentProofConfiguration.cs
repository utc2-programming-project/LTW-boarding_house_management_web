using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Invoices.Entities;
using Web.Features.Storage.Entities;

namespace Web.Data.Configurations.Invoices;

public sealed class PaymentProofConfiguration : IEntityTypeConfiguration<PaymentProof>
{
    public void Configure(EntityTypeBuilder<PaymentProof> builder)
    {
        builder.ToTable("PaymentProofs");

        builder.HasKey(proof => new { proof.PaymentId, proof.FileId });

        // Mỗi tệp minh chứng chỉ thuộc tối đa một lần gửi thanh toán.
        builder.HasIndex(proof => proof.FileId).IsUnique();

        // Đồng bộ filter với StoredFile (xóa mềm): tệp bị ẩn thì liên kết minh chứng cũng biến mất.
        builder.HasQueryFilter(proof => !proof.File.IsDeleted);

        builder.HasOne(proof => proof.File)
            .WithMany()
            .HasForeignKey(proof => proof.FileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
