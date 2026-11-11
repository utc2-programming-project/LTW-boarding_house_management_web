namespace Web.Features.Tenants.Invitations;

/// <summary>PLAN-006: yêu cầu mời một khách thuê.</summary>
public sealed class CreateTenantInvitationRequest
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
}

/// <summary>Một lời mời hiển thị trong danh sách của chủ trọ/quản lý.</summary>
public sealed record TenantInvitationDto(
    int Id,
    string FullName,
    string Email,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    bool IsAccepted,
    bool IsRevoked,
    bool IsExpired);

public static class TenantInvitationErrorCodes
{
    public const string InvalidRequest = "InvitationInvalidRequest";
    public const string Forbidden = "InvitationForbidden";
    public const string DuplicateEmail = "InvitationDuplicateEmail";
    public const string NotFound = "InvitationNotFound";
    public const string Expired = "InvitationExpired";
    public const string AlreadyUsed = "InvitationAlreadyUsed";
    public const string EmailAlreadyRegistered = "InvitationEmailAlreadyRegistered";
}

/// <summary>Kết quả tạo lời mời: kèm token thô để nơi gọi dựng liên kết (không lưu DB).</summary>
public sealed record TenantInvitationResult(
    bool Succeeded,
    int InvitationId = 0,
    string? RawToken = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    DateTime? ExpiresAt = null,
    string? UserId = null,
    string? Email = null)
{
    public static TenantInvitationResult Success(int id, string? rawToken = null) =>
        new(true, id, rawToken);

    public static TenantInvitationResult SuccessAccount(
        int id, string userId, string email, string rawToken = "") =>
        new(true, id, rawToken, UserId: userId, Email: email);

    public static TenantInvitationResult Failure(string code, string message) =>
        new(false, ErrorCode: code, ErrorMessage: message);
}

/// <summary>PLAN-024: yêu cầu tạo QR.</summary>
public sealed class CreateTenantQrRequest
{
    public Enums.TenantInvitationKind Kind { get; set; } = Enums.TenantInvitationKind.NewAccount;

    public int? PropertyId { get; set; }

    public int? RoomId { get; set; }

    public Enums.TenantInvitationExpiry Expiry { get; set; } = Enums.TenantInvitationExpiry.Hours24;

    public string? TargetUserId { get; set; }
}

/// <summary>PLAN-024: khách mới tự điền hồ sơ khi quét QR.</summary>
public sealed class NewTenantAccountRequest
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Password { get; set; } = string.Empty;
}

/// <summary>PLAN-024: thông tin hiển thị trước khi khách nhận lời mời.</summary>
public sealed record TenantInvitationDetails(
    int Id,
    Enums.TenantInvitationChannel Channel,
    Enums.TenantInvitationKind Kind,
    string OwnerId,
    string OwnerName,
    string? PropertyName,
    string? RoomCode,
    string Email,
    string FullName,
    string? Phone,
    string? TargetUserId,
    DateTime ExpiresAt,
    bool IsAccepted,
    bool IsRevoked,
    bool IsExpired);

/// <summary>PLAN-024: khách đã có tài khoản để chủ trọ/quản lý chọn mời.</summary>
public sealed record ExistingTenantOptionDto(
    string UserId,
    int TenantId,
    string FullName,
    string? Email,
    string? Phone);
