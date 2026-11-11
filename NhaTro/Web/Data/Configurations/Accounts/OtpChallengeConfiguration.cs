using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Accounts.Otp;

namespace Web.Data.Configurations.Accounts;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("OtpChallenges");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.UserId).HasMaxLength(450);
        builder.Property(item => item.Email).HasMaxLength(256).IsRequired();
        builder.Property(item => item.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Salt).HasMaxLength(32).IsRequired();
        builder.Property(item => item.IpAddress).HasMaxLength(64);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => new { item.UserId, item.Purpose, item.ConsumedAt });
        builder.HasIndex(item => new { item.Email, item.CreatedAt });
        builder.HasIndex(item => item.ExpiresAt);
    }
}
