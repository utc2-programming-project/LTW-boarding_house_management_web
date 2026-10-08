using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Rooms.Entities;

namespace Web.Data.Configurations.Rooms;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
        builder.Property(r => r.StandardPrice).HasPrecision(18, 2);
        builder.Property(r => r.AreaM2).HasPrecision(10, 2);

        builder.HasIndex(r => new { r.PropertyId, r.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.HasOne<Features.Properties.Entities.Property>()
            .WithMany()
            .HasForeignKey(r => r.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
