namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Thông tin liên hệ của Chủ trọ và (các) Quản lý đang phụ trách tòa nhà khách thuê ở.
/// Chỉ trả tên + số điện thoại để khách gọi trực tiếp; không lộ email/CCCD.
/// </summary>
public sealed class PropertyContactDto
{
    public PropertyContactPersonDto? Owner { get; init; }

    public IReadOnlyList<PropertyContactPersonDto> Managers { get; init; } = [];
}

public sealed class PropertyContactPersonDto
{
    public required string Name { get; init; }

    /// <summary>Null khi tài khoản chưa cập nhật số điện thoại.</summary>
    public string? PhoneNumber { get; init; }
}
