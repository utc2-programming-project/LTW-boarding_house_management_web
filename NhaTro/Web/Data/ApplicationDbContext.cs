using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Web.Features.Properties.Entities;
using Web.Features.Approvals.Entities;
using Web.Features.Audit.Entities;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.Entities;
using Web.Features.Maintenance.Entities;
using Web.Features.Notifications;
using Web.Features.Rooms.Entities;
using Web.Features.Settings.Entities;
using Web.Features.Storage.Entities;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.Entities;
using Web.Features.Accounts.Otp;
using Web.Data.Tariffs;

namespace Web.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Property> Properties => Set<Property>();

        // VHT-24: phân công Quản lý theo tòa nhà (nhiều-nhiều, có lịch sử).
        public DbSet<PropertyManager> PropertyManagers => Set<PropertyManager>();

        // PLAN-022: mã OTP một lần cho đăng ký/xác thực tài khoản.
        public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<Tenant> Tenants => Set<Tenant>();

        // PLAN-006: lời mời khách thuê qua email.
        public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

        // PLAN-005: hồ sơ khách ↔ chủ trọ (nhiều-nhiều, một khách có thể thuộc nhiều chủ trọ).
        public DbSet<TenantOwnerLink> TenantOwnerLinks => Set<TenantOwnerLink>();
        // PLAN-024: khách nhận QR/lời mời cho phòng, chờ lập hợp đồng.
        public DbSet<PendingRoomAssignment> PendingRoomAssignments => Set<PendingRoomAssignment>();
        public DbSet<Contract> Contracts => Set<Contract>();
        public DbSet<ContractMember> ContractMembers => Set<ContractMember>();
        // Luồng ký hợp đồng: phiên bản nội dung, bằng chứng khách xác nhận, yêu cầu chỉnh sửa.
        public DbSet<ContractVersion> ContractVersions => Set<ContractVersion>();
        public DbSet<ContractAcceptance> ContractAcceptances => Set<ContractAcceptance>();
        public DbSet<ContractChangeRequest> ContractChangeRequests => Set<ContractChangeRequest>();
        public DbSet<ContractDepositPayment> ContractDepositPayments => Set<ContractDepositPayment>();
        // VHT-125 (2D): yeu cau gia han hop dong do khach thue gui tu "Phong dang thue".
        public DbSet<ContractRenewalRequest> ContractRenewalRequests => Set<ContractRenewalRequest>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
        // Lịch sử Chủ trọ chỉnh sửa hoá đơn đã phát hành (chưa thanh toán): khách thuê thấy lý do.
        public DbSet<InvoiceRevision> InvoiceRevisions => Set<InvoiceRevision>();
        // VHT-54: yêu cầu thanh toán, minh chứng và audit nghiệp vụ P6.
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<PaymentProof> PaymentProofs => Set<PaymentProof>();
        public DbSet<PaymentAuditLog> PaymentAuditLogs => Set<PaymentAuditLog>();
        public DbSet<MaintenanceIncident> MaintenanceIncidents => Set<MaintenanceIncident>();
        public DbSet<MaintenanceIncidentHistory> MaintenanceIncidentHistories => Set<MaintenanceIncidentHistory>();
        public DbSet<IncidentPhotoAttachment> IncidentPhotoAttachments => Set<IncidentPhotoAttachment>();
        public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
        public DbSet<FileAccessAuditLog> FileAccessAuditLogs => Set<FileAccessAuditLog>();

        // VHT-94: 21 cong tac Quan ly (SW-01..SW-16 + SW-15a..e).
        public DbSet<ManagerToggle> ManagerToggles => Set<ManagerToggle>();

        // VHT-96: hang doi duyet thao tac Quan ly (NEW-03).
        public DbSet<ManagerApprovalRequest> ManagerApprovalRequests => Set<ManagerApprovalRequest>();
        public DbSet<UtilityTariff> UtilityTariffs => Set<UtilityTariff>();
        public DbSet<UtilityTariffTier> UtilityTariffTiers => Set<UtilityTariffTier>();
        public DbSet<UtilityBillingPeriodSnapshot> UtilityBillingPeriodSnapshots => Set<UtilityBillingPeriodSnapshot>();
        public DbSet<UtilityBillingCostLineSnapshot> UtilityBillingCostLineSnapshots => Set<UtilityBillingCostLineSnapshot>();

        // VHT-51: kỳ ghi chỉ số thật, giữ chỉ số đầu/cuối và tham chiếu hóa đơn đã phát hành (D10).
        public DbSet<UtilityReadingPeriod> UtilityReadingPeriods => Set<UtilityReadingPeriod>();
        public DbSet<UtilityServicePeriodSnapshot> UtilityServicePeriodSnapshots => Set<UtilityServicePeriodSnapshot>();

        public DbSet<ServiceCatalog> ServiceCatalogs => Set<ServiceCatalog>();
        public DbSet<ServicePriceHistory> ServicePriceHistories => Set<ServicePriceHistory>();

        // Khách thuê đăng ký dịch vụ hằng tháng theo hợp đồng (phòng).
        public DbSet<ContractServiceRegistration> ContractServiceRegistrations => Set<ContractServiceRegistration>();

        // Yêu cầu đăng ký dịch vụ có xét duyệt (PENDING → APPROVED/REJECTED).
        public DbSet<ServiceRegistrationRequest> ServiceRegistrationRequests => Set<ServiceRegistrationRequest>();

        // Thông báo giữa các vai trò (đăng ký, hóa đơn, thanh toán).
        public DbSet<AppNotification> AppNotifications => Set<AppNotification>();

        // D16: nhật ký thao tác tách sổ hệ thống (Admin) và sổ nghiệp vụ theo tòa nhà (Chủ trọ).
        public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

        // P5: chỉ số điện/nước bền vững + lịch sử sửa (D10).
        public DbSet<UtilityReading> UtilityReadings => Set<UtilityReading>();
        public DbSet<UtilityReadingAudit> UtilityReadingAudits => Set<UtilityReadingAudit>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            builder.Entity<AuditLogEntry>(entity =>
            {
                entity.Property(item => item.Action).HasMaxLength(200);
                entity.Property(item => item.ActorUserId).HasMaxLength(450);
                entity.Property(item => item.Metadata).HasMaxLength(1000);
                entity.HasIndex(item => new { item.Category, item.OccurredAt });
                entity.HasIndex(item => item.PropertyId);
            });

            builder.Entity<UtilityReading>(entity =>
            {
                entity.Property(item => item.RowVersion).IsRowVersion();
                entity.Property(item => item.ElectricStart).HasPrecision(18, 2);
                entity.Property(item => item.ElectricEnd).HasPrecision(18, 2);
                entity.Property(item => item.WaterStart).HasPrecision(18, 2);
                entity.Property(item => item.WaterEnd).HasPrecision(18, 2);
                entity.HasIndex(item => new { item.RoomId, item.PeriodStart }).IsUnique();
                entity.HasOne<Web.Features.Rooms.Entities.Room>()
                    .WithMany()
                    .HasForeignKey(item => item.RoomId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(item => item.Audits)
                    .WithOne()
                    .HasForeignKey(audit => audit.UtilityReadingId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<UtilityReadingAudit>(entity =>
            {
                entity.ToTable("UtilityReadingAudits", table => table.HasTrigger("TR_UtilityReadingAudits_Immutable"));
                entity.Property(item => item.Reason).HasMaxLength(500);
                entity.Property(item => item.ChangedByUserId).HasMaxLength(450);
                entity.Property(item => item.OldElectricEnd).HasPrecision(18, 2);
                entity.Property(item => item.NewElectricEnd).HasPrecision(18, 2);
                entity.Property(item => item.OldWaterEnd).HasPrecision(18, 2);
                entity.Property(item => item.NewWaterEnd).HasPrecision(18, 2);
                entity.HasIndex(item => item.UtilityReadingId);
            });

            builder.Entity<ContractServiceRegistration>(entity =>
            {
                entity.Property(item => item.RegisteredByUserId).HasMaxLength(450);
                entity.Property(item => item.DeletedBy).HasMaxLength(450);
                entity.Property(item => item.MonthStart).HasColumnType("date");
                entity.HasQueryFilter(item => !item.IsDeleted);
                entity.HasIndex(item => new { item.ContractId, item.ServiceCatalogId, item.MonthStart })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");
                entity.HasIndex(item => item.ContractId);
            });

            builder.Entity<ServiceRegistrationRequest>(entity =>
            {
                entity.Property(item => item.RowVersion).IsRowVersion();
                entity.Property(item => item.Quantity).HasPrecision(18, 2);
                entity.Property(item => item.ServiceName).HasMaxLength(250);
                entity.Property(item => item.Note).HasMaxLength(1000);
                entity.Property(item => item.RequestedByUserId).HasMaxLength(450);
                entity.Property(item => item.ReviewedByUserId).HasMaxLength(450);
                entity.Property(item => item.RejectReason).HasMaxLength(1000);
                entity.Property(item => item.CancelReason).HasMaxLength(1000);
                entity.Property(item => item.CancelReviewedByUserId).HasMaxLength(450);
                entity.Property(item => item.CancelRejectReason).HasMaxLength(1000);
                entity.HasIndex(item => item.ContractId);
                entity.HasIndex(item => item.Status);
                entity.HasIndex(item => new { item.ContractId, item.ServiceCatalogId, item.StartDate });
            });
        }
    }
}
