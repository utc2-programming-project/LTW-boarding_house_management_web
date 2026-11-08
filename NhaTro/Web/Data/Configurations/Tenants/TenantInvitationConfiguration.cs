using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Tenants.Entities;

namespace Web.Data.Configurations.Tenants;

/// <summary>PLAN-006: cấu hình bảng lời mời khách thuê.</summary>
public class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.FullName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Phone).HasMaxLength(20);
        builder.Property(i => i.OwnerId).HasMaxLength(450).IsRequired();
        builder.Property(i => i.InvitedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(i => i.RowVersion).IsRowVersion();

        // Tra cứu khi khách mở liên kết.
        builder.HasIndex(i => i.TokenHash).IsUnique();

        // Danh sách lời mời theo chủ trọ và theo email.
        builder.HasIndex(i => new { i.OwnerId, i.AcceptedAt });
        builder.HasIndex(i => i.Email);
    }
}
