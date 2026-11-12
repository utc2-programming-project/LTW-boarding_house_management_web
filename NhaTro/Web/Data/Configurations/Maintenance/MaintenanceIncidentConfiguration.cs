using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Data;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;

namespace Web.Data.Configurations.Maintenance;

public sealed class MaintenanceIncidentConfiguration : IEntityTypeConfiguration<MaintenanceIncident>
{
    public void Configure(EntityTypeBuilder<MaintenanceIncident> builder)
    {
        builder.ToTable("MaintenanceIncidents", table =>
        {
            table.HasCheckConstraint("CK_MaintenanceIncidents_Category", $"[Category] BETWEEN {(int)IncidentCategory.Electrical} AND {(int)IncidentCategory.Other}");
            table.HasCheckConstraint("CK_MaintenanceIncidents_Urgency", $"[Urgency] BETWEEN {(int)IncidentUrgency.Normal} AND {(int)IncidentUrgency.Emergency}");
            table.HasCheckConstraint("CK_MaintenanceIncidents_Status", $"[Status] BETWEEN {(int)IncidentStatus.New} AND {(int)IncidentStatus.Completed}");
            table.HasCheckConstraint("CK_MaintenanceIncidents_RepairCost", "[RepairCost] IS NULL OR [RepairCost] >= 0");
        });

        builder.HasKey(incident => incident.Id);
        builder.Property(incident => incident.Id).UseIdentityColumn();
        builder.Property(incident => incident.ReportedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(incident => incident.Title).HasMaxLength(200).IsRequired();
        builder.Property(incident => incident.Description).HasMaxLength(2000).IsRequired();
        builder.Property(incident => incident.Category).IsRequired();
        builder.Property(incident => incident.Urgency).IsRequired();
        builder.Property(incident => incident.Status).IsRequired();
        builder.Property(incident => incident.AssigneeName).HasMaxLength(100);
        builder.Property(incident => incident.TechnicalNotes).HasMaxLength(2000);
        builder.Property(incident => incident.RepairCost).HasPrecision(18, 0);
        builder.Property(incident => incident.CreatedAt).IsRequired();
        builder.Property(incident => incident.RowVersion).IsRowVersion();

        builder.HasIndex(incident => incident.RoomId);
        builder.Property(incident => incident.DeletedBy).HasMaxLength(450);
        builder.HasQueryFilter(incident => !incident.IsDeleted);
        builder.HasIndex(incident => incident.IsDeleted);
        builder.HasOne<Room>().WithMany().HasForeignKey(incident => incident.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(incident => incident.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(incident => incident.History).WithOne().HasForeignKey(history => history.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
