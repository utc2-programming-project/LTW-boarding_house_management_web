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
    string? ErrorMessage = null)
{
    public static TenantInvitationResult Success(int id, string? rawToken = null) =>
        new(true, id, rawToken);

    public static TenantInvitationResult Failure(string code, string message) =>
        new(false, ErrorCode: code, ErrorMessage: message);
}
