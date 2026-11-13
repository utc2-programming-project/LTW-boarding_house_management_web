using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Web.Features.Audit.Entities;

namespace Web.Infrastructure.Auditing;

/// <summary>
/// Kho nhật ký thao tác dạng file JSONL (VHT-104): một file hệ thống cho Admin và một file
/// cho mỗi chủ trọ. Không dùng DB; giữ log trong <c>RetentionDays</c> ngày.
/// </summary>
public sealed class AuditLogFileStore
{
    private const string SystemFileName = "system.jsonl";
    private const string OwnersFolderName = "owners";
    private const string UnscopedFileName = "_unscoped.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly int _retentionDays;
    private readonly string _ownersDirectory;

    public AuditLogFileStore(string rootDirectory, int retentionDays = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
        _retentionDays = retentionDays > 0 ? retentionDays : 3;
        _ownersDirectory = Path.Combine(RootDirectory, OwnersFolderName);
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(_ownersDirectory);
    }

    public string RootDirectory { get; }

    public string SystemFilePath => Path.Combine(RootDirectory, SystemFileName);

    public string OwnerFilePath(string ownerId) =>
        Path.Combine(_ownersDirectory, $"{SafeFileName(ownerId)}.jsonl");

    internal string UnscopedFilePath => Path.Combine(_ownersDirectory, UnscopedFileName);

    /// <summary>
    /// Xác định thư mục gốc: ưu tiên Audit:Directory; nếu trống thì dùng <c>audit</c> trong
    /// thư mục log (Logging:Directory) hoặc %LOCALAPPDATA%\NhaTro\logs.
    /// </summary>
    public static string ResolveRoot(string? auditDirectory, string? loggingDirectory, string contentRootPath)
    {
        var configured = Environment.ExpandEnvironmentVariables(auditDirectory ?? string.Empty).Trim();
        if (configured.Length > 0)
        {
            return Path.GetFullPath(configured, contentRootPath);
        }

        var logConfigured = Environment.ExpandEnvironmentVariables(loggingDirectory ?? string.Empty).Trim();
        var logRoot = logConfigured.Length > 0
            ? Path.GetFullPath(logConfigured, contentRootPath)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NhaTro",
                "logs");

        return Path.Combine(logRoot, "audit");
    }

    public Task AppendAsync(AuditLogFileEntry entry, CancellationToken cancellationToken = default) =>
        AppendManyAsync([entry], cancellationToken);

    /// <summary>Bản đồng bộ cho luồng SaveChanges đồng bộ của EF (hiện repo chưa dùng).</summary>
    public void AppendMany(IReadOnlyCollection<AuditLogFileEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        _gate.Wait();
        try
        {
            foreach (var group in entries.GroupBy(entry => TargetPath(entry.Category, entry.OwnerId)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(group.Key)!);
                var lines = group.Select(entry => JsonSerializer.Serialize(
                    entry with { Metadata = AuditMetadataSanitizer.Sanitize(entry.Metadata) }, JsonOptions));
                File.AppendAllLines(group.Key, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AppendManyAsync(
        IReadOnlyCollection<AuditLogFileEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var group in entries.GroupBy(entry => TargetPath(entry.Category, entry.OwnerId)))
            {
                var path = group.Key;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                foreach (var entry in group)
                {
                    var sanitized = entry with { Metadata = AuditMetadataSanitizer.Sanitize(entry.Metadata) };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(sanitized, JsonOptions));
                }

                await writer.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Đọc log còn trong hạn lưu. Admin đọc file hệ thống; Chủ trọ chỉ đọc file của chính mình.
    /// </summary>
    public async Task<IReadOnlyList<AuditLogFileEntry>> ReadAsync(
        AuditLogCategory category,
        string? ownerId,
        CancellationToken cancellationToken = default)
    {
        var path = TargetPath(category, ownerId);
        if (!File.Exists(path))
        {
            return [];
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_retentionDays);
        var entries = new List<AuditLogFileEntry>();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (TryParse(line, out var entry) && entry.OccurredAt >= cutoff)
                {
                    entries.Add(entry);
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        return entries;
    }

    /// <summary>Xóa dòng cũ hơn hạn lưu và file rỗng. Trả số dòng đã bỏ.</summary>
    public async Task<int> PruneAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_retentionDays);
        var removed = 0;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var files = Directory.Exists(_ownersDirectory)
                ? Directory.EnumerateFiles(_ownersDirectory, "*.jsonl").Prepend(SystemFilePath).ToList()
                : [SystemFilePath];

            foreach (var path in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var kept = new List<string>();
                await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (await reader.ReadLineAsync(cancellationToken) is { } line)
                    {
                        if (TryParse(line, out var entry) && entry.OccurredAt >= cutoff)
                        {
                            kept.Add(line);
                        }
                        else
                        {
                            removed++;
                        }
                    }
                }

                if (kept.Count == 0)
                {
                    File.Delete(path);
                    continue;
                }

                var tempPath = path + ".tmp";
                await File.WriteAllLinesAsync(tempPath, kept, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
                File.Move(tempPath, path, overwrite: true);
            }
        }
        finally
        {
            _gate.Release();
        }

        return removed;
    }

    private string TargetPath(AuditLogCategory category, string? ownerId)
    {
        if (category == AuditLogCategory.Account)
        {
            return SystemFilePath;
        }

        return string.IsNullOrWhiteSpace(ownerId) ? UnscopedFilePath : OwnerFilePath(ownerId);
    }

    /// <summary>Mã chủ trọ lấy từ Identity (GUID) nhưng test có thể dùng chuỗi khác; chặn path traversal.</summary>
    private static string SafeFileName(string ownerId)
    {
        var name = new string(ownerId
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray());

        return name.Length == 0 ? "_unknown" : name;
    }

    private static bool TryParse(string line, out AuditLogFileEntry entry)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<AuditLogFileEntry>(line, JsonOptions);
            if (parsed is not null)
            {
                entry = parsed;
                return true;
            }
        }
        catch (JsonException)
        {
            // Dòng hỏng (ghi dở, chỉnh tay) bị bỏ qua thay vì làm hỏng cả màn hình.
        }

        entry = null!;
        return false;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
