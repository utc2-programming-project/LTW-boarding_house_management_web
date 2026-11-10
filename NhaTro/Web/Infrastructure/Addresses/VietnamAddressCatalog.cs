using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Web.Infrastructure.Addresses;

/// <summary>
/// Danh mục đơn vị hành chính Việt Nam dùng cho gợi ý nhập địa chỉ.
/// Dữ liệu được tải một lần từ <c>wwwroot/data/vn-address.json</c> nên form không
/// phụ thuộc vào API bên ngoài lúc người dùng đang nhập.
/// </summary>
public sealed class VietnamAddressCatalog
{
    private const string DataFile = "data/vn-address.json";

    /// <summary>Viết tắt thường gặp của tỉnh/thành phố, khóa là tên đã chuẩn hóa.</summary>
    private static readonly Dictionary<string, string> ProvinceAliases = new(StringComparer.Ordinal)
    {
        ["hcm"] = "ho chi minh",
        ["tphcm"] = "ho chi minh",
        ["sai gon"] = "ho chi minh",
        ["hn"] = "ha noi",
        ["tphn"] = "ha noi",
        ["dn"] = "da nang",
        ["hp"] = "hai phong",
        ["ct"] = "can tho",
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<VietnamAddressCatalog> _logger;
    private readonly Lazy<IReadOnlyList<Province>> _provinces;

    public VietnamAddressCatalog(IWebHostEnvironment environment, ILogger<VietnamAddressCatalog> logger)
    {
        _environment = environment;
        _logger = logger;
        _provinces = new Lazy<IReadOnlyList<Province>>(LoadProvinces);
    }

    /// <summary>34 tỉnh/thành phố theo mô hình sau sáp nhập, kèm phường/xã trực thuộc.</summary>
    public IReadOnlyList<Province> Provinces => _provinces.Value;

    /// <summary>Tìm tỉnh/thành phố theo tên đầy đủ, tên rút gọn hoặc viết tắt, không phân biệt dấu và hoa thường.</summary>
    public Province? FindProvince(string? text)
    {
        var normalized = AddressText.NormalizeForSearch(text);
        if (normalized.Length == 0) return null;
        var normalizedShort = AddressText.RemoveAdministrativePrefix(text!);

        foreach (var province in Provinces)
        {
            if (AddressText.MatchesName(province.Name, normalized, normalizedShort))
            {
                return province;
            }
        }

        var alias = ResolveAlias(normalized) ?? ResolveAlias(normalizedShort);
        if (alias is null) return null;

        foreach (var province in Provinces)
        {
            if (AddressText.MatchesName(province.Name, alias, alias))
            {
                return province;
            }
        }

        return null;
    }

    private static string? ResolveAlias(string normalized) =>
        ProvinceAliases.TryGetValue(normalized, out var shortName) ? shortName : null;

    /// <summary>Tìm phường/xã trong một tỉnh theo tên đầy đủ hoặc tên rút gọn.</summary>
    public Ward? FindWard(Province province, string? text)
    {
        var normalized = AddressText.NormalizeForSearch(text);
        if (normalized.Length == 0) return null;
        var normalizedShort = AddressText.RemoveAdministrativePrefix(text!);

        foreach (var ward in province.Wards)
        {
            if (AddressText.MatchesName(ward.Name, normalized, normalizedShort))
            {
                return ward;
            }
        }

        return null;
    }

    private IReadOnlyList<Province> LoadProvinces()
    {
        var root = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        var path = Path.Combine(root, DataFile.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            _logger.LogWarning("Không tìm thấy dữ liệu địa chỉ tại {Path}; ô nhập địa chỉ sẽ chỉ dùng nhập tự do.", path);
            return Array.Empty<Province>();
        }

        try
        {
            using var stream = File.OpenRead(path);
            var data = JsonSerializer.Deserialize<List<Province>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return data ?? new List<Province>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không đọc được dữ liệu địa chỉ tại {Path}; ô nhập địa chỉ sẽ chỉ dùng nhập tự do.", path);
            return Array.Empty<Province>();
        }
    }
}

/// <summary>Một tỉnh/thành phố và danh sách phường/xã trực thuộc.</summary>
public sealed class Province
{
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<Ward> Wards { get; init; } = new();
}

/// <summary>Một phường/xã/đặc khu.</summary>
public sealed class Ward
{
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>Chuẩn hóa chuỗi tiếng Việt để tìm kiếm không dấu.</summary>
public static class AddressText
{
    private static readonly string[] AdministrativePrefixes =
    {
        "thanh pho ", "tinh ", "tp. ", "tp.", "tp ", "phuong ", "xa ", "p. ", "x. ", "dac khu ", "thi tran ", "quan ", "huyen "
    };

