using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Maintenance.Entities;

namespace Web.Data.Configurations.Maintenance;

public sealed class IncidentPhotoAttachmentConfiguration : IEntityTypeConfiguration<IncidentPhotoAttachment>
{
    public void Configure(EntityTypeBuilder<IncidentPhotoAttachment> builder)
    {
        builder.ToTable("IncidentPhotoAttachments", table =>
        {
            table.HasCheckConstraint("CK_IncidentPhotoAttachments_Kind", "[Kind] IN (0, 1)");
            table.HasCheckConstraint("CK_IncidentPhotoAttachments_Target", "[IncidentId] IS NOT NULL OR [ApprovalRequestId] IS NOT NULL");
        });
        builder.HasKey(photo => photo.FileId);
        builder.Property(photo => photo.FileId).ValueGeneratedNever();
        builder.HasOne(photo => photo.File).WithOne().HasForeignKey<IncidentPhotoAttachment>(photo => photo.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(photo => photo.Incident).WithMany().HasForeignKey(photo => photo.IncidentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(photo => photo.ApprovalRequest).WithMany().HasForeignKey(photo => photo.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(photo => new { photo.IncidentId, photo.Kind });
        builder.HasIndex(photo => photo.ApprovalRequestId);
    }
}
