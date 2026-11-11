using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Tenants.Entities;

namespace Web.Data.Configurations.Tenants;

public sealed class PendingRoomAssignmentConfiguration : IEntityTypeConfiguration<PendingRoomAssignment>
{
    public void Configure(EntityTypeBuilder<PendingRoomAssignment> builder)
    {
        builder.ToTable("PendingRoomAssignments");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.CreatedByUserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(item => new { item.TenantId, item.RoomId, item.Status }).IsUnique();
        builder.HasIndex(item => item.InvitationId);
        builder.HasIndex(item => item.PropertyId);
    }
}
