using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Storage.Entities;

namespace Web.Data.Configurations.Storage;

public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        // Id do service sinh (Guid.NewGuid) trước khi ghi, không để DB sinh lại.
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.FileName).HasMaxLength(260).IsRequired();
        builder.Property(f => f.StoredFileName).HasMaxLength(128).IsRequired();
        builder.Property(f => f.RelativePath).HasMaxLength(512).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(f => f.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(f => f.RelatedEntityId).HasMaxLength(450).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(500);

        // Xóa mềm: danh sách/tải chỉ thấy tệp chưa xóa; tra cứu để xóa dùng IgnoreQueryFilters.
        builder.HasQueryFilter(f => !f.IsDeleted);

        builder.HasIndex(f => new { f.RelatedEntityType, f.RelatedEntityId });
        builder.HasIndex(f => f.UploadedByUserId);

        builder.HasOne<Web.Data.ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
