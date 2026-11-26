namespace Web.Features.Tenants.Enums;

/// <summary>Preset hạn lời mời; email dùng 24 giờ mặc định, QR chọn 15m/1h/6h/24h.</summary>
public enum TenantInvitationExpiry
{
    Minutes15 = 15,
    Hour1 = 60,
    Hours6 = 360,
    Hours24 = 1440,
}
