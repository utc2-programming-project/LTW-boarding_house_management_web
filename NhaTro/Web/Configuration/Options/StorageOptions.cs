namespace Web.Configuration.Options;

/// <summary>
/// Cấu hình storage dùng chung (VHT-13), bind từ section "Storage" trong appsettings.
/// Không chứa secret và không hard-code đường dẫn máy chủ: mọi giá trị đến từ cấu hình/môi trường.
/// </summary>
/// <remarks>
/// Đây là hợp đồng VHT-12: <c>RootPath</c> phải nằm ngoài <c>wwwroot</c>; giới hạn theo loại tệp
/// đặt trong cấu hình, không ghim trong code. Khoá của từ điển là tên <c>FileCategory</c>
/// (ví dụ <c>RoomPhoto</c>) để tầng Configuration không phụ thuộc Features.
/// </remarks>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Thư mục gốc lưu tệp thật, nằm ngoài <c>wwwroot</c>. Hỗ trợ biến môi trường
    /// (ví dụ <c>%LOCALAPPDATA%\NhaTro\storage</c>) và đường dẫn tương đối so với content root.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Dung lượng tối đa (byte) theo tên loại tệp. Thiếu khoá thì dùng mặc định trong <c>FileTypeRules</c>.</summary>
    public Dictionary<string, long> Limits { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Số tệp tối đa cho mỗi đối tượng liên quan theo tên loại tệp.
    /// <c>null</c> = chưa chốt (không giới hạn); VHT-12 còn để ngỏ giá trị này.
    /// </summary>
    public Dictionary<string, int?> MaxFilesPerEntity { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Giới hạn cấu hình cho một loại tệp, trả <c>null</c> khi chưa khai báo.</summary>
    public long? ConfiguredMaxBytes(string categoryName) =>
        Limits.TryGetValue(categoryName, out var value) && value > 0 ? value : null;

    /// <summary>Số tệp tối đa cho một loại tệp, trả <c>null</c> khi chưa chốt.</summary>
    public int? ConfiguredMaxFilesPerEntity(string categoryName) =>
        MaxFilesPerEntity.TryGetValue(categoryName, out var value) && value > 0 ? value : null;

    /// <summary>
    /// Đường dẫn tuyệt đối của thư mục gốc: mở rộng biến môi trường, ghép vào content root nếu là
    /// đường dẫn tương đối và chuẩn hoá. Dùng chung cho validator và <c>LocalFileStorage</c>.
    /// </summary>
    public string ResolveRootPath(string contentRootPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(RootPath).Trim();
        if (expanded.Length == 0)
        {
            return string.Empty;
        }

        var combined = Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(contentRootPath, expanded);

        return Path.GetFullPath(combined);
    }
}
