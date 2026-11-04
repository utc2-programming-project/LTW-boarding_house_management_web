using System.Globalization;

namespace Web.Common.Utils;

/// <summary>
/// Số hiển thị theo quy ước VN cố định (dấu phẩy thập phân), không đổi theo culture của yêu cầu
/// hay dữ liệu ICU của máy chủ — cùng nguyên tắc với MoneyFormatter.
/// </summary>
public static class NumberFormatter
{
    public static readonly NumberFormatInfo Display = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3],
        NegativeSign = "-",
    };

    /// <summary>Một chữ số thập phân, ví dụ 1,5.</summary>
    public static string Megabytes(double value) => value.ToString("F1", Display);

    /// <summary>Số gọn không chữ số 0 thừa, ví dụ 30 → "30", 30,5 → "30,5".</summary>
    public static string Trimmed(decimal value) => value.ToString("0.##", Display);

    /// <summary>Số gọn không chữ số 0 thừa, ví dụ 62,5.</summary>
    public static string Trimmed(double value) => value.ToString("0.#", Display);

    public static string FileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0).ToString("F1", Display)} KB";
        return $"{(bytes / (1024.0 * 1024.0)).ToString("F2", Display)} MB";
    }
}
