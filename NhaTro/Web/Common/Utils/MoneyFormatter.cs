using System.Globalization;

namespace Web.Common.Utils;

/// <summary>Định dạng VND cố định, không phụ thuộc ngôn ngữ giao diện.</summary>
public static class MoneyFormatter
{
    public const string Symbol = "₫";

    // Tự dựng NumberFormatInfo để kết quả không đổi theo dữ liệu ICU của máy chủ.
    private static readonly NumberFormatInfo Vnd = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3],
        NegativeSign = "-",
    };

    public static decimal RoundVnd(decimal amount) =>
        Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    public static string Format(decimal amount) =>
        $"{RoundVnd(amount).ToString("N0", Vnd)} {Symbol}";

    public static string Format(decimal? amount, string empty = "—") =>
        amount is null ? empty : Format(amount.Value);

    /// <summary>Đọc "1.500.000", "1500000", "1.500.000 ₫", "1.500.000 VND". Không nhận phần thập phân.</summary>
    public static bool TryParse(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim()
            .Replace(Symbol, "", StringComparison.Ordinal)
            .Replace("VND", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".", "")
            .Replace(" ", "");

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
    }
}