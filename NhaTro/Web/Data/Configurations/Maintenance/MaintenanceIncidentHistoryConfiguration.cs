using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;

namespace Web.Data.Configurations.Maintenance;

public sealed class MaintenanceIncidentHistoryConfiguration : IEntityTypeConfiguration<MaintenanceIncidentHistory>
{
    public void Configure(EntityTypeBuilder<MaintenanceIncidentHistory> builder)
    {
        builder.ToTable("MaintenanceIncidentHistories", table =>
        {
            table.HasCheckConstraint("CK_MaintenanceIncidentHistories_Action", $"[Action] BETWEEN {(int)IncidentHistoryAction.Created} AND {(int)IncidentHistoryAction.Updated}");
            table.HasCheckConstraint("CK_MaintenanceIncidentHistories_FromStatus", $"[FromStatus] IS NULL OR [FromStatus] BETWEEN {(int)IncidentStatus.New} AND {(int)IncidentStatus.Completed}");
            table.HasCheckConstraint("CK_MaintenanceIncidentHistories_ToStatus", $"[ToStatus] BETWEEN {(int)IncidentStatus.New} AND {(int)IncidentStatus.Completed}");
        });

        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).UseIdentityColumn();
        builder.Property(history => history.Action).IsRequired();
        builder.Property(history => history.ToStatus).IsRequired();
        builder.Property(history => history.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(history => history.Note).HasMaxLength(1000);
        builder.Property(history => history.CreatedAt).IsRequired();
        builder.HasIndex(history => history.IncidentId);
    }
}
