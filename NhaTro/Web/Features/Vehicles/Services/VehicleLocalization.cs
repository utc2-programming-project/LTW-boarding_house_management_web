using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace Web.Features.Vehicles.Services;

/// <summary>
/// Tra cứu chuỗi đã bản địa hóa cho các dịch vụ (thông báo, hóa đơn) có thể được dựng
/// ngoài DI (unit test) nên localizer là tùy chọn; khi thiếu sẽ dùng chuỗi fallback.
/// </summary>
internal static class VehicleLocalization
{
    private static readonly ResourceManager Resources = new(
        "Web.Resources.Features.Vehicles.VehicleResource",
        typeof(VehicleResource).Assembly);

    public static string T(IStringLocalizer<VehicleResource>? localizer, string key, string fallback, params object[] args)
    {
        if (localizer is null)
        {
            return args.Length == 0 ? fallback : string.Format(fallback, args);
        }

        return localizer[key, args];
    }

    /// <summary>
    /// Tra cứu theo <see cref="CultureInfo.CurrentUICulture"/> cho các lớp tĩnh không có DI localizer
    /// (ví dụ quy tắc vòng đời hợp đồng); thiếu bản dịch thì dùng fallback.
    /// </summary>
    public static string TCurrent(string key, string fallback, params object[] args)
    {
        var value = Resources.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        return args.Length == 0 ? value : string.Format(value, args);
    }
}