    /// <summary>Bỏ dấu, đưa về chữ thường và gộp khoảng trắng để so khớp.</summary>
    public static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => ch
            });
        }

        return string.Join(' ', builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Bỏ tiền tố loại đơn vị hành chính ("Thành phố", "Tỉnh", "Phường", "Xã"...).</summary>
    public static string RemoveAdministrativePrefix(string value)
    {
        var normalized = NormalizeForSearch(value);
        foreach (var prefix in AdministrativePrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                return normalized[prefix.Length..];
            }
        }

        return normalized;
    }

    /// <summary>So khớp tên đầy đủ hoặc tên đã bỏ tiền tố với chuỗi người dùng nhập.</summary>
    public static bool MatchesName(string fullName, string normalizedInput, string normalizedShortInput)
    {
        var full = NormalizeForSearch(fullName);
        var shortName = RemoveAdministrativePrefix(fullName);

        if (full == normalizedInput || shortName == normalizedInput)
        {
            return true;
        }

        return normalizedShortInput.Length > 0
            && (full == normalizedShortInput || shortName == normalizedShortInput);
    }

    /// <summary>Đoạn có dạng tên tỉnh/thành phố ("Tỉnh …", "Thành phố …", "TP. …").</summary>
    public static bool LooksLikeProvince(string? value)
    {
        var normalized = NormalizeForSearch(value);
        if (normalized.Length == 0) return false;

        return normalized.StartsWith("tinh ", StringComparison.Ordinal)
            || normalized.StartsWith("thanh pho ", StringComparison.Ordinal)
            || normalized.StartsWith("tp.", StringComparison.Ordinal)
            || normalized.StartsWith("tp ", StringComparison.Ordinal);
    }

    /// <summary>Đoạn có dạng tên phường/xã/đặc khu ("Phường …", "Xã …", "Đặc khu …").</summary>
    public static bool LooksLikeWard(string? value)
    {
        var normalized = NormalizeForSearch(value);
        if (normalized.Length == 0) return false;

        return normalized.StartsWith("phuong ", StringComparison.Ordinal)
            || normalized.StartsWith("xa ", StringComparison.Ordinal)
            || normalized.StartsWith("dac khu ", StringComparison.Ordinal)
            || normalized.StartsWith("p. ", StringComparison.Ordinal)
            || normalized.StartsWith("x. ", StringComparison.Ordinal);
    }

    /// <summary>
    /// Đoạn cấp huyện/thành phố thuộc tỉnh của mô hình 3 cấp cũ (Quận 1, Huyện Nhà Bè, TP. Thủ Đức…).
    /// Địa chỉ cũ cần bỏ các đoạn này khi quy về mô hình 2 cấp hiện hành.
    /// </summary>
    public static bool IsLegacyDistrictSegment(string? value)
    {
        var normalized = NormalizeForSearch(value);
        if (normalized.Length == 0) return false;

        return normalized.StartsWith("quan ", StringComparison.Ordinal)
            || normalized.StartsWith("huyen ", StringComparison.Ordinal)
            || normalized.StartsWith("thi xa ", StringComparison.Ordinal)
            || normalized.StartsWith("thi tran ", StringComparison.Ordinal)
            || normalized.StartsWith("thanh pho ", StringComparison.Ordinal)
            || normalized.StartsWith("tp.", StringComparison.Ordinal)
            || normalized.StartsWith("tp ", StringComparison.Ordinal)
            || normalized.StartsWith("q.", StringComparison.Ordinal)
            || normalized.StartsWith("h.", StringComparison.Ordinal)
            || normalized.StartsWith("tx.", StringComparison.Ordinal)
            || normalized.StartsWith("tt.", StringComparison.Ordinal);
    }
}
