namespace Web.Features.Accounts;

/// <summary>
/// VHT-8: ma loi tra ve cua <see cref="IAccountService"/>.
/// UI doi chieu ma nay de hien thi thong bao da dich (khong hien thi cau tieng Viet cung trong service).
/// </summary>
public static class AccountErrorCodes
{
    public const string Unauthorized = nameof(Unauthorized);
    public const string Forbidden = nameof(Forbidden);
    public const string UserNotFound = nameof(UserNotFound);
    public const string InvalidRole = nameof(InvalidRole);
    public const string CannotChangeOwnRole = nameof(CannotChangeOwnRole);
    public const string CannotRemoveLastAdmin = nameof(CannotRemoveLastAdmin);
    public const string CannotLockSelf = nameof(CannotLockSelf);
    public const string CannotLockAdmin = nameof(CannotLockAdmin);
    public const string RoleNotAllowed = nameof(RoleNotAllowed);
    public const string OperationFailed = nameof(OperationFailed);
    public const string DuplicateEmail = nameof(DuplicateEmail);
    public const string WeakPassword = nameof(WeakPassword);
    public const string InvalidEmail = nameof(InvalidEmail);
    public const string InvalidPhone = nameof(InvalidPhone);
}
