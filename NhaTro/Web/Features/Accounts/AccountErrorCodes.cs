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
    public const string CannotLockLastAdmin = nameof(CannotLockLastAdmin);
    public const string RoleNotAllowed = nameof(RoleNotAllowed);
    public const string OperationFailed = nameof(OperationFailed);
    public const string DuplicateEmail = nameof(DuplicateEmail);
    public const string WeakPassword = nameof(WeakPassword);
    public const string InvalidEmail = nameof(InvalidEmail);
    public const string InvalidPhone = nameof(InvalidPhone);

    /// <summary>PLAN-028: thong tin tai khoan nhan tien khong hop le (ngan hang/so TK/ten TK).</summary>
    public const string InvalidBankAccount = nameof(InvalidBankAccount);

    // VHT-95: vong doi tai khoan.
    /// <summary>Tai khoan nam ngoai pham vi du lieu nguoi goi duoc thao tac.</summary>
    public const string OutOfScope = nameof(OutOfScope);
    /// <summary>Tao tai khoan KhachThue bat buoc chon ho so khach.</summary>
    public const string TenantRequired = nameof(TenantRequired);
    /// <summary>Ho so khach da co tai khoan khac.</summary>
    public const string TenantAlreadyLinked = nameof(TenantAlreadyLinked);
    /// <summary>Tai khoan dang gan ho so khach nen khong doi duoc vai tro.</summary>
    public const string RoleLinkedToTenant = nameof(RoleLinkedToTenant);
    /// <summary>Chi duyet duoc tai khoan dang Cho duyet.</summary>
    public const string NotPendingApproval = nameof(NotPendingApproval);

    /// <summary>Không cho tự xoá tài khoản đang đăng nhập.</summary>
    public const string CannotDeleteSelf = nameof(CannotDeleteSelf);

    /// <summary>Không cho xoá Admin cuối cùng của hệ thống.</summary>
    public const string CannotDeleteLastAdmin = nameof(CannotDeleteLastAdmin);

    /// <summary>Chot 13/11/2026: Admin khong duoc xoa tai khoan Admin khac.</summary>
    public const string CannotDeleteAdmin = nameof(CannotDeleteAdmin);

    /// <summary>Tài khoản còn dữ liệu nghiệp vụ tham chiếu (hồ sơ khách) nên không xoá được.</summary>
    public const string AccountInUse = nameof(AccountInUse);

    /// <summary>Key .resx de UI hien thi thong bao da dich cho ma loi.</summary>
    public static string ResourceKey(string? code) => code switch
    {
        CannotChangeOwnRole => "ErrorCannotChangeOwnRole",
        CannotRemoveLastAdmin => "ErrorCannotRemoveLastAdmin",
        CannotLockSelf => "ErrorCannotLockSelf",
        CannotLockAdmin => "ErrorCannotLockAdmin",
        CannotLockLastAdmin => "ErrorCannotLockLastAdmin",
        RoleNotAllowed => "ErrorRoleNotAllowed",
        InvalidRole => "ErrorInvalidRole",
        Forbidden => "ErrorForbidden",
        Unauthorized => "ErrorForbidden",
        UserNotFound => "ErrorUserNotFound",
        InvalidPhone => "ErrorInvalidPhone",
        InvalidBankAccount => "ErrorInvalidBankAccount",
        DuplicateEmail => "ErrorDuplicateEmail",
        WeakPassword => "ErrorWeakPassword",
        InvalidEmail => "ErrorInvalidEmail",
        OutOfScope => "ErrorOutOfScope",
        TenantRequired => "ErrorTenantRequired",
        TenantAlreadyLinked => "ErrorTenantAlreadyLinked",
        RoleLinkedToTenant => "ErrorRoleLinkedToTenant",
        NotPendingApproval => "ErrorNotPendingApproval",
        CannotDeleteSelf => "ErrorCannotDeleteSelf",
        CannotDeleteLastAdmin => "ErrorCannotDeleteLastAdmin",
        CannotDeleteAdmin => "ErrorCannotDeleteAdmin",
        AccountInUse => "ErrorAccountInUse",
        _ => "ErrorGeneric",
    };
}
