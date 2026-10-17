using Web.Configuration.Options;
using Web.Features.Storage.Enums;

namespace Web.Features.Storage.Rules;

/// <summary>
/// Giới hạn hiệu lực của storage: cấu hình trong <c>appsettings</c> thắng, thiếu thì lấy mặc định
/// trong <see cref="FileTypeRules"/>. Không hard-code giới hạn trong service.
/// </summary>
public static class StorageLimits
{
    /// <summary>Dung lượng tối đa (byte) của một loại tệp.</summary>
    public static long MaxBytes(StorageOptions options, FileCategory category) =>
        options.ConfiguredMaxBytes(category.ToString()) ?? FileTypeRules.DefaultMaxBytes(category);

    /// <summary>Số tệp tối đa cho mỗi đối tượng liên quan; <c>null</c> = chưa chốt, không giới hạn.</summary>
    public static int? MaxFilesPerEntity(StorageOptions options, FileCategory category) =>
        options.ConfiguredMaxFilesPerEntity(category.ToString());
}
