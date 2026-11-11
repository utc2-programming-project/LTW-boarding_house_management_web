namespace Web.Features.Accounts.Otp;

public interface IOtpService
{
    Task<OtpIssueResult> IssueAsync(
        string? userId, string email, OtpPurpose purpose, CancellationToken ct = default);

    Task<OtpVerifyResult> VerifyAsync(
        string? userId, string email, OtpPurpose purpose, string code, CancellationToken ct = default);

    Task InvalidateAsync(string email, OtpPurpose purpose, CancellationToken ct = default);
}
