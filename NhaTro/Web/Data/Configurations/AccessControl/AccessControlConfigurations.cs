using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Data;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.Contracts.Entities;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Tenants.Entities;

namespace Web.Data.Configurations.AccessControl;

public sealed class GatePolicyConfiguration : IEntityTypeConfiguration<GatePolicy>
{
    public void Configure(EntityTypeBuilder<GatePolicy> builder)
    {
        builder.ToTable("GatePolicies", table =>
        {
            table.HasCheckConstraint("CK_GatePolicies_AuthMode", $"[AuthMode] BETWEEN {(int)GateAuthMode.SingleFactor} AND {(int)GateAuthMode.FaceOnly}");
            table.HasCheckConstraint("CK_GatePolicies_TenantQuota", "[TenantMaxPassesPerDay] BETWEEN 0 AND 50 AND [TenantMaxUses] BETWEEN 1 AND 10");
            table.HasCheckConstraint("CK_GatePolicies_Hours", "[DayVisitStartHour] BETWEEN 0 AND 23 AND [DayVisitEndHour] BETWEEN 1 AND 24 AND [DayVisitStartHour] < [DayVisitEndHour]");
        });

        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.Id).UseIdentityColumn();
        builder.Property(policy => policy.RequiredGuestFields).HasMaxLength(500).IsRequired();
        builder.Property(policy => policy.UpdatedByUserId).HasMaxLength(450);
        builder.Property(policy => policy.UpdatedAt).IsRequired();
        builder.Property(policy => policy.RowVersion).IsRowVersion();

