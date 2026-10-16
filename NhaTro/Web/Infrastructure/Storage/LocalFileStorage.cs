using Microsoft.Extensions.Options;
using Web.Configuration.Options;

namespace Web.Infrastructure.Storage;

/// <summary>
/// Lưu tệp trên đĩa server, ngoài <c>wwwroot</c>, tổ chức theo năm/tháng (VHT-12 mục 6).
/// Mọi đường dẫn đều do server sinh và được kiểm tra không thoát khỏi thư mục gốc.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(
        IOptions<StorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<LocalFileStorage> logger)
    {
        _rootPath = options.Value.ResolveRootPath(environment.ContentRootPath);
        if (_rootPath.Length == 0)
        {
            throw new InvalidOperationException("Storage:RootPath chưa được cấu hình.");
        }

        _logger = logger;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFileWriteResult> SaveAsync(
        ReadOnlyMemory<byte> content,
        string extension,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        var storedFileName = StoredFileNaming.NewStoredFileName(Guid.NewGuid(), extension);
        var relativePath = StoredFileNaming.BuildRelativePath(uploadedAt, storedFileName);
        var physicalPath = ResolvePhysicalPath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        await using (var stream = new FileStream(
            physicalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await stream.WriteAsync(content, cancellationToken);
        }

        return new StoredFileWriteResult(storedFileName, relativePath, content.Length);
    }

    public Stream? TryOpenRead(string relativePath)
    {
        var physicalPath = ResolvePhysicalPath(relativePath);
        if (!File.Exists(physicalPath))
        {
            // Không ghi đường dẫn vật lý vào log; chỉ ghi tên lưu nội bộ để đối chiếu.
            _logger.LogWarning("Tệp vật lý không tồn tại cho bản ghi {StoredFileName}.",
                Path.GetFileName(physicalPath));
            return null;
        }

        return new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
    }

    public bool Exists(string relativePath) => File.Exists(ResolvePhysicalPath(relativePath));

    public void Delete(string relativePath)
    {
        var physicalPath = ResolvePhysicalPath(relativePath);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }

    // Chỉ chấp nhận đường dẫn tương đối nằm trong thư mục gốc; chặn path traversal.
    private string ResolvePhysicalPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new StoragePathViolationException(relativePath);
        }

        var combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var prefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new StoragePathViolationException(relativePath);
        }

        return combined;
    }
}
