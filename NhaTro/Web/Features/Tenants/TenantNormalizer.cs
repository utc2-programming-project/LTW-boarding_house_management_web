namespace Web.Features.Tenants;

/// <summary>
/// Chuẩn hóa dữ liệu hồ sơ khách thuê (mục 2, mục 5).
/// Hàm thuần, test được không cần DB.
/// </summary>
public static class TenantNormalizer
{
    /// <summary>Cắt khoảng trắng đầu/cuối và gộp khoảng trắng thừa trong họ tên.</summary>
    public static string NormalizeFullName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Bỏ khoảng trắng, dấu chấm, gạch; đổi tiền tố +84/0084/84 thành 0. Chỉ giữ chữ số.
    /// </summary>
    public static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .Replace("-", string.Empty);

        if (trimmed.StartsWith("+84", StringComparison.Ordinal))
        {
            trimmed = "0" + trimmed[3..];
        }
        else if (trimmed.StartsWith("0084", StringComparison.Ordinal))
        {
            trimmed = "0" + trimmed[4..];
        }
        else if (trimmed.StartsWith("84", StringComparison.Ordinal) && trimmed.Length >= 11)
        {
            // 84xxxxxxxxx (khong co +): 84 khong phai dau so noi dia hop le cua VN.
            trimmed = "0" + trimmed[2..];
        }

        return new string(trimmed.Where(char.IsAsciiDigit).ToArray());
    }

    /// <summary>Giữ chữ số và chữ cái ASCII (hộ chiếu có chữ), in hoa để so trùng ổn định.</summary>
    public static string NormalizeIdNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var kept = new string(value.Where(char.IsAsciiLetterOrDigit).ToArray());
        return kept.ToUpperInvariant();
    }

    /// <summary>Số điện thoại VN sau chuẩn hóa: 10-11 chữ số, bắt đầu bằng 0.</summary>
    public static bool IsValidVietnamesePhone(string normalizedPhone) =>
        normalizedPhone.Length is >= 10 and <= 11 && normalizedPhone[0] == '0';

    /// <summary>Địa chỉ/ghi chú: cắt khoảng trắng đầu/cuối; rỗng thành null.</summary>
    public static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>Che số giấy tờ, chỉ giữ 4 ký tự cuối (ví dụ ********4821).</summary>
    public static string? MaskIdNumber(string? idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber))
        {
            return null;
        }

        var trimmed = idNumber.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', 8);
        }

        return new string('*', 8) + trimmed[^4..];
    }
}
