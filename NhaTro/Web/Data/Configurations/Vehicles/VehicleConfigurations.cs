using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Features.Contracts.Entities;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Tenants.Entities;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;

namespace Web.Data.Configurations.Vehicles;

public sealed class VehiclePropertySettingConfiguration : IEntityTypeConfiguration<VehiclePropertySetting>
{
    public void Configure(EntityTypeBuilder<VehiclePropertySetting> builder)
    {
        builder.ToTable("VehiclePropertySettings", table =>
        {
            table.HasCheckConstraint("CK_VehiclePropertySettings_Fees",
                "[ElectricMotorbikeMonthlyFee] >= 0 AND [ManualMotorbikeMonthlyFee] >= 0 AND [ScooterMonthlyFee] >= 0");
            table.HasCheckConstraint("CK_VehiclePropertySettings_MaxVehicles",
                "[MaxVehiclesPerRoom] BETWEEN 0 AND 50");
            table.HasCheckConstraint("CK_VehiclePropertySettings_Threshold",
                "[ZoneNearFullThresholdPercent] BETWEEN 50 AND 100");
        });

        builder.HasKey(setting => setting.Id);
        builder.Property(setting => setting.Id).UseIdentityColumn();
        builder.Property(setting => setting.ElectricMotorbikeMonthlyFee).HasPrecision(18, 2);
        builder.Property(setting => setting.ManualMotorbikeMonthlyFee).HasPrecision(18, 2);
        builder.Property(setting => setting.ScooterMonthlyFee).HasPrecision(18, 2);
        builder.Property(setting => setting.UpdatedByUserId).HasMaxLength(450);
        builder.Property(setting => setting.UpdatedAt).IsRequired();
        builder.Property(setting => setting.RowVersion).IsRowVersion();

        builder.HasIndex(setting => setting.PropertyId).IsUnique();
        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(setting => setting.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParkingZoneConfiguration : IEntityTypeConfiguration<ParkingZone>
{
    public void Configure(EntityTypeBuilder<ParkingZone> builder)
    {
        builder.ToTable("ParkingZones", table =>
        {
            table.HasCheckConstraint("CK_ParkingZones_Capacity", "[Capacity] BETWEEN 1 AND 5000");
            table.HasCheckConstraint("CK_ParkingZones_OverrideFee", "[OverrideMonthlyFee] IS NULL OR [OverrideMonthlyFee] >= 0");
        });

        builder.HasKey(zone => zone.Id);
        builder.Property(zone => zone.Id).UseIdentityColumn();
        builder.Property(zone => zone.Name).HasMaxLength(150).IsRequired();
        builder.Property(zone => zone.SlotPrefix).HasMaxLength(10);
        builder.Property(zone => zone.OverrideMonthlyFee).HasPrecision(18, 2);
        builder.Property(zone => zone.CreatedAt).IsRequired();
        builder.Property(zone => zone.RowVersion).IsRowVersion();

        builder.HasIndex(zone => new { zone.PropertyId, zone.Name }).IsUnique();
        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(zone => zone.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParkingSlotConfiguration : IEntityTypeConfiguration<ParkingSlot>
{
    public void Configure(EntityTypeBuilder<ParkingSlot> builder)
    {
        builder.ToTable("ParkingSlots", table =>
        {
            table.HasCheckConstraint("CK_ParkingSlots_Status",
                $"[Status] BETWEEN {(int)ParkingSlotStatus.Available} AND {(int)ParkingSlotStatus.Maintenance}");
        });

        builder.HasKey(slot => slot.Id);
        builder.Property(slot => slot.Id).UseIdentityColumn();
        builder.Property(slot => slot.Code).HasMaxLength(30).IsRequired();
        builder.Property(slot => slot.RowVersion).IsRowVersion();

        builder.HasIndex(slot => new { slot.ParkingZoneId, slot.Code }).IsUnique();
        builder.HasOne<ParkingZone>()
            .WithMany()
            .HasForeignKey(slot => slot.ParkingZoneId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mỗi xe chỉ giữ tối đa một ô; FK + unique filtered đảm bảo toàn vẹn ở tầng DB.
        builder.HasIndex(slot => slot.CurrentVehicleId)
            .IsUnique()
            .HasFilter("[CurrentVehicleId] IS NOT NULL");
        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(slot => slot.CurrentVehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", table =>
        {
            table.HasCheckConstraint("CK_Vehicles_VehicleType",
                $"[VehicleType] BETWEEN {(int)VehicleType.ElectricMotorbike} AND {(int)VehicleType.Scooter}");
            table.HasCheckConstraint("CK_Vehicles_Status",
                $"[Status] BETWEEN {(int)VehicleStatus.Active} AND {(int)VehicleStatus.Inactive}");
        });

        builder.HasKey(vehicle => vehicle.Id);
        builder.Property(vehicle => vehicle.Id).UseIdentityColumn();
        builder.Property(vehicle => vehicle.PlateNumber).HasMaxLength(30).IsRequired();
        builder.Property(vehicle => vehicle.Brand).HasMaxLength(100);
        builder.Property(vehicle => vehicle.Color).HasMaxLength(60);
        builder.Property(vehicle => vehicle.PhotoRef).HasMaxLength(450);
        builder.Property(vehicle => vehicle.Note).HasMaxLength(500);
        builder.Property(vehicle => vehicle.QrCodeToken).HasMaxLength(64).IsRequired();
        builder.Property(vehicle => vehicle.EndReason).HasMaxLength(500);
        builder.Property(vehicle => vehicle.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(vehicle => vehicle.StartedAt).IsRequired();
        builder.Property(vehicle => vehicle.RowVersion).IsRowVersion();

        // Filtered Unique Index: không bao giờ trùng biển số đang gửi (Active = 1) trong cùng một nhà,
        // trong khi vẫn giữ được lịch sử các bản ghi Inactive.
        builder.HasIndex(vehicle => new { vehicle.PropertyId, vehicle.PlateNumber })
            .IsUnique()
            .HasFilter($"[Status] = {(int)VehicleStatus.Active}");

        builder.HasIndex(vehicle => vehicle.QrCodeToken).IsUnique();
        builder.HasIndex(vehicle => new { vehicle.RoomId, vehicle.Status });
        builder.HasIndex(vehicle => new { vehicle.ContractId, vehicle.Status });
        builder.HasIndex(vehicle => vehicle.ParkingZoneId);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ParkingZone>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.ParkingZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ParkingSlot>()
            .WithMany()
            .HasForeignKey(vehicle => vehicle.ParkingSlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GuestVehicleNoticeConfiguration : IEntityTypeConfiguration<GuestVehicleNotice>
{
    public void Configure(EntityTypeBuilder<GuestVehicleNotice> builder)
    {
        builder.ToTable("GuestVehicleNotices", table =>
        {
            table.HasCheckConstraint("CK_GuestVehicleNotices_VehicleType",
                $"[VehicleType] BETWEEN {(int)VehicleType.ElectricMotorbike} AND {(int)VehicleType.Scooter}");
            table.HasCheckConstraint("CK_GuestVehicleNotices_Status",
                $"[Status] BETWEEN {(int)GuestVehicleNoticeStatus.Active} AND {(int)GuestVehicleNoticeStatus.Cancelled}");
            table.HasCheckConstraint("CK_GuestVehicleNotices_ExpectedRange", "[ExpectedTo] > [ExpectedFrom]");
        });

        builder.HasKey(notice => notice.Id);
        builder.Property(notice => notice.Id).UseIdentityColumn();
        builder.Property(notice => notice.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(notice => notice.PlateNumber).HasMaxLength(30).IsRequired();
        builder.Property(notice => notice.GuestName).HasMaxLength(150);
        builder.Property(notice => notice.Note).HasMaxLength(500);
        builder.Property(notice => notice.ExpectedFrom).IsRequired();
        builder.Property(notice => notice.ExpectedTo).IsRequired();
        builder.Property(notice => notice.CreatedAt).IsRequired();

        builder.HasIndex(notice => new { notice.PropertyId, notice.Status });
        builder.HasIndex(notice => new { notice.RoomId, notice.Status });
        builder.HasIndex(notice => notice.ExpectedTo);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(notice => notice.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(notice => notice.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(notice => notice.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VehicleAuditSessionConfiguration : IEntityTypeConfiguration<VehicleAuditSession>
{
    public void Configure(EntityTypeBuilder<VehicleAuditSession> builder)
    {
        builder.ToTable("VehicleAuditSessions", table =>
        {
            table.HasCheckConstraint("CK_VehicleAuditSessions_Status",
                $"[Status] BETWEEN {(int)VehicleAuditSessionStatus.InProgress} AND {(int)VehicleAuditSessionStatus.Completed}");
        });

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).UseIdentityColumn();
        builder.Property(session => session.AuditedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(session => session.Note).HasMaxLength(500);
        builder.Property(session => session.StartedAt).IsRequired();

        builder.HasIndex(session => new { session.PropertyId, session.StartedAt });
        builder.HasIndex(session => new { session.PropertyId, session.Status });

        // Mỗi nhà trọ chỉ có tối đa một phiên kiểm kê đang diễn ra tại một thời điểm.
        builder.HasIndex(session => session.PropertyId)
            .IsUnique()
            .HasFilter($"[Status] = {(int)VehicleAuditSessionStatus.InProgress}")
            .HasDatabaseName("IX_VehicleAuditSessions_PropertyId_InProgress");

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(session => session.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(session => session.Items)
            .WithOne()
            .HasForeignKey(item => item.AuditSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class VehicleAuditItemConfiguration : IEntityTypeConfiguration<VehicleAuditItem>
{
    public void Configure(EntityTypeBuilder<VehicleAuditItem> builder)
    {
        builder.ToTable("VehicleAuditItems", table =>
        {
            table.HasCheckConstraint("CK_VehicleAuditItems_Result",
                $"[Result] BETWEEN {(int)VehicleAuditItemResult.Pending} AND {(int)VehicleAuditItemResult.Missing}");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).UseIdentityColumn();
        builder.Property(item => item.PlateNumberSnapshot).HasMaxLength(30).IsRequired();
        builder.Property(item => item.RoomCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Note).HasMaxLength(500);

        builder.HasIndex(item => new { item.AuditSessionId, item.VehicleId })
            .IsUnique()
            .HasFilter("[VehicleId] IS NOT NULL");

        builder.HasIndex(item => new { item.AuditSessionId, item.GuestNoticeId })
            .IsUnique()
            .HasFilter("[GuestNoticeId] IS NOT NULL");

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(item => item.VehicleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<GuestVehicleNotice>()
            .WithMany()
            .HasForeignKey(item => item.GuestNoticeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
