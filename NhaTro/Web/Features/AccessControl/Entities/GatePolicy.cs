using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Entities;

/// <summary>
/// Chính sách cổng theo từng nhà trọ (một bản ghi / nhà ở v1).
/// </summary>
public sealed class GatePolicy
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    public GateAuthMode AuthMode { get; set; } = GateAuthMode.SingleFactor;

    public bool AntiPassbackEnabled { get; set; } = true;

    public bool AllowManualOpen { get; set; } = true;

    public bool AllowManagerApprove { get; set; }

    public bool AutoApproveDayVisit { get; set; }

    public bool AutoApproveOvernight { get; set; }

    public bool AllowTenantIssuePass { get; set; } = true;

    public bool TenantMayIssueOvernight { get; set; }

    public int TenantMaxPassesPerDay { get; set; } = 3;

    public int TenantMaxTtlMinutes { get; set; } = 60;

    public int TenantMaxUses { get; set; } = 1;

    public int PassTtlMinutes { get; set; } = 60;

    public int DayVisitStartHour { get; set; } = 6;

    public int DayVisitEndHour { get; set; } = 22;

    public int RetentionDays { get; set; } = 30;

    public int OfflineGraceMinutes { get; set; } = 15;

    public string RequiredGuestFields { get; set; } = "FullName,Phone";

    public StayReportChannel PreferredStayChannel { get; set; } = StayReportChannel.VNeId;

    public DateTimeOffset UpdatedAt { get; set; }

    public string? UpdatedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
