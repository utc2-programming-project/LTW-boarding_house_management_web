using System.Security.Cryptography;
using System.Text;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.Rules;

/// <summary>
/// Quy tắc thuần nghiệp vụ cho Quản lý xe: chuẩn hóa biển số, sinh mã QR nội bộ,
/// kiểm tra loại xe hợp lệ theo khu đỗ và tính phí gửi xe hàng tháng.
/// </summary>
public static class VehicleRules
{
    public const decimal DefaultElectricMotorbikeMonthlyFee = 50_000m;
    public const decimal DefaultManualMotorbikeMonthlyFee = 80_000m;
    public const decimal DefaultScooterMonthlyFee = 100_000m;
    public const int DefaultMaxVehiclesPerRoom = 2;
    public const int DefaultZoneNearFullThresholdPercent = 85;
    public const int MaxActiveGuestNoticesPerRoom = 3;
    public static readonly TimeSpan BlockingAlertCooldown = TimeSpan.FromMinutes(5);
    public const string ParkingInvoiceLinePrefix = "Phí gửi xe";
    public const string ParkingInvoiceLinePrefixEn = "Parking fee";

    /// <summary>
    /// Chuẩn hóa biển số: bỏ khoảng trắng, dấu gạch ngang, dấu chấm, dấu gạch chéo, chuyển sang chữ in hoa.
    /// Ví dụ: "59-X1 123.45" → "59X112345".
    /// </summary>
    public static string NormalizePlateNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is 'đ' or 'Đ')
            {
                sb.Append('D');
            }
            else if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(char.ToUpperInvariant(ch));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Biển số xe 2 bánh hợp lệ sau khi chuẩn hóa phải từ 5 đến 15 ký tự chữ/số và có ít nhất 1 chữ số.
    /// </summary>
    public static bool IsValidPlateNumber(string? normalizedPlate)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate))
        {
            return false;
        }

        if (normalizedPlate.Length is < 5 or > 15)
        {
            return false;
        }

        var hasDigit = false;
        foreach (var ch in normalizedPlate)
        {
            if (!char.IsAsciiLetterUpper(ch) && !char.IsAsciiDigit(ch))
            {
                return false;
            }

            if (char.IsAsciiDigit(ch))
            {
                hasDigit = true;
            }
        }

        return hasDigit;
    }

    /// <summary>
    /// Sinh mã QR định danh nội bộ không chứa PII (ví dụ: VH-9A4F2C1E8B7D0356).
    /// </summary>
    public static string GenerateQrCodeToken()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return "VH-" + Convert.ToHexString(bytes);
    }

    public static AllowedVehicleTypeFlags ToFlag(VehicleType type) => type switch
    {
        VehicleType.ElectricMotorbike => AllowedVehicleTypeFlags.ElectricMotorbike,
        VehicleType.ManualMotorbike => AllowedVehicleTypeFlags.ManualMotorbike,
        VehicleType.Scooter => AllowedVehicleTypeFlags.Scooter,
        _ => AllowedVehicleTypeFlags.None,
    };

    public static bool IsVehicleTypeAllowed(VehicleType type, AllowedVehicleTypeFlags allowedFlags)
    {
        var flag = ToFlag(type);
        return flag != AllowedVehicleTypeFlags.None && (allowedFlags & flag) == flag;
    }

    /// <summary>
    /// D-VH-03 &amp; D-VH-12: Tính phí gửi xe theo tháng cho 1 xe.
    /// Mặc định tính theo loại xe; khi bật EnableParkingZones và xe gán vào khu có IsFree hoặc OverrideMonthlyFee thì ưu tiên giá khu.
    /// </summary>
    public static decimal CalculateMonthlyFee(
        VehicleType type,
        VehiclePropertySetting setting,
        ParkingZone? zone = null)
    {
        ArgumentNullException.ThrowIfNull(setting);

        if (setting.EnableParkingZones && zone is not null && zone.IsActive)
        {
            if (zone.IsFree)
            {
                return 0m;
            }

            if (zone.OverrideMonthlyFee is { } overrideFee && overrideFee >= 0m)
            {
                return overrideFee;
            }
        }

        return type switch
        {
            VehicleType.ElectricMotorbike => setting.ElectricMotorbikeMonthlyFee,
            VehicleType.ManualMotorbike => setting.ManualMotorbikeMonthlyFee,
            VehicleType.Scooter => setting.ScooterMonthlyFee,
            _ => 0m,
        };
    }

    /// <summary>
    /// Tạo mô tả dòng hóa đơn "Phí gửi xe" ghi rõ số lượng từng loại xe đang Active.
    /// Ví dụ: "Phí gửi xe (1 xe máy điện, 1 xe máy số, 1 xe tay ga)".
    /// </summary>
    public static string BuildInvoiceLineDescription(IEnumerable<VehicleType> vehicleTypes) =>
        BuildInvoiceLineDescription(vehicleTypes.Select(t => (t, 0m)));

    /// <summary>
    /// Tạo mô tả dòng hóa đơn "Phí gửi xe" kèm đơn giá của từng nhóm (loại xe + mức phí).
    /// Ví dụ: "Phí gửi xe (1 xe máy số (80.000đ), 1 xe tay ga (100.000đ))".
    /// <paramref name="prefix"/> cho phép truyền nhãn đã bản địa hóa; mặc định dùng tiếng Việt.
    /// </summary>
    public static string BuildInvoiceLineDescription(
        IEnumerable<(VehicleType Type, decimal Fee)> vehicles,
        Func<VehicleType, string>? typeLabel = null,
        string? prefix = null)
    {
        var linePrefix = string.IsNullOrWhiteSpace(prefix) ? ParkingInvoiceLinePrefix : prefix;
        var list = vehicles.ToList();
        if (list.Count == 0)
        {
            return linePrefix;
        }

        var parts = list
            .GroupBy(v => (v.Type, v.Fee))
            .OrderBy(g => TypeOrder(g.Key.Type))
            .ThenBy(g => g.Key.Fee)
            .Select(g =>
            {
                var label = $"{g.Count()} {typeLabel?.Invoke(g.Key.Type) ?? TypeLabel(g.Key.Type)}";
                return g.Key.Fee > 0m
                    ? $"{label} ({FormatFee(g.Key.Fee)}đ)"
                    : label;
            })
            .ToList();

        return parts.Count == 0
            ? linePrefix
            : $"{linePrefix} ({string.Join(", ", parts)})";
    }

    private static int TypeOrder(VehicleType type) => type switch
    {
        VehicleType.ElectricMotorbike => 0,
        VehicleType.ManualMotorbike => 1,
        VehicleType.Scooter => 2,
        _ => 3,
    };

    private static string TypeLabel(VehicleType type) => type switch
    {
        VehicleType.ElectricMotorbike => "xe máy điện",
        VehicleType.ManualMotorbike => "xe máy số",
        VehicleType.Scooter => "xe tay ga",
        _ => "xe",
    };

    private static string FormatFee(decimal fee) =>
        fee.ToString("#,##0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    public static bool IsParkingFeeLine(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var trimmed = description.Trim();
        // Nhận diện cả nhãn tiếng Việt lẫn tiếng Anh để dữ liệu cũ/mới đều được xử lý đồng nhất.
        return trimmed.StartsWith(ParkingInvoiceLinePrefix, StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(ParkingInvoiceLinePrefixEn, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Che biển số cho thông báo công khai nếu cần, hoặc định dạng dễ đọc.
    /// </summary>
    public static string FormatPlateForDisplay(string normalizedPlate)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate) || normalizedPlate.Length <= 4)
        {
            return normalizedPlate;
        }

        return $"{normalizedPlate[..4]}-{normalizedPlate[4..]}";
    }
}
