namespace Web.Features.AccessControl;

/// <summary>Mã lỗi ổn định; UI dịch qua resource AccessControlResources.</summary>
public static class GateErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string PropertyNotFound = nameof(PropertyNotFound);
    public const string RoomNotFound = nameof(RoomNotFound);
    public const string PassNotFound = nameof(PassNotFound);
    public const string InvalidRequest = nameof(InvalidRequest);
    public const string MissingGuestName = nameof(MissingGuestName);
    public const string MissingGuestPhone = nameof(MissingGuestPhone);
    public const string MissingLawfulFields = nameof(MissingLawfulFields);
    public const string TenantIssueDisabled = nameof(TenantIssueDisabled);
    public const string TenantOvernightDisabled = nameof(TenantOvernightDisabled);
    public const string TenantQuotaExceeded = nameof(TenantQuotaExceeded);
    public const string TenantNoActiveContract = nameof(TenantNoActiveContract);
    public const string InvalidStatus = nameof(InvalidStatus);
    public const string ReasonRequired = nameof(ReasonRequired);
    public const string PassExpired = nameof(PassExpired);
    public const string PassRevoked = nameof(PassRevoked);
    public const string PassNotApproved = nameof(PassNotApproved);
    public const string PassUsedUp = nameof(PassUsedUp);
    public const string OutsideAllowedHours = nameof(OutsideAllowedHours);
    public const string MethodNotAllowed = nameof(MethodNotAllowed);
    public const string TokenInvalid = nameof(TokenInvalid);
    public const string AlreadyInside = nameof(AlreadyInside);
    public const string NotInside = nameof(NotInside);
    public const string ManualOpenDisabled = nameof(ManualOpenDisabled);

    // Link mời khách vãng lai tự điền (không tiết lộ link hỏng vì lý do gì).
    public const string InviteInvalid = nameof(InviteInvalid);
    public const string GuestFillDisabled = nameof(GuestFillDisabled);
    public const string GuestQuotaExceeded = nameof(GuestQuotaExceeded);
    public const string DuplicateGuestRequest = nameof(DuplicateGuestRequest);
    public const string SaveFailed = nameof(SaveFailed);
}
