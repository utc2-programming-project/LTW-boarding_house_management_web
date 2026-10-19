using Microsoft.Extensions.Options;
using Web.Configuration.Options;

namespace Web.Configuration.Validation;

/// <summary>
/// Kiểm tra cấu hình storage ngay khi khởi động (VHT-13): thiếu đường dẫn, đặt trong <c>wwwroot</c>
/// hoặc giới hạn không hợp lệ thì dừng ứng dụng thay vì chạy với cấu hình sai.
/// </summary>
public sealed class StorageOptionsValidator(IWebHostEnvironment environment)
    : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        var failures = new List<string>();
        var rootPath = options.ResolveRootPath(environment.ContentRootPath);

        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            failures.Add("Storage:RootPath là bắt buộc.");
        }
        else if (rootPath.Length == 0)
        {
            failures.Add("Storage:RootPath không hợp lệ sau khi mở rộng biến môi trường.");
        }
        else if (IsInside(rootPath, environment.WebRootPath))
        {
            failures.Add("Storage:RootPath phải nằm ngoài wwwroot để tệp không được phục vụ công khai.");
        }

        foreach (var (category, bytes) in options.Limits)
        {
            if (bytes <= 0)
            {
                failures.Add($"Storage:Limits:{category} phải lớn hơn 0.");
            }
        }

        foreach (var (category, count) in options.MaxFilesPerEntity)
        {
            if (count is <= 0)
            {
                failures.Add($"Storage:MaxFilesPerEntity:{category} phải là null (chưa chốt) hoặc lớn hơn 0.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    // Cung thu muc hoac nam sau wwwroot deu bi coi la trong wwwroot.
    private static bool IsInside(string candidate, string? webRootPath)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            return false;
        }

        var webRoot = Path.GetFullPath(webRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return candidate.Equals(webRoot, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(webRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
