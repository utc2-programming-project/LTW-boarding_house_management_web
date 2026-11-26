namespace Web.Features.Accounts.Sessions;

/// <summary>Một phiên đăng nhập (một cookie xác thực trên một trình duyệt/thiết bị).</summary>
public sealed record UserSessionInfo(
    string SessionId,
    string UserId,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt)
{
    public bool IsCurrent(string? sessionId) =>
        string.Equals(SessionId, sessionId, StringComparison.Ordinal);
}

/// <summary>Tên cookie giữ mã phiên ngoài cookie xác thực của Identity.</summary>
public static class UserSessionCookies
{
    public const string SessionId = "nhatro.sid";
}
