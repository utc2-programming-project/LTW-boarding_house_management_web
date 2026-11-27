namespace Web.Features.Accounts.Otp;

public enum OtpPurpose
{
    OwnerRegistration = 1,

    // Giữ giá trị cũ để đọc OtpChallenge lịch sử; không phát mới (xem VerifyOtp.ParsePurpose).
    ManagerFirstLogin = 2,
    TenantFirstLogin = 3,

    TenantRegistration = 4,
    FirstLogin = 5,
    PasswordReset = 6,
    ContractAcceptance = 7,
}
