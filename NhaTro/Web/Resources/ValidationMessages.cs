using System.Globalization;
using System.Resources;

namespace Web.Resources;

/// <summary>
/// Thông điệp kiểm tra dữ liệu dùng cho DataAnnotations
/// (<see cref="System.ComponentModel.DataAnnotations.ValidationAttribute.ErrorMessageResourceType"/>).
/// Nguồn dịch: Web/Resources/Shared.vi-VN.resx và Shared.en-US.resx.
/// </summary>
/// <remarks>
/// Tách khỏi lớp marker <c>Web.Shared</c> vì DataAnnotations cần property tĩnh công khai
/// trỏ tới resource; UI vẫn dùng <c>IStringLocalizer&lt;Shared&gt;</c> như cũ.
/// </remarks>
public static class ValidationMessages
{
    private static readonly ResourceManager Resources =
        new("Web.Resources.Shared", typeof(ValidationMessages).Assembly);

    private static string Get(string name)
    {
        var culture = CultureInfo.CurrentUICulture;
        return Resources.GetString(name, culture)
            ?? Resources.GetString(name, CultureInfo.GetCultureInfo("vi-VN"))
            ?? name;
    }

    // Dùng lại key đã có của form P2 để không nhân đôi chuỗi dịch.
    public static string PropertyNameRequired => Get("P2ValidationPropertyNameRequired");
    public static string PropertyNameMaxLength => Get("P2ValidationNameLength");
    public static string PropertyAddressMaxLength => Get("P2ValidationAddressLength");

    public static string RoomPropertyRequired => Get(nameof(RoomPropertyRequired));
    public static string RoomCodeRequired => Get(nameof(RoomCodeRequired));
    public static string RoomCodeMaxLength => Get(nameof(RoomCodeMaxLength));
    public static string RoomFloorInvalid => Get(nameof(RoomFloorInvalid));
    public static string RoomAreaInvalid => Get(nameof(RoomAreaInvalid));
    public static string RoomPriceInvalid => Get(nameof(RoomPriceInvalid));
    public static string RoomStatusInvalid => Get(nameof(RoomStatusInvalid));

    public static string TenantFullNameRequired => Get(nameof(TenantFullNameRequired));
    public static string TenantFullNameMaxLength => Get(nameof(TenantFullNameMaxLength));
    public static string TenantPhoneRequired => Get(nameof(TenantPhoneRequired));
    public static string TenantPhoneMaxLength => Get(nameof(TenantPhoneMaxLength));
    public static string TenantEmailInvalid => Get(nameof(TenantEmailInvalid));
    public static string TenantEmailMaxLength => Get(nameof(TenantEmailMaxLength));
    public static string TenantIdNumberMaxLength => Get(nameof(TenantIdNumberMaxLength));
    public static string TenantGenderInvalid => Get(nameof(TenantGenderInvalid));
    public static string TenantAddressMaxLength => Get(nameof(TenantAddressMaxLength));
    public static string TenantIdIssuedPlaceMaxLength => Get(nameof(TenantIdIssuedPlaceMaxLength));
    public static string TenantNoteMaxLength => Get(nameof(TenantNoteMaxLength));
    public static string TenantRowVersionRequired => Get(nameof(TenantRowVersionRequired));
}
