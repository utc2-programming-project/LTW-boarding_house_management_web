namespace Web.Features.Accounts.Otp;

public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public int Length { get; set; } = 6;

    public int LifetimeMinutes { get; set; } = 10;

    public int MaxAttempts { get; set; } = 5;

    public int LockMinutes { get; set; } = 15;

    public int ResendCooldownSeconds { get; set; } = 60;

    public int MaxSendsPerHour { get; set; } = 5;

    public string HmacKey { get; set; } = string.Empty;
}
