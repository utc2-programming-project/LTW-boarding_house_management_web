using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Storage.Entities;

namespace Web.Data.Configurations.Storage;

public class FileAccessAuditLogConfiguration : IEntityTypeConfiguration<FileAccessAuditLog>
{
    public void Configure(EntityTypeBuilder<FileAccessAuditLog> builder)
    {
        builder.Property(a => a.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.ActorRole).HasMaxLength(20).IsRequired();
        builder.Property(a => a.RelatedEntityId).HasMaxLength(450);
        builder.Property(a => a.ErrorCode).HasMaxLength(50);
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(20);

        // Truy vấn nhật ký theo tệp/đối tượng và theo người dùng (VHT-104 dùng lại).
        builder.HasIndex(a => new { a.RelatedEntityType, a.RelatedEntityId, a.OccurredAt });
        builder.HasIndex(a => new { a.ActorUserId, a.OccurredAt });
        builder.HasIndex(a => a.FileId);
    }
}
