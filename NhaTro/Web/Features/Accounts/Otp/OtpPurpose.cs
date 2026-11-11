namespace Web.Features.Accounts.Otp;

public enum OtpPurpose
{
    OwnerRegistration = 1,
    ManagerFirstLogin = 2,
    TenantFirstLogin = 3,
    TenantRegistration = 4,
    FirstLogin = 5,
    PasswordReset = 6,
}
