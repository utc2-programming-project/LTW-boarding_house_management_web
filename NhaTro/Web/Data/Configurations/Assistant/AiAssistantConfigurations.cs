using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Data;
using Web.Features.Assistant.Entities;
using Web.Features.Assistant.Enums;
using Web.Features.Properties.Entities;

namespace Web.Data.Configurations.Assistant;

public sealed class AiSuggestionConfiguration : IEntityTypeConfiguration<AiSuggestion>
{
    public void Configure(EntityTypeBuilder<AiSuggestion> builder)
    {
        builder.ToTable("AiSuggestions", table => table.HasCheckConstraint(
            "CK_AiSuggestions_Status",
            $"[Status] IN ({(int)AiSuggestionStatus.Pending}, {(int)AiSuggestionStatus.Approved}, {(int)AiSuggestionStatus.Rejected}, {(int)AiSuggestionStatus.Applied})"));

        builder.HasKey(item => item.Id);
        builder.Property(item => item.UseCase).IsRequired();
        builder.Property(item => item.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.ScopeJson).IsRequired();
        builder.Property(item => item.InputRefsJson).IsRequired();
        builder.Property(item => item.OutputJson).IsRequired();
        builder.Property(item => item.Model).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Status).IsRequired();
        builder.Property(item => item.DecidedByUserId).HasMaxLength(450);
        builder.Property(item => item.DecisionReason).HasMaxLength(500);
        builder.Property(item => item.AppliedRefId).HasMaxLength(100);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.Status, item.CreatedAt });
        builder.HasIndex(item => new { item.RequestedByUserId, item.Status });
        builder.HasIndex(item => new { item.PropertyId, item.UseCase, item.CreatedAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(item => item.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("AiUsageLogs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.UseCase).IsRequired();
        builder.Property(item => item.Model).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Cost).HasPrecision(18, 8);
        builder.Property(item => item.ErrorCode).HasMaxLength(50);
        builder.Property(item => item.CreatedAt).IsRequired();

        builder.HasIndex(item => new { item.UserId, item.CreatedAt });
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiAssistantSettingsConfiguration : IEntityTypeConfiguration<AiAssistantSettings>
{
    public void Configure(EntityTypeBuilder<AiAssistantSettings> builder)
    {
        builder.ToTable("AiAssistantSettings");
        builder.HasKey(item => item.OwnerUserId);
        builder.Property(item => item.OwnerUserId).HasMaxLength(450);
        builder.Property(item => item.DailyRequestLimit).IsRequired();
        builder.Property(item => item.UpdatedAt).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
