using System.Globalization;
using System.Text;

namespace Web.Features.Utilities.Rules;

public enum ServiceUnitKind { Metered, PerPerson, Fixed }

/// <summary>Chuẩn hóa đơn vị danh mục trước khi tính chi phí kỳ.</summary>
public static class ServiceUnitNormalizer
{
    public static ServiceUnitKind Classify(string? unit)
    {
        var value = Normalize(unit);
        if (value.StartsWith("kwh", StringComparison.Ordinal) || value.StartsWith("m3", StringComparison.Ordinal)) return ServiceUnitKind.Metered;
        if (value.StartsWith("nguoi", StringComparison.Ordinal) || value.StartsWith("person", StringComparison.Ordinal)) return ServiceUnitKind.PerPerson;
        return ServiceUnitKind.Fixed;
    }

    private static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character == '³' ? '3' : character);
            }
        }

        return string.Concat(builder.ToString().Normalize(NormalizationForm.FormC).Where(char.IsLetterOrDigit));
    }
}
