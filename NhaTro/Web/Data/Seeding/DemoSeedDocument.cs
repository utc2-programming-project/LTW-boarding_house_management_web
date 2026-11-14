using Web.Data.Tariffs;
using Web.Features.Accounts.Otp;
using Web.Features.Approvals.Entities;
using Web.Features.Audit.Entities;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.Entities;
using Web.Features.Maintenance.Entities;
using Web.Features.Notifications;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Storage.Entities;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.Entities;

namespace Web.Data.Seeding;

/// <summary>
/// Hình dạng của <c>demo-data.json</c>. File này là nguồn dữ liệu demo duy nhất:
/// xoá nó thì <c>--seed-demo</c> không nạp gì. Các mảng dùng chính entity của app
/// để JSON khớp bảng; chỉ tài khoản và tệp có thêm phần riêng (vai trò, nội dung base64).
/// </summary>
public sealed class DemoSeedDocument
{
    public int SchemaVersion { get; set; }

    public string SeedName { get; set; } = string.Empty;

    /// <summary>Mốc thời gian gốc của bộ dữ liệu (giờ Việt Nam, không kèm offset).</summary>
    public DateTime SeedNow { get; set; }

    /// <summary>
    /// Nội dung nhị phân dùng chung theo khoá (base64). Mỗi tệp chỉ trỏ <see cref="DemoFile.TemplateKey"/>,
    /// nhờ vậy 18.000 tệp demo không lặp lại cùng một ảnh minh chứng.
    /// </summary>
    public Dictionary<string, string> FileTemplates { get; set; } = new(StringComparer.Ordinal);

    public List<DemoAccount> Accounts { get; set; } = [];
    public List<DemoFile> Files { get; set; } = [];
    public List<Tenant> Tenants { get; set; } = [];
    public List<TenantOwnerLink> TenantOwnerLinks { get; set; } = [];
    public List<Property> Properties { get; set; } = [];
    public List<Room> Rooms { get; set; } = [];
    public List<PropertyManager> PropertyManagers { get; set; } = [];
    public List<TenantInvitation> Invitations { get; set; } = [];
    public List<PendingRoomAssignment> PendingRoomAssignments { get; set; } = [];
    public List<Contract> Contracts { get; set; } = [];
    public List<ContractMember> ContractMembers { get; set; } = [];
    public List<ContractRenewalRequest> RenewalRequests { get; set; } = [];
    public List<ContractVersion> ContractVersions { get; set; } = [];
    public List<ContractAcceptance> ContractAcceptances { get; set; } = [];
    public List<ContractChangeRequest> ContractChangeRequests { get; set; } = [];
    public List<ServiceCatalog> ServiceCatalogs { get; set; } = [];
    public List<ServicePriceHistory> ServicePriceHistories { get; set; } = [];
    public List<ContractServiceRegistration> ServiceRegistrations { get; set; } = [];
    public List<ServiceRegistrationRequest> ServiceRegistrationRequests { get; set; } = [];
    public List<UtilityReading> UtilityReadings { get; set; } = [];
    public List<UtilityReadingAudit> UtilityReadingAudits { get; set; } = [];
    public List<UtilityBillingPeriodSnapshot> UtilityBillingPeriods { get; set; } = [];
    public List<UtilityBillingCostLineSnapshot> UtilityBillingCostLines { get; set; } = [];
    public List<UtilityReadingPeriod> UtilityReadingPeriods { get; set; } = [];
    public List<UtilityServicePeriodSnapshot> UtilityServiceSnapshots { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];
    public List<InvoiceItem> InvoiceItems { get; set; } = [];
    public List<InvoiceRevision> InvoiceRevisions { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<PaymentProof> PaymentProofs { get; set; } = [];
    public List<PaymentAuditLog> PaymentAuditLogs { get; set; } = [];
    public List<MaintenanceIncident> Incidents { get; set; } = [];
    public List<MaintenanceIncidentHistory> IncidentHistory { get; set; } = [];
    public List<IncidentPhotoAttachment> IncidentPhotos { get; set; } = [];
    public List<ManagerApprovalRequest> Approvals { get; set; } = [];
    public List<OtpChallenge> OtpChallenges { get; set; } = [];
    public List<AppNotification> Notifications { get; set; } = [];
    public List<AuditLogEntry> AuditLogs { get; set; } = [];
    public List<FileAccessAuditLog> FileAccessLogs { get; set; } = [];
}

/// <summary>Tài khoản demo: mật khẩu lấy từ cấu hình, không nằm trong file dữ liệu.</summary>
public sealed class DemoAccount
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public List<string> Roles { get; set; } = [];
    public string? PhoneNumber { get; set; }
    public string? BankCode { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
    public bool EmailConfirmed { get; set; } = true;
    public DateTime? ApprovedAt { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public string? OriginalEmail { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

/// <summary>Metadata tệp + khoá nội dung dùng chung; seeder ghi ra <c>storage/demo</c>.</summary>
public sealed class DemoFile
{
    public StoredFile File { get; set; } = new();

    /// <summary>Khoá trong <see cref="DemoSeedDocument.FileTemplates"/>.</summary>
    public string TemplateKey { get; set; } = string.Empty;
}
