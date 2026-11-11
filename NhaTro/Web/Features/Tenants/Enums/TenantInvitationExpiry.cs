namespace Web.Features.Tenants.Enums;

/// <summary>Preset hạn QR; email luôn dùng 15 phút.</summary>
public enum TenantInvitationExpiry
{
    Minutes15 = 15,
    Hour1 = 60,
    Hours6 = 360,
    Hours24 = 1440,
}