        builder.HasIndex(policy => policy.PropertyId).IsUnique();
        builder.HasOne<Property>().WithMany().HasForeignKey(policy => policy.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GateDeviceConfiguration : IEntityTypeConfiguration<GateDevice>
{
    public void Configure(EntityTypeBuilder<GateDevice> builder)
    {
        builder.ToTable("GateDevices", table =>
        {
            table.HasCheckConstraint("CK_GateDevices_Kind", $"[Kind] BETWEEN {(int)GateDeviceKind.Kiosk} AND {(int)GateDeviceKind.Camera}");
            table.HasCheckConstraint("CK_GateDevices_Level", $"[Level] BETWEEN {(int)GateLevel.A} AND {(int)GateLevel.B}");
        });

        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).UseIdentityColumn();
        builder.Property(device => device.Name).HasMaxLength(150).IsRequired();
        builder.Property(device => device.Note).HasMaxLength(500);
        builder.Property(device => device.CreatedAt).IsRequired();
        builder.Property(device => device.RowVersion).IsRowVersion();

        builder.HasIndex(device => new { device.PropertyId, device.Name }).IsUnique();
        builder.HasOne<Property>().WithMany().HasForeignKey(device => device.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GatePassConfiguration : IEntityTypeConfiguration<GatePass>
{
    public void Configure(EntityTypeBuilder<GatePass> builder)
    {
        builder.ToTable("GatePasses", table =>
        {
            table.HasCheckConstraint("CK_GatePasses_Status", $"[Status] BETWEEN {(int)GatePassStatus.Pending} AND {(int)GatePassStatus.Revoked}");
            table.HasCheckConstraint("CK_GatePasses_Uses", "[MaxUses] BETWEEN 1 AND 50 AND [UsedCount] >= 0");
            table.HasCheckConstraint("CK_GatePasses_Issuer", $"[IssuerRole] BETWEEN {(int)GateIssuerRole.ChuTro} AND {(int)GateIssuerRole.System}");
        });

        builder.HasKey(pass => pass.Id);
        builder.Property(pass => pass.Id).UseIdentityColumn();
        builder.Property(pass => pass.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(pass => pass.CodeHash).HasMaxLength(64);
        builder.Property(pass => pass.VisitorName).HasMaxLength(200).IsRequired();
        builder.Property(pass => pass.VisitorPhone).HasMaxLength(30);
        builder.Property(pass => pass.VisitorIdNumber).HasMaxLength(50);
        builder.Property(pass => pass.StayReason).HasMaxLength(500);
        builder.Property(pass => pass.VisitorPhotoRef).HasMaxLength(450);
        builder.Property(pass => pass.Note).HasMaxLength(1000);
        builder.Property(pass => pass.IssuedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(pass => pass.ApprovedByUserId).HasMaxLength(450);
        builder.Property(pass => pass.RejectedReason).HasMaxLength(500);
        builder.Property(pass => pass.RevokedReason).HasMaxLength(500);
        builder.Property(pass => pass.RevokedByUserId).HasMaxLength(450);
        builder.Property(pass => pass.CreatedAt).IsRequired();
        builder.Property(pass => pass.RowVersion).IsRowVersion();

        builder.HasIndex(pass => pass.TokenHash).IsUnique();
        builder.HasIndex(pass => new { pass.PropertyId, pass.Status });
        builder.HasIndex(pass => pass.ExpiresAt);
        builder.HasIndex(pass => new { pass.RoomId, pass.CreatedAt });

        builder.HasOne<Property>().WithMany().HasForeignKey(pass => pass.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(pass => pass.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(pass => pass.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Contract>().WithMany().HasForeignKey(pass => pass.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StayReportConfiguration : IEntityTypeConfiguration<StayReport>
{
    public void Configure(EntityTypeBuilder<StayReport> builder)
    {
        builder.ToTable("StayReports", table =>
        {
            table.HasCheckConstraint("CK_StayReports_Status", $"[Status] BETWEEN {(int)StayReportStatus.Pending} AND {(int)StayReportStatus.Cancelled}");
        });

        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).UseIdentityColumn();
        builder.Property(report => report.GuestName).HasMaxLength(200).IsRequired();
        builder.Property(report => report.GuestIdNumber).HasMaxLength(50);
        builder.Property(report => report.StayReason).HasMaxLength(500);
        builder.Property(report => report.AddressSnapshot).HasMaxLength(500).IsRequired();
        builder.Property(report => report.ReportedByUserId).HasMaxLength(450);
        builder.Property(report => report.EvidenceRef).HasMaxLength(450);
        builder.Property(report => report.Note).HasMaxLength(1000);
        builder.Property(report => report.CreatedAt).IsRequired();
        builder.Property(report => report.RowVersion).IsRowVersion();

        builder.HasIndex(report => report.DueAt);
        builder.HasIndex(report => new { report.PropertyId, report.Status });
        builder.HasIndex(report => report.GatePassId);

        builder.HasOne<Property>().WithMany().HasForeignKey(report => report.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(report => report.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GatePass>().WithMany().HasForeignKey(report => report.GatePassId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class BiometricEnrollmentConfiguration : IEntityTypeConfiguration<BiometricEnrollment>
{
    public void Configure(EntityTypeBuilder<BiometricEnrollment> builder)
    {
        builder.ToTable("BiometricEnrollments", table =>
        {
            table.HasCheckConstraint("CK_BiometricEnrollments_Method", $"[Method] BETWEEN {(int)BiometricMethod.Face} AND {(int)BiometricMethod.Passkey}");
            table.HasCheckConstraint("CK_BiometricEnrollments_Status", $"[Status] BETWEEN {(int)EnrollmentStatus.Pending} AND {(int)EnrollmentStatus.Revoked}");
        });

        builder.HasKey(enrollment => enrollment.Id);
        builder.Property(enrollment => enrollment.Id).UseIdentityColumn();
        builder.Property(enrollment => enrollment.TemplateRef).HasMaxLength(450);
        builder.Property(enrollment => enrollment.PublicKey).HasMaxLength(2000);
        builder.Property(enrollment => enrollment.EnrolledByUserId).HasMaxLength(450).IsRequired();
        builder.Property(enrollment => enrollment.ReviewedByUserId).HasMaxLength(450);
        builder.Property(enrollment => enrollment.RevokeReason).HasMaxLength(500);
        builder.Property(enrollment => enrollment.ConsentAt).IsRequired();
        builder.Property(enrollment => enrollment.CreatedAt).IsRequired();
        builder.Property(enrollment => enrollment.RowVersion).IsRowVersion();

        builder.HasIndex(enrollment => new { enrollment.PropertyId, enrollment.Status });
        builder.HasIndex(enrollment => enrollment.TenantId);

        builder.HasOne<Property>().WithMany().HasForeignKey(enrollment => enrollment.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(enrollment => enrollment.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GatePass>().WithMany().HasForeignKey(enrollment => enrollment.GatePassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GateLogConfiguration : IEntityTypeConfiguration<GateLog>
{
    public void Configure(EntityTypeBuilder<GateLog> builder)
    {
        builder.ToTable("GateLogs", table =>
        {
            table.HasCheckConstraint("CK_GateLogs_Direction", $"[Direction] BETWEEN {(int)GateDirection.In} AND {(int)GateDirection.Out}");
            table.HasCheckConstraint("CK_GateLogs_Subject", $"[SubjectType] BETWEEN {(int)GateSubjectType.Person} AND {(int)GateSubjectType.Vehicle}");
        });

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).UseIdentityColumn();
        builder.Property(log => log.Reason).HasMaxLength(200);
        builder.Property(log => log.SubjectName).HasMaxLength(200);
        builder.Property(log => log.UserId).HasMaxLength(450);
        builder.Property(log => log.OccurredAt).IsRequired();

        builder.HasIndex(log => new { log.PropertyId, log.OccurredAt });
        builder.HasIndex(log => new { log.GatePassId, log.OccurredAt });
        builder.HasIndex(log => new { log.PropertyId, log.Direction, log.IsSuccess });

        builder.HasOne<Property>().WithMany().HasForeignKey(log => log.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GateDevice>().WithMany().HasForeignKey(log => log.GateDeviceId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<GatePass>().WithMany().HasForeignKey(log => log.GatePassId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
