using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.DTOs;

public sealed record GateRoomOption(int RoomId, int PropertyId, string PropertyName, string RoomCode);

public sealed record GatePropertyOption(int PropertyId, string Name);

public sealed record GatePassDto(
    int Id,
    int PropertyId,
    string PropertyName,
    int? RoomId,
    string? RoomCode,
    string VisitorName,
    string? VisitorPhone,
    GatePassType PassType,
    GateStayType StayType,
    GatePassStatus Status,
    DateTimeOffset ExpiresAt,
    int MaxUses,
    int UsedCount,
    GateIssuerRole IssuerRole,
    string IssuedByUserId,
    bool AutoApproved,
    string? RejectedReason,
    string? RevokedReason,
    DateTimeOffset CreatedAt)
{
    public bool CanApprove { get; init; }
    public bool CanRevoke { get; init; }
    public bool IsOvernight => StayType == GateStayType.Overnight;
}

public sealed record GatePassCreateRequest
{
    public int? PropertyId { get; init; }
    public int? RoomId { get; init; }
    public string VisitorName { get; init; } = string.Empty;
    public string? VisitorPhone { get; init; }
    public string? VisitorIdNumber { get; init; }
    public DateOnly? VisitorDob { get; init; }
    public string? StayReason { get; init; }
    public string? Note { get; init; }
    public GateStayType StayType { get; init; } = GateStayType.Visit;
    public int? TtlMinutes { get; init; }
    public int? MaxUses { get; init; }
    public bool IssueCode { get; init; } = true;
}

public sealed record GatePassCreatedDto(GatePassDto Pass, string RawToken, string? Code);

public sealed record GatePassResult(bool Succeeded, string? ErrorCode = null, GatePassCreatedDto? Created = null, GatePassDto? Pass = null)
{
    public static GatePassResult Fail(string errorCode) => new(false, errorCode);
}

public sealed record GatePolicyDto(
    int PropertyId,
    GateAuthMode AuthMode,
    bool AntiPassbackEnabled,
    bool AllowManualOpen,
    bool AllowManagerApprove,
    bool AutoApproveDayVisit,
    bool AutoApproveOvernight,
    bool AllowTenantIssuePass,
    bool TenantMayIssueOvernight,
    int TenantMaxPassesPerDay,
    int TenantMaxTtlMinutes,
    int TenantMaxUses,
    int PassTtlMinutes,
    int DayVisitStartHour,
    int DayVisitEndHour,
    int RetentionDays,
    int OfflineGraceMinutes,
    string RequiredGuestFields,
    StayReportChannel PreferredStayChannel,
    DateTimeOffset UpdatedAt)
{
    public static GatePolicyDto Defaults(int propertyId) => new(
        propertyId, GateAuthMode.SingleFactor, true, true, false, false, false, true, false,
        3, 60, 1, 60, 6, 22, 30, 15, "FullName,Phone", StayReportChannel.VNeId, DateTimeOffset.MinValue);
}

public sealed record GatePolicySaveRequest
{
    public int PropertyId { get; set; }
    public GateAuthMode AuthMode { get; set; }
    public bool AntiPassbackEnabled { get; set; }
    public bool AllowManualOpen { get; set; }
    public bool AllowManagerApprove { get; set; }
    public bool AutoApproveDayVisit { get; set; }
    public bool AutoApproveOvernight { get; set; }
    public bool AllowTenantIssuePass { get; set; }
    public bool TenantMayIssueOvernight { get; set; }
    public int TenantMaxPassesPerDay { get; set; }
    public int TenantMaxTtlMinutes { get; set; }
    public int TenantMaxUses { get; set; }
    public int PassTtlMinutes { get; set; }
    public int DayVisitStartHour { get; set; }
    public int DayVisitEndHour { get; set; }
    public int RetentionDays { get; set; }
    public int OfflineGraceMinutes { get; set; }
    public string RequiredGuestFields { get; set; } = "FullName,Phone";
    public StayReportChannel PreferredStayChannel { get; set; }
}

public sealed record GateDeviceDto(int Id, string Name, GateDeviceKind Kind, GateLevel Level, bool IsOnline, DateTimeOffset? LastSeenAt);

public sealed record GateDeviceSaveRequest(int PropertyId, string Name, GateDeviceKind Kind, GateLevel Level);

public sealed record GateVerifyRequest
{
    public int PropertyId { get; init; }
    public int? GateDeviceId { get; init; }
    public string Token { get; init; } = string.Empty;
    public GateMethod Method { get; init; } = GateMethod.Qr;
    public GateDirection Direction { get; init; } = GateDirection.In;
}

public sealed record GateVerifyResult(bool Succeeded, string? ErrorCode, string Message, int? PassId, string? SubjectName)
{
    public bool CanOpen { get; init; }
}

public sealed record GateLogDto(
    long Id,
    string PropertyName,
    string? GateName,
    GateDirection Direction,
    GateMethod Method,
    bool IsSuccess,
    string? Reason,
    string? SubjectName,
    DateTimeOffset OccurredAt);

public sealed record GateLogFilter
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public bool? Success { get; init; }
    public GateDirection? Direction { get; init; }
    public int Take { get; init; } = 100;
}

public sealed record StayReportDto(
    int Id,
    int? GatePassId,
    string PropertyName,
    string? RoomCode,
    string GuestName,
    DateOnly? GuestDob,
    string? GuestIdNumber,
    string? StayReason,
    DateTimeOffset StayFrom,
    DateTimeOffset StayTo,
    StayReportStatus Status,
    StayReportChannel? Channel,
    DateTimeOffset DueAt,
    DateTimeOffset? ReportedAt,
    string? Note);

public sealed record StayReportResult(bool Succeeded, string? ErrorCode = null);

public sealed record BiometricEnrollmentDto(
    int Id,
    string PropertyName,
    int? TenantId,
    int? GatePassId,
    BiometricMethod Method,
    EnrollmentStatus Status,
    DateTimeOffset ConsentAt,
    DateTimeOffset? RetentionUntil);

public sealed record GateOpenRequest(int PropertyId, int? GateDeviceId, string Reason, string ActorUserId);

public sealed record GateOpenResult(bool Opened, string? ErrorCode = null);
