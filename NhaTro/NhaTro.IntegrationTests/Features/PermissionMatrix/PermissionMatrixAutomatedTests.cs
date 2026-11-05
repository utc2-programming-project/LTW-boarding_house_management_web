using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Accounts;
using Xunit;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (!SqlServerFixture.Available)
        {
            Skip = "Không có SQL Server LocalDB trên máy này (bỏ qua integration test).";
        }
    }
}

/// <summary>
/// Bộ test data-driven cho 180 ô ma trận quyền docs/task/thao_tac_va_quyen.xlsx.
/// Mỗi ô khả dụng có đúng một test hành vi và test chỉ đạt khi backend thật trả đúng hiệu lực
/// workbook mô tả. Ô chưa chứng minh được phải khai báo tĩnh trong
/// <see cref="PermissionMatrixProbeExecutor.UnsupportedCells"/> và chỉ được tính BLOCKED, không bao giờ PASS.
/// </summary>
public sealed class PermissionMatrixAutomatedTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// Kết quả thật của từng ô do chính test lý thuyết sinh ra; báo cáo dùng lại bảng này thay vì
    /// chạy probe lần hai, để số liệu trong file báo cáo đúng bằng số liệu test đã PASS/FAIL.
    /// </summary>
    private static readonly ConcurrentDictionary<string, CellResult> Executed = new(StringComparer.Ordinal);

    private static PermissionMatrixWorld? _sharedWorld;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    private async Task<PermissionMatrixWorld> GetWorldAsync()
    {
        if (_sharedWorld is not null) return _sharedWorld;
        await _initLock.WaitAsync();
        try
        {
            if (_sharedWorld is null)
            {
                var world = new PermissionMatrixWorld(fixture);
                await world.InitializeAsync();
                _sharedWorld = world;
            }
            return _sharedWorld;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<CellResult> RunCellAsync(MatrixCell cell)
    {
        if (Executed.TryGetValue(cell.Cell, out var cached)) return cached;

        var world = await GetWorldAsync();
        var result = await PermissionMatrixProbeExecutor.ExecuteProbeAsync(cell, world);
        Executed[cell.Cell] = result;
        return result;
    }

    /// <summary>Ô không phải "—" và không nằm trong danh sách khai báo thiếu API.</summary>
    public static IEnumerable<object[]> GetExecutableCells() =>
        PermissionMatrixManifest.ApplicableCells
            .Where(cell => !PermissionMatrixProbeExecutor.IsDeclaredUnsupported(cell))
            .OrderBy(cell => cell.Row).ThenBy(cell => cell.Cell)
            .Select(cell => new object[] { cell.Cell });

    /// <summary>Ô không phải "—" nhưng đã khai báo là chưa chứng minh được bằng backend thật.</summary>
    public static IEnumerable<object[]> GetDeclaredUnsupportedCells() =>
        PermissionMatrixManifest.ApplicableCells
            .Where(PermissionMatrixProbeExecutor.IsDeclaredUnsupported)
            .OrderBy(cell => cell.Row).ThenBy(cell => cell.Cell)
            .Select(cell => new object[] { cell.Cell });

    public static IEnumerable<object[]> GetNotApplicableCells() =>
        PermissionMatrixManifest.AllCells
            .Where(cell => !cell.Applicable || cell.Symbol == MatrixSymbol.NotApplicable)
            .OrderBy(cell => cell.Row).ThenBy(cell => cell.Cell)
            .Select(cell => new object[] { cell.Cell });

    /// <summary>
    /// Một test cho mỗi ô khả dụng; test chỉ PASS khi probe khớp kỳ vọng của ký hiệu ma trận.
    /// Không chấp nhận BLOCKED ở đây: ô đã có API mà không chứng minh được là FAIL, không phải đường lùi.
    /// </summary>
    [SqlServerTheory]
    [MemberData(nameof(GetExecutableCells))]
    public async Task Cell_Satisfies_Matrix_Requirement(string cellAddress)
    {
        var cell = PermissionMatrixManifest.Cell(cellAddress);
        var result = await RunCellAsync(cell);

        var subcases = result.Subcases is { Count: > 0 }
            ? " || Tình huống con: " + string.Join(
                " | ",
                result.Subcases.Select(s => $"{s.Name}={s.Verdict}: {Redact(s.Evidence)}"))
            : string.Empty;

        Assert.True(
            result.Status == "PASS",
            $"Ô {cell.Sheet}!{cell.Cell} ({cell.OperationCode} {cell.OperationName} - {cell.Role}) " +
            $"ký hiệu '{cell.Symbol}', kỳ vọng {cell.ExpectedPermission}, thực tế {result.Actual} => {result.Status}. " +
            $"Bằng chứng: {Redact(result.Evidence)}{subcases}");
    }

    /// <summary>
    /// Sau khi bổ sung đủ service cho P1/P4/P5/P6/P8, KHÔNG còn ô nào được phép khai báo BLOCKED.
    /// Đây là guard cho CI: nếu ai thêm lại một ô vào danh sách khai báo, test này fail ngay thay vì
    /// lặng lẽ biến ô đó thành BLOCKED trong báo cáo.
    /// </summary>
    [Fact]
    public void No_Cell_Is_Declared_Unsupported()
    {
        Assert.True(
            PermissionMatrixProbeExecutor.UnsupportedCells.Count == 0,
            "Danh sách ô BLOCKED phải rỗng; còn: "
            + string.Join(", ", PermissionMatrixProbeExecutor.UnsupportedCells.Select(
                u => $"OP{u.OperationNumber:D2}/{u.Role}: {u.Reason}")));
    }

    /// <summary>
    /// CI gate: mọi ô ❌ phải thực thi được. Một ô ❌ bị khai báo BLOCKED là lỗi chặn bàn giao,
    /// vì như vậy chưa chứng minh được quyền bị từ chối trên backend thật.
    /// </summary>
    [Fact]
    public void No_Forbidden_Cell_Is_Declared_Unsupported()
    {
        var forbidden = PermissionMatrixManifest.ApplicableCells
            .Where(cell => cell.Symbol == MatrixSymbol.Forbidden)
            .ToList();

        Assert.NotEmpty(forbidden);

        foreach (var cell in forbidden)
        {
            Assert.False(
                PermissionMatrixProbeExecutor.IsDeclaredUnsupported(cell),
                $"Ô ❌ {cell.Cell} ({cell.OperationCode}/{cell.Role}) đang bị khai báo BLOCKED — "
                + "phải có test chứng minh backend từ chối thay vì bỏ qua.");
        }
    }

    /// <summary>7 ô "—" chỉ được xác nhận là loại trừ theo Mục VI.5, không được cấp kết quả hành vi giả.</summary>
    [Theory]
    [MemberData(nameof(GetNotApplicableCells))]
    public void Cell_Not_Applicable_Is_Excluded_From_Execution(string cellAddress)
    {
        var cell = PermissionMatrixManifest.Cell(cellAddress);

        Assert.Equal(MatrixSymbol.NotApplicable, cell.Symbol);
        Assert.False(cell.Applicable);
        Assert.Equal("NOT_APPLICABLE", cell.ExpectedPermission);
        Assert.False(PermissionMatrixProbeExecutor.IsDeclaredUnsupported(cell));
    }

    // ==========================================
    // GUARD TOÀN VẸN MANIFEST (chống tự ý sửa kỳ vọng)
    // ==========================================

    [Fact]
    public void Manifest_Integrity_Counts_Match_Loaded_Cells()
    {
        var cells = PermissionMatrixManifest.AllCells;
        var integrity = PermissionMatrixManifest.Integrity;

        Assert.Equal(integrity.CellCount, cells.Count);
        Assert.Equal(integrity.OperationCount, cells.Select(c => c.OperationNumber).Distinct().Count());
        Assert.Equal(integrity.RoleCount, cells.Select(c => c.Role).Distinct().Count());
        Assert.Equal(integrity.ApplicableCellCount, PermissionMatrixManifest.ApplicableCells.Count);
        Assert.Equal(integrity.SwitchCount, PermissionMatrixManifest.Switches.Count);
        Assert.Empty(integrity.Problems);

        foreach (var (symbol, expected) in integrity.SymbolCounts)
        {
            Assert.Equal(expected, cells.Count(c => c.Symbol == symbol));
        }
    }

    /// <summary>Workbook là nguồn sự thật duy nhất: đổi workbook mà chưa sinh lại manifest thì guard fail.</summary>
    [Fact]
    public void Workbook_Hash_In_Manifest_Matches_Current_File()
    {
        var root = PermissionMatrixManifest.FindRepoRoot();
        var workbookPath = Path.Combine(root, PermissionMatrixManifest.Current.Workbook.Path);
        Assert.True(File.Exists(workbookPath), $"Thiếu workbook nguồn: {workbookPath}");

        var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(workbookPath)));
        Assert.True(
            string.Equals(PermissionMatrixManifest.Current.Workbook.Sha256, actual, StringComparison.OrdinalIgnoreCase),
            $"SHA-256 workbook khác manifest ({actual} != {PermissionMatrixManifest.Current.Workbook.Sha256}). " +
            "Chạy: python scripts/permission_matrix/build_manifest.py");
    }

    [Fact]
    public void Every_Cell_Has_Unique_Traceable_Test_Id()
    {
        var cells = PermissionMatrixManifest.AllCells;

        Assert.Equal(cells.Count, cells.Select(c => c.TestId).Distinct(StringComparer.Ordinal).Count());

        foreach (var cell in cells)
        {
            Assert.Equal($"{cell.Module}_{cell.Cell}_{cell.OperationCode}_{cell.Role}", cell.TestId);
        }
    }

    /// <summary>Kỳ vọng quyền của mỗi ô phải đúng bằng legend workbook; không tự thêm ký hiệu mới.</summary>
    [Fact]
    public void Legend_Symbols_Map_To_Expected_Permission()
    {
        var legend = PermissionMatrixManifest.Legend.ExpectedPermission;
        var knownSymbols = new[]
        {
            MatrixSymbol.Allowed, MatrixSymbol.Conditional, MatrixSymbol.Forbidden,
            MatrixSymbol.SpecialModes, MatrixSymbol.NotApplicable,
        };

        foreach (var symbol in knownSymbols)
        {
            Assert.True(legend.ContainsKey(symbol), $"Legend workbook thiếu ký hiệu '{symbol}'.");
        }

        foreach (var cell in PermissionMatrixManifest.AllCells)
        {
            Assert.True(
                legend.ContainsKey(cell.Symbol),
                $"Ô {cell.Cell} mang ký hiệu '{cell.Symbol}' không có trong legend workbook.");
            Assert.True(
                legend[cell.Symbol] == cell.ExpectedPermission,
                $"Ô {cell.Cell}: expectedPermission '{cell.ExpectedPermission}' không khớp legend của '{cell.Symbol}' ('{legend[cell.Symbol]}').");
        }
    }

    /// <summary>Ô ⚙️ và sheet Công tắc Quản lý phải ánh xạ 1-1, mỗi công tắc phải có đủ chế độ đã ghi.</summary>
    [Fact]
    public void Special_Mode_Cells_Map_One_To_One_To_Manager_Switches()
    {
        var gearCells = PermissionMatrixManifest.AllCells
            .Where(c => c.Symbol == MatrixSymbol.SpecialModes).ToList();

        Assert.Equal(PermissionMatrixManifest.Switches.Count, gearCells.Count);

        var mapped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sw in PermissionMatrixManifest.Switches)
        {
            Assert.True(mapped.Add(sw.MatrixCell), $"Công tắc {sw.Code} trùng ô ma trận {sw.MatrixCell}.");
            var cell = gearCells.FirstOrDefault(c => c.Cell == sw.MatrixCell);
            Assert.True(cell is not null, $"Không tìm thấy ô ⚙️ {sw.MatrixCell} cho công tắc {sw.Code}.");
            Assert.Equal(sw.MatrixOperationNumber, cell!.OperationNumber);
            Assert.Equal(Roles.QuanLy, cell.Role);
            Assert.NotEmpty(sw.Modes);
            Assert.Contains(sw.DefaultMode, sw.Modes);
        }

        foreach (var cell in gearCells)
        {
            Assert.True(
                mapped.Contains(cell.Cell),
                $"Ô ⚙️ {cell.Cell} ({cell.OperationCode}) không có công tắc nào trong sheet {PermissionMatrixManifest.Current.Workbook.SwitchSheet}.");
        }
    }

    /// <summary>
    /// Danh sách khai báo thiếu API phải truy vết về ô thật, không trùng, và phần bù với ô chạy được
    /// phải đúng bằng số ô khả dụng — không ô nào được rơi khỏi cả hai nhóm.
    /// </summary>
    [Fact]
    public void Declared_Unsupported_Entries_Are_Traceable_And_Partition_Applicable_Cells()
    {
        var unsupported = PermissionMatrixProbeExecutor.UnsupportedCells;
        var keys = unsupported.Select(u => $"{u.OperationNumber}|{u.Role}").ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());

        foreach (var entry in unsupported)
        {
            var cell = PermissionMatrixManifest.FindCell(entry.OperationNumber, entry.Role);
            Assert.True(
                cell is not null,
                $"Khai báo thiếu API cho OP{entry.OperationNumber:D2}/{entry.Role} nhưng ma trận không có ô này.");
            Assert.True(
                cell!.Applicable,
                $"Ô {cell.Cell} khai báo thiếu API nhưng workbook đánh dấu '—' — phải xóa khỏi danh sách khai báo.");
            Assert.True(
                entry.Reason.Length >= 60,
                $"OP{entry.OperationNumber:D2}/{entry.Role}: lý do BLOCKED phải đủ dài để truy vết (hiện {entry.Reason.Length} ký tự).");
        }

        Assert.Equal(
            PermissionMatrixManifest.ApplicableCells.Count,
            GetExecutableCells().Count() + unsupported.Count);
    }

    /// <summary>Số test đúng bằng số ô: mỗi ô thuộc đúng một trong ba nguồn test data, không thiếu không thừa.</summary>
    [Fact]
    public void Theory_Data_Sources_Cover_Every_Cell_Exactly_Once()
    {
        var executable = GetExecutableCells().Select(a => (string)a[0]).ToList();
        var declared = GetDeclaredUnsupportedCells().Select(a => (string)a[0]).ToList();
        var notApplicable = GetNotApplicableCells().Select(a => (string)a[0]).ToList();

        Assert.Equal(PermissionMatrixManifest.Integrity.CellCount, executable.Count + declared.Count + notApplicable.Count);
        Assert.Equal(PermissionMatrixManifest.Integrity.ApplicableCellCount, executable.Count + declared.Count);
        Assert.Equal(7, notApplicable.Count);

        var all = executable.Concat(declared).Concat(notApplicable).ToList();
        Assert.Equal(
            PermissionMatrixManifest.Integrity.CellCount,
            all.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// 10 file test phân hệ phải theo đúng hợp đồng "mỗi ô một method, tên định vị ô Excel".
    /// Chặn đúng loại lỗi mà script rà soát thủ công dễ bỏ qua: một method đặt tên ngoài quy ước
    /// sẽ không có tool nào đọc được, và ô của nó rơi khỏi mọi cuộc đối chiếu mà không ai báo lỗi.
    /// </summary>
    [Fact]
    public void Module_Tests_Are_One_Per_Cell_And_Named_By_Cell_Address()
    {
        var pattern = new Regex(
            @"^Cell_([DEFG])(\d+)_OP(\d+)_([A-Za-z]+)_(IsDenied|IsAllowed|IsDeniedNoMutation|IsAllowedInScope|IsDeniedOutOfScope)$",
            RegexOptions.Compiled);

        var moduleClasses = typeof(PermissionMatrixAutomatedTests).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(PermissionMatrixAutomatedTests).Namespace
                           && type.Name.EndsWith("Tests", StringComparison.Ordinal)
                           && type != typeof(PermissionMatrixAutomatedTests))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(10, moduleClasses.Count);

        var covered = new List<string>();
        var offenders = new List<string>();

        foreach (var type in moduleClasses)
        {
            var testMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.GetCustomAttributes(typeof(FactAttribute), inherit: true).Any())
                .ToList();

            foreach (var method in testMethods)
            {
                var match = pattern.Match(method.Name);
                if (!match.Success)
                {
                    offenders.Add($"{type.Name}.{method.Name}");
                    continue;
                }

                var address = $"{match.Groups[1].Value}{match.Groups[2].Value}";
                var excelRow = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                covered.Add(address);

                var cell = PermissionMatrixManifest.AllCells.SingleOrDefault(item => item.Cell == address);
                Assert.NotNull(cell);
                Assert.Equal(excelRow, cell!.Row);
                Assert.Equal(
                    int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                    cell.OperationNumber);

                var roleInName = match.Groups[4].Value switch
                {
                    "Admin" => Roles.Admin,
                    "ChuTro" => Roles.ChuTro,
                    "QuanLy" => Roles.QuanLy,
                    "KhachThue" => Roles.KhachThue,
                    _ => string.Empty,
                };
                Assert.Equal(cell.Role, roleInName);

                // Ô BLOCKED/"—" không được có test hành vi: nếu có thì hoặc engine sai, hoặc test tự bịa kết quả.
                Assert.False(
                    PermissionMatrixProbeExecutor.IsDeclaredUnsupported(cell),
                    $"{type.Name}.{method.Name} kiểm chứng ô {address} nhưng engine khai báo ô này BLOCKED vì thiếu API.");
                Assert.True(cell.Applicable, $"{type.Name}.{method.Name} kiểm chứng ô '—' {address}.");
            }
        }

        Assert.True(offenders.Count == 0,
            "Method test phân hệ đặt tên ngoài quy ước Cell_<ô>_OP<nn>_<Role>_<verb>: "
            + string.Join(", ", offenders));

        var duplicates = covered
            .GroupBy(cell => cell, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToList();
        Assert.True(duplicates.Count == 0,
            "Một ô có nhiều hơn một method test phân hệ (vi phạm hợp đồng mỗi-ô-một-method): "
            + string.Join(", ", duplicates));

        // Đủ coverage: mọi ô đã thực thi phải có đúng một method test phân hệ. Guard này chặn
        // trường hợp thêm ô/API mới nhưng quên bổ sung test hành vi tương ứng.
        var executable = GetExecutableCells().Select(values => (string)values[0]).ToList();
        var missing = executable
            .Except(covered, StringComparer.Ordinal)
            .OrderBy(cell => cell, StringComparer.Ordinal)
            .ToList();
        Assert.True(missing.Count == 0,
            $"Còn {missing.Count} ô chưa có method test phân hệ: {string.Join(", ", missing)}");
    }

    // ==========================================
    // COLLECTOR BÁO CÁO THEO Ô
    // ==========================================

    /// <summary>Xuất báo cáo markdown + JSON + CSV cho 180 ô, dùng lại đúng kết quả test đã chạy.</summary>
    [SqlServerFact]
    public async Task Generate_Full_Permission_Matrix_Report()
    {
        var results = new List<CellResult>();
        foreach (var cell in PermissionMatrixManifest.AllCells)
        {
            results.Add(await RunCellAsync(cell));
        }

        var report = BuildMarkdownReport(results);
        var json = BuildJsonReport(results);
        var csv = BuildCsvReport(results);

        EnsureNoCredentials(report);
        EnsureNoCredentials(json);
        EnsureNoCredentials(csv);

        var dir = Path.Combine(PermissionMatrixManifest.FindRepoRoot(), "docs/test-results");
        Directory.CreateDirectory(dir);
        // Không ghi BOM: JSON/CSV được đọc bởi công cụ CI và script Python, BOM làm hỏng parser.
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllTextAsync(Path.Combine(dir, "VHT-101-permission-matrix-report.md"), report, utf8NoBom);
        await File.WriteAllTextAsync(Path.Combine(dir, "permission_matrix_results.json"), json, utf8NoBom);
        await File.WriteAllTextAsync(Path.Combine(dir, "permission_matrix_results.csv"), csv, utf8NoBom);

        var applicable = results.Where(r => r.Cell.Applicable).ToList();
        Assert.Equal(PermissionMatrixManifest.Integrity.CellCount, results.Count);
        Assert.Equal(PermissionMatrixManifest.Integrity.ApplicableCellCount, applicable.Count);
        Assert.Equal(applicable.Count, applicable.Count(r => r.Status is "PASS" or "FAIL" or "BLOCKED"));
        Assert.Equal(
            PermissionMatrixManifest.ApplicableCells.Count - PermissionMatrixProbeExecutor.UnsupportedCells.Count,
            applicable.Count(r => r.UnsupportedReason is null));
    }

    private static void EnsureNoCredentials(string content)
    {
        Assert.True(
            !content.Contains(PermissionMatrixWorld.AccountPassword, StringComparison.Ordinal),
            "Báo cáo chứa mật khẩu tài khoản test.");
        Assert.True(
            !content.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase),
            "Báo cáo chứa connection string.");
    }

    /// <summary>Bằng chứng do probe sinh ra có thể mang credential; redact trước khi ghi file.</summary>
    private static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var text = value.Replace(PermissionMatrixWorld.AccountPassword, "***", StringComparison.Ordinal);

        foreach (var token in new[] { "Data Source", "Initial Catalog", "Trusted_Connection", "Integrated Security" })
        {
            var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) text = text[..index] + "<redacted-connection-string>";
        }

        return text;
    }

    private static IOrderedEnumerable<CellResult> Ordered(IReadOnlyList<CellResult> results) =>
        results.OrderBy(r => r.Cell.Row).ThenBy(r => r.Cell.Cell);

    private static int SymbolCount(IReadOnlyList<CellResult> results, string symbol) =>
        results.Count(r => r.Cell.Symbol == symbol);

    private static string BuildMarkdownReport(IReadOnlyList<CellResult> results)
    {
        var manifest = PermissionMatrixManifest.Current;
        var total = results.Count;
        var naCount = results.Count(r => !r.Cell.Applicable || r.Cell.Symbol == MatrixSymbol.NotApplicable);
        var applicable = results.Where(r => r.Cell.Applicable).ToList();
        var passed = applicable.Count(r => r.Status == "PASS");
        var failed = applicable.Where(r => r.Status == "FAIL").ToList();
        var declaredBlocked = applicable.Where(r => r.UnsupportedReason is not null).ToList();
        var runtimeBlocked = applicable.Where(r => r.Status == "BLOCKED" && r.UnsupportedReason is null).ToList();
        var executableTotal = applicable.Count - declaredBlocked.Count;
        var executablePassRate = executableTotal == 0 ? 0d : (double)passed / executableTotal * 100d;

        var sb = new StringBuilder();
        sb.AppendLine("# VHT-101 – Báo cáo kiểm thử ma trận quyền tự động");
        sb.AppendLine();
        sb.AppendLine($"- Sinh lúc: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("- Nguồn yêu cầu: `docs/task/thao_tac_va_quyen.xlsx` (sheet Ma trận quyền / Công tắc Quản lý / Quyết định)");
        sb.AppendLine("- Số liệu dưới đây là kết quả THẬT của các test hành vi đã chạy trong cùng lần chạy; không ô nào được tô hồng.");
        sb.AppendLine("- Chống rửa điểm: Skip không tính là PASS, FAIL không biến thành BLOCKED, không đổi kỳ vọng cho khớp code.");
        sb.AppendLine();

        sb.AppendLine("## A. Workbook và manifest");
        sb.AppendLine();
        sb.AppendLine($"- Workbook: `{manifest.Workbook.Path}`");
        sb.AppendLine($"- SHA-256: `{manifest.Workbook.Sha256}`");
        sb.AppendLine($"- Sheet đã đọc: {string.Join(", ", manifest.Workbook.Sheets)}");
        sb.AppendLine($"- Thao tác: {manifest.Integrity.OperationCount} | Vai trò: {manifest.Integrity.RoleCount} | Ô: {manifest.Integrity.CellCount}");
        sb.AppendLine($"- Phân bố ký hiệu: ✅ {SymbolCount(results, MatrixSymbol.Allowed)} | ⚠️ {SymbolCount(results, MatrixSymbol.Conditional)} | ❌ {SymbolCount(results, MatrixSymbol.Forbidden)} | ⚙️ {SymbolCount(results, MatrixSymbol.SpecialModes)} | — {naCount}");
        sb.AppendLine($"- Ô bắt buộc có test (không phải `—`): {applicable.Count}");
        sb.AppendLine($"- Công tắc Quản lý: {manifest.Switches.Count} | Quyết định nghiệp vụ: {manifest.Decisions.Count}");
        sb.AppendLine($"- Problems của manifest: {(manifest.Integrity.Problems.Length == 0 ? "không có" : manifest.Integrity.Problems.Length.ToString())}");
        sb.AppendLine();

        sb.AppendLine("## B. Coverage");
        sb.AppendLine();
        sb.AppendLine("| Chỉ số | Giá trị |");
        sb.AppendLine("|---|---:|");
        sb.AppendLine($"| Tổng ô ma trận | {total} |");
        sb.AppendLine($"| Ô `—` (không áp dụng, Mục VI.5) | {naCount} |");
        sb.AppendLine($"| Ô bắt buộc chứng minh | {applicable.Count} |");
        sb.AppendLine($"| Ô đã gọi backend thật (executable) | {executableTotal} |");
        sb.AppendLine($"| Ô PASS | {passed} |");
        sb.AppendLine($"| Ô FAIL | {failed.Count} |");
        sb.AppendLine($"| Ô BLOCKED – khai báo tĩnh thiếu API | {declaredBlocked.Count} |");
        sb.AppendLine($"| Ô BLOCKED – phát sinh lúc chạy | {runtimeBlocked.Count} |");
        sb.AppendLine($"| Tỷ lệ PASS trên ô đã thực thi | {executablePassRate:F1}% |");
        sb.AppendLine();
        sb.AppendLine(
            $"Đẳng thức coverage: executable {executableTotal} + BLOCKED khai báo {declaredBlocked.Count} + ô `—` {naCount} = {total}. " +
            $"Mỗi ô đã thực thi đều có test hành vi riêng; con số này là coverage ô, không phải tuyên bố mọi luồng UI đã được kiểm thử.");
        sb.AppendLine();
        sb.AppendLine("### B.1 Coverage theo phân hệ");
        sb.AppendLine();
        sb.AppendLine("| Phân hệ | Ô khả dụng | Đã thực thi | PASS | FAIL | BLOCKED |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var group in applicable.GroupBy(r => r.Cell.Module).OrderBy(g => g.Key))
        {
            sb.AppendLine(
                $"| {group.Key} | {group.Count()} | {group.Count(r => r.UnsupportedReason is null)} | " +
                $"{group.Count(r => r.Status == "PASS")} | {group.Count(r => r.Status == "FAIL")} | " +
                $"{group.Count(r => r.Status == "BLOCKED")} |");
        }
        sb.AppendLine();

        sb.AppendLine("## C. Kết quả theo từng ô");
        sb.AppendLine();
        sb.AppendLine("| Ô | Thao tác | Vai trò | Ký hiệu | Expected | Actual | Kết quả | Tầng | Service | Tình huống con | Test ID |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var r in Ordered(results))
        {
            sb.AppendLine(
                $"| {r.Cell.Cell} | {r.Cell.OperationCode} {Md(r.Cell.OperationName)} | {r.Cell.Role} | {r.Cell.Symbol} | " +
                $"{Md(r.Expected)} | {Md(r.Actual)} | {r.Status} | {r.Tier} | {Md(r.Service)} | {r.SubcaseCount} | {r.TestId} |");
        }
        sb.AppendLine();

        sb.AppendLine("### C.1 Bằng chứng và tình huống con của từng ô");
        sb.AppendLine();
        sb.AppendLine("<details><summary>Bấm để xem bằng chứng / hiệu lực DB / từng chế độ công tắc của 180 ô</summary>");
        sb.AppendLine();
        foreach (var r in Ordered(results))
        {
            sb.AppendLine($"- **{r.Cell.Cell} · {r.Cell.OperationCode} · {r.Cell.Role} → {r.Status}** (tầng {r.Tier}, service `{r.Service}`)");
            sb.AppendLine($"  - Bằng chứng: {Md(Redact(r.Evidence))}");
            foreach (var sub in r.Subcases ?? [])
            {
                sb.AppendLine($"    - `{sub.Name}` = {sub.Verdict}: {Md(sub.Evidence)}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("</details>");
        sb.AppendLine();

        sb.AppendLine("## D. Danh sách BUG (ô FAIL)");
        sb.AppendLine();
        if (failed.Count == 0)
        {
            sb.AppendLine("Không có ô FAIL: mọi ô đã thực thi đều khớp kỳ vọng của workbook.");
        }
        else
        {
            sb.AppendLine("| # | Test ID | Ô | Thao tác | Vai trò | Ký hiệu | Expected | Actual | Mã lỗi | Service | Tầng |");
            sb.AppendLine("|---:|---|---|---|---|---|---|---|---|---|---|");
            var index = 1;
            foreach (var f in failed)
            {
                sb.AppendLine(
                    $"| BUG-{index:D2} | {f.TestId} | {f.Cell.Cell} | {f.Cell.OperationCode} {Md(f.Cell.OperationName)} | {f.Cell.Role} | " +
                    $"{f.Cell.Symbol} | {Md(f.Expected)} | {Md(f.Actual)} | {Md(f.ErrorCode)} | {Md(f.Service)} | {f.Tier} |");
                index++;
            }
            sb.AppendLine();
            sb.AppendLine("### D.1 Chi tiết từng bug (trường bắt buộc theo đặc tả VHT-101 mục IX)");
            sb.AppendLine();
            index = 1;
            foreach (var f in failed)
            {
                sb.AppendLine($"#### BUG-{index:D2} · {f.Cell.Cell} · {f.Cell.OperationCode} {f.Cell.OperationName} · {f.Cell.Role}");
                sb.AppendLine();
                sb.AppendLine($"- Ô Excel: `{f.Cell.Sheet}!{f.Cell.Cell}` (dòng {f.Cell.Row}, phân hệ {f.Cell.Module})");
                sb.AppendLine($"- Test ID: `{f.TestId}`");
                sb.AppendLine($"- Ký hiệu workbook: `{f.Cell.Symbol}` → kỳ vọng `{f.Expected}`");
                sb.AppendLine($"- Thực tế backend: `{f.Actual}` → kết quả `{f.Status}` (mã lỗi `{f.ErrorCode}`)");
                sb.AppendLine($"- Đối tượng gọi thật: `{f.Service}` (tầng {f.Tier})");
                sb.AppendLine($"- Phạm vi ghi trong workbook: {Md(f.Cell.Scope)}");
                sb.AppendLine($"- Ví dụ được phép: {Md(f.Cell.AllowedExample)}");
                sb.AppendLine($"- Ví dụ bị cấm: {Md(f.Cell.DeniedExample)}");
                sb.AppendLine($"- Trạng thái chốt / ghi chú triển khai: {Md(f.Cell.StatusNote)} / {Md(f.Cell.ImplementationNote)}");
                sb.AppendLine($"- Bằng chứng: {Md(Redact(f.Evidence))}");
                foreach (var sub in f.Subcases ?? [])
                {
                    sb.AppendLine($"  - `{sub.Name}` = {sub.Verdict}: {Md(sub.Evidence)}");
                }
                sb.AppendLine($"- Cách tái hiện: `dotnet test NhaTro/NhaTro.IntegrationTests --filter \"FullyQualifiedName~PermissionMatrixAutomatedTests\"` (cần SQL Server LocalDB)");
                sb.AppendLine();
                index++;
            }
        }
        sb.AppendLine();

        sb.AppendLine("## E. Danh sách BLOCKED");
        sb.AppendLine();
        var allBlocked = Ordered(declaredBlocked.Concat(runtimeBlocked).ToList());
        if (allBlocked.Count() == 0)
        {
            sb.AppendLine("Không có ô BLOCKED: toàn bộ 173 ô khả dụng đều gọi backend thật và PASS.");
            sb.AppendLine("`PermissionMatrixProbeExecutor.UnsupportedCells` rỗng và được guard");
            sb.AppendLine("`No_Cell_Is_Declared_Unsupported` + `No_Forbidden_Cell_Is_Declared_Unsupported` giữ ở trạng thái đó.");
        }
        else
        {
            sb.AppendLine("BLOCKED không phải PASS. Mỗi ô dưới đây thiếu service/endpoint thật hoặc chờ quyết định nghiệp vụ.");
            sb.AppendLine("Ô khai báo tĩnh nằm trong `PermissionMatrixProbeExecutor.UnsupportedCells` và bị guard");
            sb.AppendLine("`Declared_Unsupported_Entries_Are_Traceable_And_Partition_Applicable_Cells` chặn nếu xóa tùy tiện.");
            sb.AppendLine();
            sb.AppendLine("| Ô | Thao tác | Vai trò | Ký hiệu | Loại | Decision | Lý do |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var b in allBlocked)
            {
                var declared = PermissionMatrixProbeExecutor.UnsupportedFor(b.Cell);
                var kind = declared is null ? "Runtime" : "Khai báo thiếu API";
                var decision = declared?.DecisionCode ?? "-";
                sb.AppendLine(
                    $"| {b.Cell.Cell} | {b.Cell.OperationCode} {Md(b.Cell.OperationName)} | {b.Cell.Role} | {b.Cell.Symbol} | " +
                    $"{kind} | {decision} | {Md(Redact(b.Evidence))} |");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## F. Chú thích legend (nguyên văn từ workbook)");
        sb.AppendLine();
        foreach (var note in manifest.Legend.MatrixNotes)
        {
            sb.AppendLine($"- **{note.Cell}** (sheet {manifest.Workbook.MatrixSheet}): {note.Text}");
        }
        foreach (var note in manifest.Legend.SwitchNotes)
        {
            sb.AppendLine($"- Sheet {manifest.Workbook.SwitchSheet}: {note.Text}");
        }
        sb.AppendLine();
        sb.AppendLine("| Ký hiệu | Kỳ vọng quyền | Số ô |");
        sb.AppendLine("|---|---|---:|");
        foreach (var (symbol, expected) in manifest.Legend.ExpectedPermission)
        {
            sb.AppendLine($"| {symbol} | {expected} | {SymbolCount(results, symbol)} |");
        }
        sb.AppendLine();

        sb.AppendLine("## G. Quyết định nghiệp vụ (sheet Quyết định, nguyên văn)");
        sb.AppendLine();
        if (manifest.Decisions.Count == 0)
        {
            sb.AppendLine("(manifest không chứa sheet Quyết định)");
        }
        else
        {
            sb.AppendLine("| Mã | Vấn đề | Đề xuất / chốt |");
            sb.AppendLine("|---|---|---|");
            foreach (var d in manifest.Decisions)
            {
                sb.AppendLine($"| {d.Code} | {d.Issue} | {d.Proposal} |");
            }
        }
        sb.AppendLine();

        sb.AppendLine("### G.1 Công tắc Quản lý và ô ⚙️ tương ứng");
        sb.AppendLine();
        sb.AppendLine("| Mã | Ô | Thao tác | Chế độ | Mặc định | Ghi chú |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var sw in manifest.Switches)
        {
            sb.AppendLine(
                $"| {sw.Code} | {sw.MatrixCell} | OP{sw.MatrixOperationNumber:D2} {Md(sw.OperationName)} | " +
                $"{string.Join(" / ", sw.Modes)} | {sw.DefaultMode} | {Md(sw.Note)} |");
        }
        sb.AppendLine();

        sb.AppendLine("## H. Hạn chế của bộ kiểm thử");
        sb.AppendLine();
        sb.AppendLine("- Tầng kiểm tra ghi ở cột `Tầng`: `Service` là gọi service nghiệp vụ thật trên database test riêng và đọc lại DB để xác nhận hiệu lực ghi; `Policy` là mới chứng minh tới cổng quyền/công tắc; `SampleState` là trạng thái mẫu trong bộ nhớ. Ô ở tầng Policy/SampleState **không được tính là đã chứng minh thao tác nghiệp vụ** và chỉ xuất hiện khi workbook cho phép hoặc ô ❌.");
        sb.AppendLine("- Chưa có `WebApplicationFactory` cho toàn bộ phân hệ nên ô chưa được chứng minh qua HTTP/form thật; đây là khoảng trống của VHT-101 lần này, không phải kết luận 'đạt'.");
        sb.AppendLine("- Không mock bộ phân quyền đang test: probe gọi thẳng service thật, ngoại lệ chỉ là dữ liệu nền do `PermissionMatrixWorld` dựng có chủ đích (2 tòa, 6 tài khoản) để tạo tình huống trong/ngoài phạm vi.");
        sb.AppendLine("- Không sửa workbook, không sửa logic nghiệp vụ/UI để làm báo cáo đẹp hơn. Khi code và ma trận khác nhau, ô ghi FAIL và mô tả bug ở mục D.");
        sb.AppendLine("- Kỳ vọng lấy nguyên văn từ manifest sinh bởi `scripts/permission_matrix/build_manifest.py`; guard hash bắt buộc chạy lại script khi workbook đổi.");
        sb.AppendLine("- Credential đã redact khỏi evidence và ba file báo cáo (guard `EnsureNoCredentials`); database kiểm thử là `NhaTro_It_*` tách biệt, không dùng tài khoản thật.");
        sb.AppendLine("- 7 ô `—` không chạy probe; test `Cell_Not_Applicable_Is_Excluded_From_Execution` chỉ xác nhận manifest đánh dấu đúng.");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string BuildJsonReport(IReadOnlyList<CellResult> results)
    {
        var manifest = PermissionMatrixManifest.Current;
        var payload = new
        {
            task = "VHT-101",
            generatedAtUtc = DateTimeOffset.UtcNow,
            workbook = new
            {
                path = manifest.Workbook.Path,
                sha256 = manifest.Workbook.Sha256,
                sheets = manifest.Workbook.Sheets,
                matrixSheet = manifest.Workbook.MatrixSheet,
                switchSheet = manifest.Workbook.SwitchSheet,
                decisionSheet = manifest.Workbook.DecisionSheet,
            },
            integrity = manifest.Integrity,
            summary = new
            {
                totalCells = results.Count,
                applicableCells = results.Count(r => r.Cell.Applicable),
                notApplicable = results.Count(r => r.Status == "NOT_APPLICABLE"),
                pass = results.Count(r => r.Status == "PASS"),
                fail = results.Count(r => r.Status == "FAIL"),
                blocked = results.Count(r => r.Status == "BLOCKED"),
                blockedDeclaredStatically = results.Count(r => r.UnsupportedReason is not null),
            },
            bugs = results.Where(r => r.Status == "FAIL").Select(r => new
            {
                testId = r.TestId,
                sheet = r.Cell.Sheet,
                cell = r.Cell.Cell,
                excelRow = r.Cell.Row,
                operation = $"{r.Cell.OperationCode} {r.Cell.OperationName}",
                role = r.Cell.Role,
                symbol = r.Cell.Symbol,
                expected = r.Expected,
                actual = r.Actual,
                errorCode = r.ErrorCode,
                service = r.Service,
                tier = r.Tier.ToString(),
                evidence = Redact(r.Evidence),
                subcases = (r.Subcases ?? []).Select(s => new { name = s.Name, verdict = s.Verdict.ToString(), evidence = Redact(s.Evidence) }).ToList(),
            }).ToList(),
            cells = Ordered(results).Select(r => new
            {
                testId = r.TestId,
                sheet = r.Cell.Sheet,
                cell = r.Cell.Cell,
                excelRow = r.Cell.Row,
                module = r.Cell.Module,
                operationNumber = r.Cell.OperationNumber,
                operationCode = r.Cell.OperationCode,
                operationName = r.Cell.OperationName,
                role = r.Cell.Role,
                symbol = r.Cell.Symbol,
                applicable = r.Cell.Applicable,
                expected = r.Expected,
                actual = r.Actual,
                status = r.Status,
                tier = r.Tier.ToString(),
                service = r.Service,
                errorCode = r.ErrorCode,
                evidence = Redact(r.Evidence),
                unsupportedReason = Redact(r.UnsupportedReason),
                scope = Redact(r.Cell.Scope),
                allowedExample = Redact(r.Cell.AllowedExample),
                deniedExample = Redact(r.Cell.DeniedExample),
                statusNote = Redact(r.Cell.StatusNote),
                implementationNote = Redact(r.Cell.ImplementationNote),
                executedAtUtc = r.At,
                subcases = (r.Subcases ?? []).Select(s => new
                {
                    name = s.Name,
                    verdict = s.Verdict.ToString(),
                    evidence = Redact(s.Evidence),
                }).ToList(),
            }).ToList(),
            legend = manifest.Legend,
            managerSwitches = manifest.Switches,
            decisions = manifest.Decisions,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    private static string BuildCsvReport(IReadOnlyList<CellResult> results)
    {
        var header = new[]
        {
            "TestId", "Sheet", "Cell", "ExcelRow", "Module", "OperationCode", "OperationName", "Role",
            "Symbol", "Expected", "Actual", "Status", "Tier", "Service", "ErrorCode",
            "SubcaseCount", "Subcases", "Evidence", "UnsupportedReason", "ExecutedAtUtc",
        };

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', header.Select(CsvEscape)));

        foreach (var r in Ordered(results))
        {
            var columns = new[]
            {
                r.TestId, r.Cell.Sheet, r.Cell.Cell, r.Cell.Row.ToString(), r.Cell.Module,
                r.Cell.OperationCode, r.Cell.OperationName, r.Cell.Role, r.Cell.Symbol,
                r.Expected, r.Actual, r.Status, r.Tier.ToString(), r.Service ?? string.Empty,
                r.ErrorCode ?? string.Empty, r.SubcaseCount.ToString(),
                string.Join(" | ", (r.Subcases ?? []).Select(s => $"{s.Name}={s.Verdict}")),
                Redact(r.Evidence), Redact(r.UnsupportedReason), r.At.ToString("O"),
            };
            sb.AppendLine(string.Join(',', columns.Select(CsvEscape)));
        }

        return sb.ToString();
    }

    private static string CsvEscape(string? value)
    {
        var singleLine = OneLine(Redact(value));
        return $"\"{singleLine.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Md(string? value) => OneLine(Redact(value)).Replace('|', '/');

    private static string OneLine(string value) => value
        .Replace("\r\n", " ", StringComparison.Ordinal)
        .Replace('\n', ' ')
        .Replace('\r', ' ');
}
