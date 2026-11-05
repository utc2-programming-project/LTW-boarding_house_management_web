using System.Security.Cryptography;
using System.Text.Json;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// Một ô trong sheet "Ma trận quyền" của docs/task/thao_tac_va_quyen.xlsx.
/// Địa chỉ ô Excel được giữ nguyên để mọi kết quả test truy vết ngược về workbook.
/// </summary>
public sealed record MatrixCell(
    string Sheet,
    string Cell,
    int Row,
    int OperationNumber,
    string OperationCode,
    string Module,
    string OperationName,
    string Role,
    string RoleHeader,
    string Symbol,
    bool Applicable,
    string ExpectedPermission,
    string Scope,
    string AllowedExample,
    string DeniedExample,
    string StatusNote,
    string ImplementationNote)
{
    /// <summary>Định danh test theo quy ước task: Sheet + CellAddress + OperationCode + Role.</summary>
    public string TestId => $"{Module}_{Cell}_{OperationCode}_{Role}";

    public override string ToString() => $"{Cell} | {OperationCode} {OperationName} | {Role} | {Symbol}";
}

public sealed record ManagerSwitch(
    string Code,
    int MatrixOperationNumber,
    int MatrixRow,
    string MatrixCell,
    string Module,
    string OperationName,
    string[] Modes,
    string DefaultMode,
    string? Note);

public sealed record PermissionMatrixWorkbook(
    string Path,
    string Sha256,
    string[] Sheets,
    string MatrixSheet,
    string SwitchSheet,
    string DecisionSheet);

/// <summary>
/// Manifest do scripts/permission_matrix/build_manifest.py sinh từ workbook.
/// Workbook là nguồn sự thật duy nhất: hash của file .xlsx được kiểm mỗi lần加载 manifest,
/// nên workbook đổi mà chưa sinh lại manifest thì bộ test fail ngay thay vì chạy trên bản cũ.
/// </summary>
public static class PermissionMatrixManifest
{
    private const string ManifestRelativePath = "docs/test-results/permission_matrix_manifest.json";
    private const string WorkbookRelativePath = "docs/task/thao_tac_va_quyen.xlsx";

    private static readonly Lazy<Loaded> Instance = new(Load);

    public static Loaded Current => Instance.Value;

    public static IReadOnlyList<MatrixCell> AllCells => Current.Cells;

    /// <summary>173 ô không phải "—" — mỗi ô bắt buộc có ít nhất một test hành vi.</summary>
    public static IReadOnlyList<MatrixCell> ApplicableCells =>
        Current.Cells.Where(cell => cell.Applicable).ToList();

    public static IReadOnlyList<ManagerSwitch> Switches => Current.Switches;

    public static IReadOnlyList<MatrixRole> Roles => Current.Roles;

    public static MatrixLegend Legend => Current.Legend;

    public static IReadOnlyList<BusinessDecision> Decisions => Current.Decisions;

    public static RawIntegrity Integrity => Current.Integrity;

    public static MatrixCell Cell(string cellAddress) =>
        Current.Cells.First(cell => cell.Cell == cellAddress);

    public static MatrixCell? FindCell(int operationNumber, string role) =>
        Current.Cells.FirstOrDefault(cell => cell.OperationNumber == operationNumber && cell.Role == role);

    public static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, WorkbookRelativePath))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Không tìm thấy workbook '{WorkbookRelativePath}' khi đi ngược từ {AppContext.BaseDirectory}.");
    }

    private static Loaded Load()
    {
        var root = FindRepoRoot();
        var manifestPath = Path.Combine(root, ManifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException(
                $"Thiếu manifest {ManifestRelativePath}. Chạy: python scripts/permission_matrix/build_manifest.py");
        }

        var workbookPath = Path.Combine(root, WorkbookRelativePath);
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(workbookPath)));
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var expectedHash = document.RootElement.GetProperty("workbook").GetProperty("sha256").GetString();
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Workbook {WorkbookRelativePath} đã đổi nhưng manifest chưa được sinh lại. " +
                $"Chạy: python scripts/permission_matrix/build_manifest.py");
        }

        var parsed = JsonSerializer.Deserialize<RawManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (parsed is null) throw new InvalidOperationException($"Manifest rỗng hoặc sai cấu trúc: {manifestPath}");

        var cells = parsed.Operations
            .SelectMany(operation => operation.Cells.Select(cell => cell with
            {
                OperationNumber = operation.OperationNumber,
                Module = cell.Module.Length > 0 ? cell.Module : operation.Module,
            }))
            .ToList();

        return new Loaded(
            cells,
            parsed.ManagerSwitches ?? [],
            new PermissionMatrixWorkbook(
                WorkbookRelativePath,
                expectedHash!,
                parsed.Workbook.Sheets,
                parsed.Workbook.MatrixSheet,
                parsed.Workbook.SwitchSheet,
                parsed.Workbook.DecisionSheet),
            parsed.Integrity,
            parsed.Roles,
            parsed.Legend,
            parsed.Decisions ?? []);
    }

    public sealed record Loaded(
        IReadOnlyList<MatrixCell> Cells,
        IReadOnlyList<ManagerSwitch> Switches,
        PermissionMatrixWorkbook Workbook,
        RawIntegrity Integrity,
        IReadOnlyList<MatrixRole> Roles,
        MatrixLegend Legend,
        IReadOnlyList<BusinessDecision> Decisions);

    public sealed record MatrixRole(string Column, string Header, string IdentityRole);

    public sealed record MatrixNote(string Cell, string Text);

    public sealed record MatrixLegend(
        Dictionary<string, string> ExpectedPermission,
        MatrixNote[] MatrixNotes,
        MatrixNote[] SwitchNotes);

    public sealed record BusinessDecision(string Code, string Issue, string Proposal);

    public sealed record RawOperation(
        int OperationNumber,
        string OperationCode,
        string Module,
        string OperationName,
        int ExcelRow,
        MatrixCell[] Cells);

    public sealed record RawWorkbook(string Path, string Sha256, string[] Sheets, string MatrixSheet, string SwitchSheet, string DecisionSheet);

    public sealed record RawIntegrity(
        int OperationCount,
        int RoleCount,
        int CellCount,
        int ApplicableCellCount,
        Dictionary<string, int> SymbolCounts,
        int SwitchCount,
        JsonElement[] Problems);

    public sealed record RawManifest(
        int SchemaVersion,
        RawWorkbook Workbook,
        MatrixRole[] Roles,
        MatrixLegend Legend,
        RawIntegrity Integrity,
        ManagerSwitch[]? ManagerSwitches,
        BusinessDecision[]? Decisions,
        RawOperation[] Operations);
}

/// <summary>Ký hiệu ma trận theo legend của workbook; không tự ý thêm ký hiệu mới.</summary>
public static class MatrixSymbol
{
    public const string Allowed = "✅";
    public const string Conditional = "⚠️";
    public const string Forbidden = "❌";
    public const string SpecialModes = "⚙️";
    public const string NotApplicable = "—";
}
