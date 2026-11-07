using System.Globalization;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.DTOs;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Audit.Services;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Services;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Features.Invoices.Services;
using Web.Features.Maintenance;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Services;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Enums;
using Web.Features.Settings;
using Web.Features.Settings.Enums;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.MockData;
using Web.Features.Utilities.Rules;
using Web.Features.Utilities.Services;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// Thực thi kiểm thử hành vi thực tế (tầng Service / HTTP) cho 180 ô ma trận phân quyền
/// docs/task/thao_tac_va_quyen.xlsx (45 thao tác x 4 vai trò).
/// </summary>
public static class PermissionMatrixProbeExecutor
{
    /// <summary>
    /// Ô KHÔNG THỂ chứng minh bằng service/HTTP thật trên code hiện có, kèm lý do truy vết được.
    /// Đây là khai báo tĩnh, không phải lối thoát lúc chạy: bộ guard bắt buộc mỗi ô trong danh sách
    /// này có lý do, và nếu code sau này thêm API thì ô phải được bỏ khỏi danh sách (guard fail khi
    /// số ô không còn khớp ma trận). Ô ở đây KHÔNG bao giờ được tính là PASS.
    /// </summary>
    public sealed record UnsupportedCell(int OperationNumber, string Role, string? DecisionCode, string Reason);

    private static readonly IReadOnlyList<UnsupportedCell> DeclaredUnsupported = [];

    public static bool IsDeclaredUnsupported(MatrixCell cell) =>
        DeclaredUnsupported.Any(u => u.OperationNumber == cell.OperationNumber && u.Role == cell.Role);

    public static UnsupportedCell? UnsupportedFor(MatrixCell cell) =>
        DeclaredUnsupported.FirstOrDefault(u => u.OperationNumber == cell.OperationNumber && u.Role == cell.Role);

    /// <summary>Danh sách ô không chứng minh được, dùng cho guard coverage và báo cáo.</summary>
    public static IReadOnlyList<UnsupportedCell> UnsupportedCells => DeclaredUnsupported;

    /// <summary>Service thật được gọi cho từng thao tác, phục vụ cột 'Service' của báo cáo bug.</summary>
    private static string ServiceNameFor(int operationNumber) => operationNumber switch
    {
        <= 7 => "Web.Features.Accounts.AccountService",
        8 => "Web.Features.Settings.ManagerToggleService",
        9 => "Web.Features.Approvals.ManagerApprovalService",
        10 => "Web.Features.Audit.Services.AuditLogService",
        <= 16 => "Web.Features.Rooms.RoomService",
        <= 21 => "Web.Features.Tenants.TenantService",
        <= 27 => "Web.Features.Contracts.Services.ContractService",
        <= 32 => "Web.Features.Utilities.Services.UtilityReadingService",
        33 => "Web.Features.Utilities.Services.ServiceCatalogService",
        <= 34 => "Web.Features.Invoices.InvoiceQueryService",
        <= 36 => "Web.Features.Invoices.InvoiceService",
        37 or 38 => "Web.Features.Payments.Services.PaymentService",
        39 => "Web.Features.Reports.Services.RevenueReportService",
        <= 43 => "Web.Features.Maintenance.IncidentService",
        44 => "Web.Features.Reports.RoomReportService",
        45 => "Web.Features.Reports.Services.ReportExportService",
        _ => "(không xác định)",
    };

    public static async Task<CellResult> ExecuteProbeAsync(MatrixCell cell, PermissionMatrixWorld world)
    {
        var testId = cell.TestId;
        var at = DateTimeOffset.UtcNow;
        var service = ServiceNameFor(cell.OperationNumber);

        if (!cell.Applicable || cell.Symbol == MatrixSymbol.NotApplicable)
        {
            return new CellResult(
                cell, "NOT_APPLICABLE", testId, ProbeTier.Service, cell.ExpectedPermission,
                "NOT_APPLICABLE", "NOT_APPLICABLE",
                $"Ô '{cell.Symbol}' — workbook đánh dấu không áp dụng (Mục VI.5)", null, at, [], service);
        }

        var unsupported = UnsupportedFor(cell);
        if (unsupported is not null)
        {
            return new CellResult(
                cell, cell.OperationCode, testId, ProbeTier.Service, cell.ExpectedPermission,
                "BLOCKED", "BLOCKED",
                unsupported.Reason, "NOT_VERIFIABLE", at, [], service, unsupported.Reason);
        }

        try
        {
            var outcome = await ProbeOperationAsync(cell, world);

            if (outcome.Verdict == ProbeVerdict.Blocked)
            {
                return new CellResult(
                    cell, cell.OperationCode, testId, outcome.Tier, cell.ExpectedPermission,
                    "BLOCKED", "BLOCKED", outcome.Evidence, "RUNTIME_BLOCKED", at, outcome.Cases, service,
                    outcome.Evidence);
            }

            var verdict = VerdictForCell(cell, outcome);

            // Mã lỗi được ghi cho MỌI phán quyết — kể cả PASS của ô ❌ — để chứng minh
            // ô bị từ chối bởi đúng tín hiệu phân quyền chứ không phải lỗi nghiệp vụ tình cờ.
            return new CellResult(
                cell, cell.OperationCode, testId, outcome.Tier, cell.ExpectedPermission,
                verdict.Actual, verdict.Status, verdict.Evidence,
                verdict.ErrorCode, at, outcome.Cases, service);
        }
        catch (Exception ex)
        {
            return new CellResult(
                cell, cell.OperationCode, testId, ProbeTier.Service, cell.ExpectedPermission,
                "ERROR", "FAIL", $"Lỗi không lường trước: {ex.GetType().Name}: {ex.Message}",
                ex.GetType().Name, at, [], service);
        }
    }

    /// <summary>
    /// Đối chiếu kết quả thật với kỳ vọng của ký hiệu ma trận.
    /// Nguyên tắc chống rửa điểm: ⚠️ bắt buộc có cả case được phép (trong phạm vi) và case bị từ chối
    /// (ngoài phạm vi); ⚙️ bắt buộc đủ mọi chế độ ghi trong sheet Công tắc Quản lý và mỗi chế độ phải
    /// cho đúng hiệu lực mà workbook mô tả; ❌ bắt buộc mọi tình huống đều bị từ chối.
    /// </summary>
    private static (string Status, string Actual, string Evidence, string? ErrorCode) VerdictForCell(
        MatrixCell cell, ProbeOutcome outcome)
    {
        var actual = outcome.Verdict switch
        {
            ProbeVerdict.PendingApproval => "PENDING_APPROVAL",
            _ => outcome.Verdict.ToString().ToUpperInvariant(),
        };

        // Lỗi không phải tín hiệu phân quyền không bao giờ được tính là "bị từ chối".
        if (outcome.Verdict == ProbeVerdict.Error)
        {
            return ("FAIL", "ERROR", outcome.Evidence, outcome.ErrorCode ?? "UNEXPECTED_ERROR");
        }

        return cell.Symbol switch
        {
            MatrixSymbol.Forbidden => MatchForbidden(outcome),
            MatrixSymbol.Allowed => MatchAllowed(cell, outcome),
            MatrixSymbol.Conditional => MatchConditional(outcome),
            MatrixSymbol.SpecialModes => MatchSpecialModes(cell, outcome),
            _ => ("FAIL", actual, $"Ký hiệu ma trận không hợp lệ: '{cell.Symbol}'", "UNKNOWN_SYMBOL"),
        };
    }

    private static (string, string, string, string?) MatchForbidden(ProbeOutcome outcome)
    {
        var leaked = outcome.Cases.Where(c => c.Verdict != ProbeVerdict.Denied).ToList();
        if (leaked.Count > 0)
        {
            var first = leaked[0];
            var detail = first.Verdict == ProbeVerdict.Error
                ? $"Ô ❌ nhưng gặp lỗi không phải tín hiệu phân quyền: {first.Evidence}"
                : $"Ô ❌ nhưng {first.Name} không bị từ chối: {first.Evidence}";
            return ("FAIL", first.Verdict.ToString().ToUpperInvariant(), detail, "AUTHZ_LEAK");
        }

        var denialCode = outcome.Cases.Select(c => c.ErrorCode)
            .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code)) ?? outcome.ErrorCode;
        return ("PASS", "DENIED", outcome.Evidence, denialCode);
    }

    private static (string, string, string, string?) MatchAllowed(MatrixCell cell, ProbeOutcome outcome)
    {
        // Workbook (chú thích A49 + ghi chú triển khai) cho phép một số ô ✅ có hiệu lực dạng
        // "Chờ duyệt" thay vì hiệu lực ngay (D3: Quản lý tạo hợp đồng luôn Chờ duyệt).
        var pendingIsDocumented =
            Contains(cell.AllowedExample, "Chờ duyệt") || Contains(cell.ImplementationNote, "Chờ duyệt");

        var bad = outcome.Cases.FirstOrDefault(c =>
            c.Verdict != ProbeVerdict.Allowed && !(pendingIsDocumented && c.Verdict == ProbeVerdict.PendingApproval));

        return bad is null
            ? ("PASS", outcome.Verdict == ProbeVerdict.PendingApproval ? "PENDING_APPROVAL" : "ALLOWED", outcome.Evidence, outcome.ErrorCode)
            : ("FAIL", bad.Verdict == ProbeVerdict.Denied ? "DENIED" : bad.Verdict.ToString().ToUpperInvariant(),
                $"Ô ✅ nhưng {bad.Name} không thực hiện được: {bad.Evidence}", "OPERATION_BLOCKED");
    }

    private static (string, string, string, string?) MatchConditional(ProbeOutcome outcome)
    {
        // Ô ⚠️ bắt buộc đúng chiều: case "A-..." phải Allowed (làm được trong phạm vi) và
        // case "B-..." phải Denied (bị chặn ngoài phạm vi). Chỉ cần "có một case Allowed và một
        // case Denied" sẽ cho PASS khi probe bị nối ngược tham số — tức là lúc hệ thống rò rỉ
        // dữ liệu chéo và lúc chủ sở hữu không xem được dữ liệu của mình.
        var inScope = outcome.Cases.FirstOrDefault(c => c.Name.StartsWith("A-", StringComparison.Ordinal));
        var outScope = outcome.Cases.FirstOrDefault(c => c.Name.StartsWith("B-", StringComparison.Ordinal));

        if (inScope is null || outScope is null)
        {
            return ("FAIL", outcome.Verdict.ToString().ToUpperInvariant(),
                $"Ô ⚠️ phải có đủ 2 tình huống (A-trong-pham-vi / B-ngoai-pham-vi); thực tế: {outcome.Evidence}",
                "SCOPE_CASES_MISSING");
        }

        if (inScope.Verdict != ProbeVerdict.Allowed)
        {
            return ("FAIL", "DENIED",
                $"Ô ⚠️ nhưng không làm được trong phạm vi: {inScope.Name}: {inScope.Evidence}", "IN_SCOPE_BLOCKED");
        }

        if (outScope.Verdict != ProbeVerdict.Denied)
        {
            return ("FAIL", "ALLOWED",
                $"Ô ⚠️ nhưng không bị chặn ngoài phạm vi (rò rỉ dữ liệu chéo): {outScope.Name}: {outScope.Evidence}",
                "AUTHZ_LEAK");
        }

        return ("PASS", "CONDITIONAL",
            $"Case A ({inScope.Name}): {inScope.Evidence} || Case B ({outScope.Name}): {outScope.Evidence}",
            outScope.ErrorCode ?? outcome.ErrorCode);
    }

    private static (string, string, string, string?) MatchSpecialModes(MatrixCell cell, ProbeOutcome outcome)
    {
        var sw = PermissionMatrixManifest.Switches.FirstOrDefault(s => s.MatrixCell == cell.Cell);
        if (sw is null)
        {
            return ("FAIL", "UNMAPPED", $"Ô ⚙️ {cell.Cell} không khớp công tắc nào trong sheet Công tắc Quản lý.", "SWITCH_NOT_MAPPED");
        }

        var documented = sw.Modes;
        var executed = outcome.Cases.Where(c => c.Name.StartsWith("mode:", StringComparison.Ordinal)).ToList();

        var missing = documented.FirstOrDefault(mode =>
            !executed.Any(c => string.Equals(c.Name, $"mode:{mode}", StringComparison.Ordinal)));
        if (missing is not null)
        {
            return ("FAIL", "MODES_INCOMPLETE",
                $"Công tắc {sw.Code} có {documented.Length} chế độ ({string.Join(", ", documented)}) theo workbook; thiếu chế độ '{missing}'.",
                "MODE_NOT_EXECUTED");
        }

        foreach (var mode in documented)
        {
            var subcase = executed.First(e => string.Equals(e.Name, $"mode:{mode}", StringComparison.Ordinal));
            if (MatchesModeSemantics(subcase.Verdict, mode)) continue;

            return ("FAIL", subcase.Verdict.ToString().ToUpperInvariant(),
                $"Công tắc {sw.Code} ở chế độ '{mode}' cho kết quả {subcase.Verdict}, " +
                $"không đúng hiệu lực workbook mô tả: {subcase.Evidence}", "MODE_SEMANTICS_MISMATCH");
        }

        return ("PASS", "SPECIAL_MODES",
            $"{sw.Code}: " + string.Join("; ", documented.Select(mode =>
            {
                var c = executed.First(e => string.Equals(e.Name, $"mode:{mode}", StringComparison.Ordinal));
                return $"{mode} -> {c.Verdict}";
            })),
            executed.Select(c => c.ErrorCode).FirstOrDefault(code => !string.IsNullOrWhiteSpace(code)));
    }

    /// <summary>Hiệu lực từng chế độ đúng chú thích A48 sheet Ma trận quyền + sheet Công tắc Quản lý.</summary>
    private static bool MatchesModeSemantics(ProbeVerdict verdict, string mode) => mode switch
    {
        "Cho phép" => verdict == ProbeVerdict.Allowed,
        "Duyệt" => verdict == ProbeVerdict.PendingApproval,
        "Không thể" => verdict == ProbeVerdict.Denied,
        _ => false,
    };

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static async Task<ProbeOutcome> ProbeOperationAsync(MatrixCell cell, PermissionMatrixWorld world)
    {
        var op = cell.OperationNumber;
        var role = cell.Role;
        var symbol = cell.Symbol;

        return op switch
        {
            1 => await ProbeOP01_AuthAsync(role, symbol, world),
            2 => await ProbeOP02_ViewMyProfileAsync(role, symbol, world),
            3 => await ProbeOP03_ViewOtherUsersAsync(role, symbol, world),
            4 => await ProbeOP04_UpdateMyProfileAsync(role, symbol, world),
            5 => await ProbeOP05_CreateAccountAsync(role, symbol, world),
            6 => await ProbeOP06_LockAccountAsync(role, symbol, world),
            7 => await ProbeOP07_ChangeRoleAsync(role, symbol, world),
            8 => await ProbeOP08_ConfigureManagerTogglesAsync(role, symbol, world),
            9 => await ProbeOP09_ApprovalQueueAsync(role, symbol, world),
            10 => await ProbeOP10_AuditLogAsync(role, symbol, world),

            11 => await ProbeOP11_ViewRoomsAsync(role, symbol, world),
            12 => await ProbeOP12_EditRoomInfoAsync(role, symbol, world),
            13 => await ProbeOP13_ChangeRoomPriceAsync(role, symbol, world),
            14 => await ProbeOP14_ChangeRoomStatusAsync(role, symbol, world),
            15 => await ProbeOP15_DeleteRoomAsync(role, symbol, world),
            16 => await ProbeOP16_RestoreRoomAsync(role, symbol, world),

            17 => await ProbeOP17_ViewTenantsAsync(role, symbol, world),
            18 => await ProbeOP18_CreateTenantAsync(role, symbol, world),
            19 => await ProbeOP19_EditTenantAsync(role, symbol, world),
            20 => await ProbeOP20_DeleteTenantAsync(role, symbol, world),
            21 => await ProbeOP21_RestoreTenantAsync(role, symbol, world),

            22 => await ProbeOP22_ViewContractAsync(role, symbol, world),
            23 => await ProbeOP23_CreateContractAsync(role, symbol, world),
            24 => await ProbeOP24_ApproveContractAsync(role, symbol, world),
            25 => await ProbeOP25_LiquidateContractAsync(role, symbol, world),
            26 => await ProbeOP26_ViewDepositAsync(role, symbol, world),
            27 => await ProbeOP27_UpdateDepositAsync(role, symbol, world),

            28 => await ProbeOP28_ViewUtilitiesAsync(role, symbol, world),
            29 => await ProbeOP29_InputMeterReadingAsync(role, symbol, world),
            31 => await ProbeOP31_EditPriorPeriodReadingAsync(role, symbol, world),
            32 => await ProbeOP32_ViewReadingHistoryAsync(role, symbol, world),
            33 => await ProbeOP33_SetupServiceCatalogAsync(role, symbol, world),

            34 => await ProbeOP34_ViewInvoiceAsync(role, symbol, world),
            35 => await ProbeOP35_CreateInvoiceAsync(role, symbol, world),
            36 => await ProbeOP36_IssueInvoiceAsync(role, symbol, world),
            37 => await ProbeOP37_RequestPaymentAsync(role, symbol, world),
            38 => await ProbeOP38_ConfirmPaymentAsync(role, symbol, world),
            39 => await ProbeOP39_ViewRevenueReportAsync(role, symbol, world),

            40 => await ProbeOP40_ViewIncidentsAsync(role, symbol, world),
            41 => await ProbeOP41_CreateIncidentAsync(role, symbol, world),
            42 => await ProbeOP42_UpdateIncidentStatusAsync(role, symbol, world),
            43 => await ProbeOP43_AttachResultPhotoAsync(role, symbol, world),

            44 => await ProbeOP44_ViewReportsAsync(role, symbol, world),
            45 => await ProbeOP45_ExportReportsAsync(role, symbol, world),

            _ => ProbeOutcome.Blocked($"Chưa cấu hình probe cho thao tác {op}"),
        };
    }

    // ==========================================
    // P1: TÀI KHOẢN VÀ XÁC THỰC (OP01 - OP10)
    // ==========================================

    private static async Task<ProbeOutcome> ProbeOP01_AuthAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        await using var db = world.NewDb();
        var userManager = world.NewUserManager(db);
        var user = await userManager.FindByIdAsync(caller.UserId);
        if (user is null)
        {
            return ProbeOutcome.Denied($"Tài khoản '{caller.UserId}' không tồn tại trong AspNetUsers");
        }

        // Đăng nhập thật: kiểm tra mật khẩu đã băm bằng chính Identity UserManager mà /Account/Login dùng.
        var passwordOk = await userManager.CheckPasswordAsync(user, PermissionMatrixWorld.AccountPassword);
        if (!passwordOk)
        {
            return ProbeOutcome.Denied("Không đăng nhập được: mật khẩu không khớp hash trong AspNetUsers");
        }

        if (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            return ProbeOutcome.Denied($"Đúng credential nhưng tài khoản đang bị khóa tới {user.LockoutEnd:yyyy-MM-dd HH:mm}");
        }

        // App thật còn chặn tài khoản chưa duyệt (VHT-95) sau bước kiểm tra mật khẩu.
        var approvedAt = await db.Users.Where(u => u.Id == caller.UserId).Select(u => u.ApprovedAt).SingleAsync();
        return approvedAt is null
            ? ProbeOutcome.Denied("Đúng credential nhưng tài khoản chưa được duyệt (ApprovedAt = null)")
            : ProbeOutcome.Allowed($"Đăng nhập thật thành công cho {role} (đúng mật khẩu đã băm, đã duyệt lúc {approvedAt:yyyy-MM-dd})");
    }

    private static async Task<ProbeOutcome> ProbeOP02_ViewMyProfileAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var service = world.NewAccounts();
        var res = await service.GetMyProfileAsync(caller.UserId);
        return res.Succeeded
            ? ProbeOutcome.Allowed($"Đọc hồ sơ cá nhân thành công: {res.Value?.FullName}")
            : ProbeOutcome.Denied(res.ErrorCode ?? "Không đọc được hồ sơ");
    }

    private static async Task<ProbeOutcome> ProbeOP03_ViewOtherUsersAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var service = world.NewAccounts();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var res = await service.ListAsync(null, null, 1, 10, caller.UserId, role);
            return res.TotalCount == 0
                ? ProbeOutcome.Denied("Bị từ chối xem danh sách người dùng khác (TotalCount = 0)")
                : ProbeOutcome.Allowed("Lỗi: Người dùng không được phép xem người khác");
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            if (role == Roles.Admin)
            {
                var listRes = await service.ListAsync(null, null, 1, 10, caller.UserId, role);
                var caseAPass = listRes.TotalCount > 0;

                var tenantService = world.NewTenants();
                bool caseBPass;
                try
                {
                    await tenantService.ListAsync(new TenantFilterRequest(), world.AdminTenant);
                    caseBPass = false;
                }
                catch (UnauthorizedAccessException)
                {
                    caseBPass = true;
                }

                return ProbeOutcome.ScopePair(caseAPass, caseBPass, "xem tài khoản");
            }

            if (role == Roles.QuanLy)
            {
                // Case A: khách có hợp đồng tại tòa được giao -> Quản lý tìm thấy, CCCD vẫn che (D9).
                var inScope = await service.SearchAssignableTenantsAsync("gán TK Nhà A", caller.UserId, role);
                var masked = inScope.Count > 0 && inScope.All(t => !string.IsNullOrEmpty(t.PhoneNumber));

                // Case B: khách của tòa khác -> không được rò rỉ dù trùng từ khóa tìm kiếm.
                var outScope = await service.SearchAssignableTenantsAsync("gán TK Nhà B", caller.UserId, role);

                return ProbeOutcome.Multi(
                    ProbeTier.Service,
                    new ProbeSubcase(
                        "A-trong-pham-vi",
                        masked ? ProbeVerdict.Allowed : ProbeVerdict.Denied,
                        masked
                            ? $"Quản lý xem {inScope.Count} khách trong tòa được giao, dữ liệu đã che theo D9"
                            : $"Quản lý không xem được khách trong tòa được giao (đọc {inScope.Count} dòng)"),
                    new ProbeSubcase(
                        "B-ngoai-pham-vi",
                        outScope.Count == 0 ? ProbeVerdict.Denied : ProbeVerdict.Allowed,
                        outScope.Count == 0
                            ? "Không trả về khách của tòa nhà khác được giao"
                            : $"RÒ RỲ: trả về {outScope.Count} khách ngoài phạm vi"));
            }
        }

        var chuTroList = await service.ListAsync(null, null, 1, 10, caller.UserId, role);
        return chuTroList.TotalCount > 0
            ? ProbeOutcome.Allowed($"Chủ trọ xem danh sách toàn bộ tài khoản ({chuTroList.TotalCount} users)")
            : ProbeOutcome.Denied("Chủ trọ không lấy được danh sách");
    }

    private static async Task<ProbeOutcome> ProbeOP04_UpdateMyProfileAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var service = world.NewAccounts();
        var req = new UpdateMyProfileRequest
        {
            FullName = $"{role} Test Modified",
            PhoneNumber = "0987654321",
        };
        var res = await service.UpdateMyProfileAsync(req, caller.UserId);
        return res.Succeeded
            ? ProbeOutcome.Allowed($"Vai trò {role} sửa hồ sơ cá nhân thành công")
            : ProbeOutcome.Denied(res.ErrorCode ?? "Lỗi sửa hồ sơ");
    }

    private static async Task<ProbeOutcome> ProbeOP05_CreateAccountAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var service = world.NewAccounts();
        var seq = NextSequence();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await UserCountAsync(world);
            var res = await service.CreateAsync(
                NewAccountRequest($"cam_{seq}", Roles.KhachThue, await NewTenantAsync(world, world.OwnerBTenant, seq)),
                caller.UserId, role);
            var after = await UserCountAsync(world);
            return !res.Succeeded && after == before
                ? ProbeOutcome.Denied($"Bị từ chối tạo tài khoản (ErrorCode={res.ErrorCode}), AspNetUsers không thêm dòng ({before} -> {after})")
                : ProbeOutcome.Allowed($"Lỗi: vai trò {role} vẫn tạo được tài khoản (Succeeded={res.Succeeded}, Users {before} -> {after})");
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            // Case A: hồ sơ khách thuộc Nhà A VÀ đã có hợp đồng tại tòa được giao — AccountService
            // chỉ xét phạm vi Quản lý qua thành viên hợp đồng (ScopedTenantIds) nên khách mới chưa
            // có hợp đồng sẽ bị OutOfScope, không chứng minh được ô ⚠️.
            var inScopeTenant = (await NewApprovedContractAsync(world, 6)).TenantId;
            var resInScope = await service.CreateAsync(
                NewAccountRequest($"in_{seq}", Roles.KhachThue, inScopeTenant), caller.UserId, role);

            // Case B: hồ sơ khách thuộc Nhà B (ngoài phạm vi) -> OutOfScope.
            var outScopeTenant = await NewTenantAsync(world, world.OwnerBTenant, seq + 1000);
            var resOutOfScope = await service.CreateAsync(
                NewAccountRequest($"out_{seq}", Roles.KhachThue, outScopeTenant), caller.UserId, role);

            return ProbeOutcome.Multi(
                ProbeTier.Service,
                new ProbeSubcase(
                    "A-trong-pham-vi",
                    resInScope.Succeeded ? ProbeVerdict.Allowed : ProbeVerdict.Denied,
                    resInScope.Succeeded
                        ? "Quản lý tạo tài khoản cho khách trong tòa được giao (tai khoản ở trạng thái chờ duyệt theo D1/A49)"
                        : $"KHÔNG tạo được trong phạm vi: ErrorCode={resInScope.ErrorCode}"),
                new ProbeSubcase(
                    "B-ngoai-pham-vi",
                    !resOutOfScope.Succeeded ? ProbeVerdict.Denied : ProbeVerdict.Allowed,
                    !resOutOfScope.Succeeded
                        ? $"Bị chặn ngoài phạm vi đúng: ErrorCode={resOutOfScope.ErrorCode}"
                        : "RÒ RỲ: Quản lý tạo được tài khoản cho khách của nhà trọ khác"));
        }

        // Admin / Chủ trọ: tạo tài khoản Quản lý (không gắn hồ sơ) và kiểm tra dòng thật trong DB.
        var usersBefore = await UserCountAsync(world);
        var req = NewAccountRequest($"mgr_{seq}", Roles.QuanLy, tenantId: null);
        var allowedRes = await service.CreateAsync(req, caller.UserId, role);
        var persisted = allowedRes.Succeeded && await UserExistsByEmailAsync(world, req.Email);
        var usersAfter = await UserCountAsync(world);

        return persisted && usersAfter > usersBefore
            ? ProbeOutcome.Allowed($"Tạo tài khoản '{req.Email}' có hiệu lực thật trong AspNetUsers ({usersBefore} -> {usersAfter})")
            : ProbeOutcome.Denied($"Không tạo được tài khoản (Succeeded={allowedRes.Succeeded}, ErrorCode={allowedRes.ErrorCode})");
    }

    private static CreateAccountRequest NewAccountRequest(string slug, string role, int? tenantId) => new()
    {
        Email = $"pm-{slug}@test.local",
        FullName = $"Tai khoan ma tran {slug}",
        PhoneNumber = "0900000000",
        Role = role,
        TenantId = tenantId,
        Password = "PmTest123!",
    };

    private static int _sequence;

    /// <summary>Số thứ tự cho dữ liệu probe, tránh phụ thuộc Guid.Random để test chạy lại được.</summary>
    private static int NextSequence() => Interlocked.Increment(ref _sequence);

    private static async Task<int> UserCountAsync(PermissionMatrixWorld world)
    {
        await using var db = world.NewDb();
        return await db.Users.AsNoTracking().CountAsync();
    }

    private static async Task<bool> UserExistsByEmailAsync(PermissionMatrixWorld world, string email)
    {
        await using var db = world.NewDb();
        return await db.Users.AsNoTracking().AnyAsync(u => u.Email == email);
    }

    /// <summary>Hồ sơ khách MỚI thuộc đúng chủ nhà, tránh lỗi TenantAlreadyLinked giữa các ô.</summary>
    private static async Task<int> NewTenantAsync(PermissionMatrixWorld world, TenantCaller owner, int seq)
    {
        var created = await world.NewTenants().CreateAsync(new CreateTenantRequest
        {
            FullName = $"Khach can tai khoan {seq}",
            PhoneNumber = $"091{seq:D7}",
            IdNumber = $"0799{seq:D8}",
        }, owner);
        if (!created.Succeeded || created.Tenant is null)
        {
            throw new InvalidOperationException($"Không tạo được hồ sơ khách cho probe OP05: {created.ErrorCode}");
        }

        return created.Tenant.Id;
    }

    private static async Task<ProbeOutcome> ProbeOP06_LockAccountAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var service = world.NewAccounts();
        var targetId = PermissionMatrixWorld.TenantUserAId;

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await IsLockedAsync(world, targetId);
            var res = await service.LockAsync(targetId, caller.UserId, role);
            var after = await IsLockedAsync(world, targetId);
            return !res.Succeeded && after == before
                ? ProbeOutcome.Denied($"Bị từ chối khóa tài khoản (ErrorCode={res.ErrorCode}), trạng thái khóa không đổi ({before} -> {after})")
                : ProbeOutcome.Allowed($"Lỗi: vai trò {role} vẫn khóa được tài khoản (Succeeded={res.Succeeded}, Locked {before} -> {after})");
        }

        // Ô ✅: khóa thật -> đọc DB xác nhận LockoutEnd tương lai -> mở khóa để khôi phục nền test.
        var lockRes = await service.LockAsync(targetId, caller.UserId, role);
        var lockedInDb = await IsLockedAsync(world, targetId);
        var unlockRes = await service.UnlockAsync(targetId, caller.UserId, role);
        var unlockedInDb = await IsLockedAsync(world, targetId);

        return lockRes.Succeeded && lockedInDb && unlockRes.Succeeded && !unlockedInDb
            ? ProbeOutcome.Allowed($"Vai trò {role} khóa rồi mở khóa có hiệu lực thật trong DB (LockoutEnd đổi đúng hai lần)")
            : ProbeOutcome.Denied(
                $"Khóa/mở khóa không có hiệu lực DB (lock={lockRes.Succeeded}/{lockRes.ErrorCode}, lockedInDb={lockedInDb}, unlock={unlockRes.Succeeded}/{unlockRes.ErrorCode})");
    }

    private static async Task<bool> IsLockedAsync(PermissionMatrixWorld world, string userId)
    {
        await using var db = world.NewDb();
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.LockoutEnabled && u.LockoutEnd > DateTime.UtcNow)
            .AnyAsync();
    }

    private static async Task<ProbeOutcome> ProbeOP07_ChangeRoleAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var service = world.NewAccounts();
        var seq = NextSequence();

        // Tài khoản đích MỚI (vai trò Quản lý) để mỗi ô chạy độc lập, không phá vai trò của tài khoản nền.
        var target = NewAccountRequest($"target_role_{seq}", Roles.QuanLy, tenantId: null);
        var created = await service.CreateAsync(target, world.AdminCaller.UserId, Roles.Admin);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException($"Không tạo được tài khoản đích cho probe OP07: {created.ErrorCode}");
        }

        var targetId = await UserIdByEmailAsync(world, target.Email);
        var before = await RolesOfAsync(world, targetId);

        if (symbol == MatrixSymbol.Forbidden)
        {
            var res = await service.ChangeRoleAsync(targetId, Roles.KhachThue, caller.UserId, role);
            var after = await RolesOfAsync(world, targetId);
            return !res.Succeeded && after == before
                ? ProbeOutcome.Denied($"Bị từ chối đổi vai trò (ErrorCode={res.ErrorCode}), vai trò trong DB không đổi ({after})")
                : ProbeOutcome.Allowed($"Lỗi: vai trò {role} vẫn đổi được role ({before} -> {after})");
        }

        // Ô ✅ Admin: đổi vai trò một tài khoản Quản lý (chưa gắn hồ sơ) sang Khách thuê, đọc lại AspNetUserRoles.
        var changeRes = await service.ChangeRoleAsync(targetId, Roles.KhachThue, caller.UserId, role);
        var changedTo = await RolesOfAsync(world, targetId);

        return changeRes.Succeeded && changedTo == Roles.KhachThue
            ? ProbeOutcome.Allowed($"Admin đổi vai trò có hiệu lực thật trong DB ({before} -> {changedTo})")
            : ProbeOutcome.Denied($"Đổi vai trò không có hiệu lực DB (Succeeded={changeRes.Succeeded}, ErrorCode={changeRes.ErrorCode}, DB={changedTo})");
    }

    private static async Task<string> UserIdByEmailAsync(PermissionMatrixWorld world, string email)
    {
        await using var db = world.NewDb();
        var id = await db.Users.AsNoTracking().Where(u => u.Email == email).Select(u => u.Id).SingleOrDefaultAsync();
        return id ?? throw new InvalidOperationException($"Không tìm thấy tài khoản '{email}' trong AspNetUsers");
    }

    private static async Task<string> RolesOfAsync(PermissionMatrixWorld world, string userId)
    {
        await using var db = world.NewDb();
        var names = await (from userRole in db.UserRoles
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where userRole.UserId == userId
                           select role.Name!).ToListAsync();
        return string.Join(",", names.OrderBy(n => n, StringComparer.Ordinal));
    }

    private static async Task<ProbeOutcome> ProbeOP08_ConfigureManagerTogglesAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var toggleCaller = new ManagerToggleCaller(caller.UserId, role);
        var service = world.NewToggles();

        if (symbol == MatrixSymbol.Forbidden)
        {
            // Prova chính thao tác ghi (đổi công tắc) chứ không chỉ đọc danh mục: ô ❌ của
            // "Cấu hình công tắc Quản lý" phải chứng minh vai trò bị cấm không ghi được.
            var res = await service.SetAsync("SW-01", ManagerToggleMode.Deny, toggleCaller);
            return !res.Succeeded
                ? ProbeOutcome.Denied($"Bị từ chối cấu hình công tắc Quản lý: {res.ErrorCode}")
                : ProbeOutcome.Allowed("Lỗi: Vai trò không được phép lại đổi được công tắc");
        }

        // Ô ✅: ghi thật một chế độ rồi đọc lại từ DB, sau đó khôi phục để không drift sang ô khác.
        var before = await world.GetSwitchAsync("SW-01");
        var target = before == ManagerToggleMode.Deny ? ManagerToggleMode.Allow : ManagerToggleMode.Deny;
        try
        {
            var written = await service.SetAsync("SW-01", target, toggleCaller);
            var after = await world.GetSwitchAsync("SW-01");

            return written.Succeeded && after == target
                ? ProbeOutcome.Allowed($"Chủ trọ ghi công tắc SW-01 = {target} có hiệu lực thật trong DB (trước đó {before})")
                : ProbeOutcome.Denied(
                    written.ErrorCode ?? $"Chủ trọ gọi ghi nhưng DB không đổi ({before} -> {after})");
        }
        finally
        {
            await world.SetSwitchAsync("SW-01", before);
        }
    }

    private static async Task<ProbeOutcome> ProbeOP09_ApprovalQueueAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var queueCaller = new ApprovalCaller(caller.UserId, role);
        var service = world.NewApprovals();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var res = await service.ListInboxAsync(queueCaller);
            return !res.Succeeded
                ? ProbeOutcome.Denied($"Bị từ chối truy cập hàng đợi phê duyệt: {res.ErrorCode}")
                : ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại truy cập được hàng đợi");
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            var list = await service.ListMineAsync(queueCaller);
            var caseAPass = list.Succeeded;

            var inboxRes = await service.ListInboxAsync(queueCaller);
            var caseBPass = !inboxRes.Succeeded;

            return ProbeOutcome.ScopePair(caseAPass, caseBPass, "xem yêu cầu của mình");
        }

        var chuTroList = await service.ListInboxAsync(queueCaller);
        return chuTroList.Succeeded
            ? ProbeOutcome.Allowed("Chủ trọ có toàn quyền xem và phê duyệt hàng đợi yêu cầu")
            : ProbeOutcome.Denied(chuTroList.ErrorCode ?? "Lỗi truy cập hàng đợi");
    }

    private static async Task<ProbeOutcome> ProbeOP10_AuditLogAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var audit = world.NewAuditLogs();

        try
        {
            if (role == Roles.Admin)
            {
                var rows = await audit.QueryAsync(caller);
                var valid = rows.Count > 0
                    && rows.All(row => row.Category == "Account")
                    && rows.All(row => row.PropertyId is null);
                return valid
                    ? ProbeOutcome.Allowed($"Admin đọc {rows.Count} dòng nhật ký hệ thống, không lẫn sổ nghiệp vụ.")
                    : ProbeOutcome.Denied(
                        "Lỗi: Admin không đọc được nhật ký hệ thống hoặc thấy lẫn sổ nghiệp vụ.");
            }

            if (role == Roles.ChuTro)
            {
                var rows = await audit.QueryAsync(caller);
                var valid = rows.Count > 0
                    && rows.All(row => row.Category == "Business")
                    && rows.All(row => row.PropertyId == world.Data.PropertyAId);
                return valid
                    ? ProbeOutcome.Allowed(
                        $"Chủ trọ đọc {rows.Count} dòng nhật ký nghiệp vụ trong đúng nhà A.")
                    : ProbeOutcome.Denied(
                        "Lỗi: Chủ trọ không đọc được nhật ký nghiệp vụ trong phạm vi của mình.");
            }

            var forbidden = await audit.QueryAsync(caller);
            return ProbeOutcome.Allowed(
                $"Lỗi: vai trò {role} vẫn đọc được {forbidden.Count} dòng nhật ký.");
        }
        catch (AuditLogAuthorizationException ex)
        {
            return ProbeOutcome.Denied(
                $"Bị từ chối đọc nhật ký: ErrorCode={ex.ErrorCode}: {ex.Message}",
                errorCode: ex.ErrorCode);
        }
    }

    // ==========================================
    // P2: PHÒNG VÀ TÒA NHÀ (OP11 - OP16)
    // ==========================================

    private static async Task<ProbeOutcome> ProbeOP11_ViewRoomsAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var rooms = world.NewRooms();

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await rooms.ListAsync(new RoomFilter(), caller);
                return ProbeOutcome.Allowed("Lỗi: Admin không được xem phòng nghiệp vụ P2 nhưng gọi thành công");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ProbeOutcome.Denied($"Admin bị từ chối xem phòng nghiệp vụ (D1 BusinessGuard): {ex.Message}");
            }
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            // Case A: khách xem được phòng mình đang thuê qua lối đọc dành cho khách (không phải danh mục chung).
            var ownRental = await world.NewTenants().GetOwnRentalOverviewAsync(world.TenantCallerFor(role));
            var caseA = ownRental.Succeeded && ownRental.Rental is not null
                ? new ProbeSubcase("A-phong-cua-minh", ProbeVerdict.Allowed, "Đọc được phòng đang thuê trong hợp đồng")
                : new ProbeSubcase("A-phong-cua-minh", ProbeVerdict.Denied, ownRental.ErrorCode ?? "Không đọc được phòng đang thuê");

            // Case B: khách không được gọi danh mục phòng toàn hệ thống.
            ProbeSubcase caseB;
            try
            {
                var leaked = await rooms.ListAsync(new RoomFilter(), caller);
                caseB = leaked.TotalCount > 0
                    ? new ProbeSubcase("B-danh-muc-chung", ProbeVerdict.Allowed, $"Lộ {leaked.TotalCount} phòng ngoài phạm vi")
                    : new ProbeSubcase("B-danh-muc-chung", ProbeVerdict.Denied, "Danh mục chung trả về 0 phòng");
            }
            catch (UnauthorizedAccessException ex)
            {
                caseB = new ProbeSubcase("B-danh-muc-chung", ProbeVerdict.Denied, $"Bị từ chối đọc danh mục phòng chung: {ex.GetType().Name}");
            }

            return ProbeOutcome.Multi(ProbeTier.Service, caseA, caseB);
        }

        if (role == Roles.QuanLy)
        {
            // Ô ✅: với Quản lý, "được xem" là xem đúng phạm vi được giao. Một tình huống duy nhất
            // (ScopePair sẽ tạo subcase Denied và ô ✅ bị đọc nhầm thành không thực hiện được), còn
            // bằng chứng vẫn nêu cả hai phía để thấy không rò rỉ nhà khác.
            var listA = await rooms.ListAsync(new RoomFilter { PropertyId = world.Data.PropertyAId }, caller);
            var listB = await rooms.ListAsync(new RoomFilter { PropertyId = world.Data.PropertyBId }, caller);

            return listA.Items.Count > 0 && listB.Items.Count == 0
                ? ProbeOutcome.Allowed(
                    $"Quản lý xem được {listA.Items.Count} phòng của nhà A được giao và không thấy phòng nhà B ({listB.Items.Count} phòng)")
                : listA.Items.Count == 0
                    ? ProbeOutcome.Denied("Quản lý không xem được phòng của tòa nhà được giao")
                    : ProbeOutcome.Denied($"Lỗi: Quản lý thấy {listB.Items.Count} phòng ngoài phạm vi được giao");
        }

        var list = await rooms.ListAsync(new RoomFilter(), caller);
        return list.TotalCount >= 1
            ? ProbeOutcome.Allowed($"Chủ trọ xem toàn bộ danh mục phòng ({list.TotalCount} phòng)")
            : ProbeOutcome.Denied("Không xem được phòng trong phạm vi");
    }

    private static async Task<ProbeOutcome> ProbeOP12_EditRoomInfoAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var rooms = world.NewRooms();
        var original = await RoomStateAsync(world, world.Data.RoomAId);
        var updateDto = new RoomSave
        {
            PropertyId = world.Data.PropertyAId,
            Code = "A101",
            StandardPrice = 1_500_000m,
            AreaM2 = 25m,
        };

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await rooms.UpdateAsync(world.Data.RoomAId, updateDto, caller);
                return ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại sửa được thông tin phòng");
            }
            catch (UnauthorizedAccessException ex)
            {
                var unchanged = string.Equals(await RoomStateAsync(world, world.Data.RoomAId), original, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối sửa phòng và dữ liệu không đổi: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối sửa phòng nhưng dữ liệu vẫn bị ghi thay đổi");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // SW-01 chỉ được quyết định bởi chính EditInfo: mỗi chế độ dùng một phòng MỚI và DTO
            // sao chép đúng giá/trạng thái đang sống, chỉ đổi tầng (RoomService.ChangedKinds).
            return await ProbeSwitchModesAsync(world, "SW-01", async () =>
            {
                var probeRoom = await NewProbeRoomAsync(world, "SW01");
                return new ModeProbe(
                    async () =>
                    {
                        var live = await ReadRoomSnapshotAsync(world, probeRoom.Id);
                        var dto = ToSave(live);
                        dto.Floor = live.Floor == 3 ? 4 : 3;
                        return await AttemptAsync(
                            async () =>
                            {
                                var res = await rooms.UpdateAsync(live.Id, dto, caller);
                                return (res.Succeeded, res.ErrorCode);
                            },
                            $"Quản lý sửa thông tin phòng {live.Code} (chỉ đổi tầng) có hiệu lực ngay");
                    },
                    () => RoomStateAsync(world, probeRoom.Id));
            });
        }

        var update = await rooms.UpdateAsync(world.Data.RoomAId, updateDto, caller);
        return update.Succeeded
            ? ProbeOutcome.Allowed("Chủ trọ sửa thông tin phòng thành công trực tiếp")
            : ProbeOutcome.Denied(update.ErrorMessage ?? "Lỗi sửa phòng");
    }

    private static async Task<ProbeOutcome> ProbeOP13_ChangeRoomPriceAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var rooms = world.NewRooms();
        var updateDto = new RoomSave
        {
            PropertyId = world.Data.PropertyAId,
            Code = "A101",
            StandardPrice = 1_700_000m,
        };

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await RoomStateAsync(world, world.Data.RoomAId);
            try
            {
                await rooms.UpdateAsync(world.Data.RoomAId, updateDto, caller);
                return ProbeOutcome.Allowed("Lỗi: Bị cấm lại đổi được giá phòng");
            }
            catch (UnauthorizedAccessException ex)
            {
                var unchanged = string.Equals(await RoomStateAsync(world, world.Data.RoomAId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối thiết lập giá phòng, dữ liệu không đổi: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối đổi giá nhưng giá phòng vẫn bị ghi");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // SW-02 chỉ đổi giá: các cột khác giữ nguyên giá trị đang sống để không lọt sang SW-01/SW-03.
            return await ProbeSwitchModesAsync(world, "SW-02", async () =>
            {
                var probeRoom = await NewProbeRoomAsync(world, "SW02");
                return new ModeProbe(
                    async () =>
                    {
                        var live = await ReadRoomSnapshotAsync(world, probeRoom.Id);
                        var dto = ToSave(live);
                        dto.StandardPrice = live.StandardPrice + 100_000m;
                        return await AttemptAsync(
                            async () =>
                            {
                                var res = await rooms.UpdateAsync(live.Id, dto, caller);
                                return (res.Succeeded, res.ErrorCode);
                            },
                            $"Quản lý đổi giá phòng {live.Code} có hiệu lực ngay");
                    },
                    () => RoomStateAsync(world, probeRoom.Id));
            });
        }

        var res = await rooms.UpdateAsync(world.Data.RoomAId, updateDto, caller);
        return res.Succeeded
            ? ProbeOutcome.Allowed("Chủ trọ đổi giá phòng thành công")
            : ProbeOutcome.Denied(res.ErrorMessage ?? "Lỗi đổi giá phòng");
    }

    private static async Task<ProbeOutcome> ProbeOP14_ChangeRoomStatusAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var rooms = world.NewRooms();
        var save = new RoomSave
        {
            PropertyId = world.Data.PropertyAId,
            Code = "A101",
            StandardPrice = 1_500_000m,
            Status = RoomStatus.Maintenance,
        };

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await RoomStateAsync(world, world.Data.RoomAId);
            try
            {
                await rooms.UpdateAsync(world.Data.RoomAId, save, caller);
                return ProbeOutcome.Allowed("Lỗi: Bị cấm lại đổi được trạng thái phòng");
            }
            catch (UnauthorizedAccessException ex)
            {
                var unchanged = string.Equals(await RoomStateAsync(world, world.Data.RoomAId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối đổi trạng thái phòng, dữ liệu không đổi: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối nhưng trạng thái phòng vẫn bị đổi");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // SW-03 chỉ đổi trạng thái: giá/tầng/diện tích giữ nguyên như đang sống.
            return await ProbeSwitchModesAsync(world, "SW-03", async () =>
            {
                var probeRoom = await NewProbeRoomAsync(world, "SW03");
                return new ModeProbe(
                    async () =>
                    {
                        var live = await ReadRoomSnapshotAsync(world, probeRoom.Id);
                        var dto = ToSave(live);
                        dto.Status = live.Status == RoomStatus.Available
                            ? RoomStatus.Maintenance
                            : RoomStatus.Available;
                        return await AttemptAsync(
                            async () =>
                            {
                                var res = await rooms.UpdateAsync(live.Id, dto, caller);
                                return (res.Succeeded, res.ErrorCode);
                            },
                            $"Quản lý đổi trạng thái phòng {live.Code} có hiệu lực ngay");
                    },
                    () => RoomStateAsync(world, probeRoom.Id));
            });
        }

        var okRes = await rooms.UpdateAsync(world.Data.RoomAId, save, caller);
        return okRes.Succeeded
            ? ProbeOutcome.Allowed("Chủ trọ đổi trạng thái phòng thành công")
            : ProbeOutcome.Denied(okRes.ErrorMessage ?? "Lỗi đổi trạng thái");
    }

    private static async Task<ProbeOutcome> ProbeOP15_DeleteRoomAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var rooms = world.NewRooms();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await RoomStateAsync(world, world.Data.RoomSpareId);
            try
            {
                var res = await rooms.DeleteAsync(world.Data.RoomSpareId, caller);
                return res.Succeeded
                    ? ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại xóa được phòng")
                    : ProbeOutcome.Denied($"Bị từ chối xóa phòng (ErrorCode={res.ErrorCode}), dữ liệu không đổi: {before}");
            }
            catch (UnauthorizedAccessException ex)
            {
                var unchanged = string.Equals(await RoomStateAsync(world, world.Data.RoomSpareId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối đưa phòng vào thùng rác: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối nhưng phòng vẫn bị xóa");
            }
        }

        // Ô ✅ Chủ trọ: xóa thật phòng dự phòng A102, đọc lại DB để chắc chắn hiệu lực, rồi khôi phục nền.
        var deleteRes = await rooms.DeleteAsync(world.Data.RoomSpareId, caller);
        var deletedInDb = await IsRoomDeletedAsync(world, world.Data.RoomSpareId);
        var restoreRes = await rooms.RestoreAsync(world.Data.RoomSpareId, caller);

        return deleteRes.Succeeded && deletedInDb
            ? ProbeOutcome.Allowed($"Chủ trọ đưa phòng vào thùng rác có hiệu lực thật (IsDeleted=true); khôi phục nền: {restoreRes.Succeeded}")
            : ProbeOutcome.Denied($"Xóa phòng không tạo hiệu lực DB (Succeeded={deleteRes.Succeeded}, IsDeleted={deletedInDb}, {deleteRes.ErrorMessage})");
    }

    private static async Task<ProbeOutcome> ProbeOP16_RestoreRoomAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var rooms = world.NewRooms();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await RoomStateAsync(world, world.Data.RoomSpareId);
            try
            {
                await rooms.RestoreAsync(world.Data.RoomSpareId, caller);
                return ProbeOutcome.Allowed("Lỗi: Bị cấm lại khôi phục được phòng");
            }
            catch (UnauthorizedAccessException ex)
            {
                var unchanged = string.Equals(await RoomStateAsync(world, world.Data.RoomSpareId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối khôi phục phòng từ thùng rác: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối nhưng phòng vẫn bị khôi phục");
            }
        }

        // Ô ✅ Chủ trọ: xóa rồi khôi phục thật trên phòng dự phòng, kiểm tra IsDeleted trở lại false.
        await rooms.DeleteAsync(world.Data.RoomSpareId, caller);
        var restoreRes = await rooms.RestoreAsync(world.Data.RoomSpareId, caller);
        var aliveInDb = !await IsRoomDeletedAsync(world, world.Data.RoomSpareId);

        return restoreRes.Succeeded && aliveInDb
            ? ProbeOutcome.Allowed("Chủ trọ khôi phục phòng từ thùng rác có hiệu lực thật (IsDeleted=false)")
            : ProbeOutcome.Denied($"Khôi phục không tạo hiệu lực DB (Succeeded={restoreRes.Succeeded}, còn trong thùng rác={await IsRoomDeletedAsync(world, world.Data.RoomSpareId)})");
    }

    // ==========================================
    // P3: KHÁCH THUÊ VÀ HỒ SƠ (OP17 - OP21)
    // ==========================================

    private static async Task<ProbeOutcome> ProbeOP17_ViewTenantsAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.TenantCallerFor(role);
        var tenants = world.NewTenants();

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await tenants.ListAsync(new TenantFilterRequest(), caller);
                return ProbeOutcome.Allowed("Lỗi: Admin bị cấm nhưng lại đọc được hồ sơ khách");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ProbeOutcome.Denied($"Admin bị từ chối truy cập hồ sơ khách thuê (D1 BusinessGuard): {ex.Message}");
            }
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            // Case A: khách chỉ xem được hồ sơ của CHÍNH mình (GetOwnAsync trên tài khoản đã liên kết).
            var own = await tenants.GetOwnAsync(caller);
            var caseA = own.Succeeded && own.Tenant is not null
                ? new ProbeSubcase("A-trong-pham-vi", ProbeVerdict.Allowed, $"Xem được hồ sơ của mình (TenantId={own.Tenant.Id})")
                : new ProbeSubcase("A-trong-pham-vi", ProbeVerdict.Denied, own.ErrorCode ?? "Không đọc được hồ sơ của mình");

            // Case B: khách không được xem danh sách hồ sơ khách khác.
            ProbeSubcase caseB;
            try
            {
                var list = await tenants.ListAsync(new TenantFilterRequest(), caller);
                var leaksOthers = list.Items.Any(t => t.Id != world.Data.TenantAId);
                caseB = leaksOthers
                    ? new ProbeSubcase("B-ngoai-pham-vi", ProbeVerdict.Allowed, $"Lộ {list.Items.Count} hồ sơ khách khác")
                    : new ProbeSubcase("B-ngoai-pham-vi", ProbeVerdict.Denied, $"Danh sách trả về {list.Items.Count} hồ sơ (không có hồ sơ khách khác)");
            }
            catch (UnauthorizedAccessException ex)
            {
                caseB = new ProbeSubcase("B-ngoai-pham-vi", ProbeVerdict.Denied, $"Bị từ chối đọc danh sách khách: {ex.GetType().Name}");
            }

            return ProbeOutcome.Multi(ProbeTier.Service, caseA, caseB);
        }

        if (role == Roles.QuanLy)
        {
            // Ô ✅: với Quản lý, "được xem" nghĩa là xem đúng phạm vi được giao. Dùng một tình huống
            // duy nhất như OP11 vì ScopePair sinh subcase Denied và ô ✅ bị đọc nhầm thành không làm được.
            var list = await tenants.ListAsync(new TenantFilterRequest(), caller);
            var seesOwn = list.Items.Any(t => t.Id == world.Data.TenantAId);
            var seesOtherHouse = list.Items.Any(t => t.Id == world.Data.TenantBId);

            if (seesOtherHouse)
            {
                return ProbeOutcome.Denied("Lỗi: Quản lý thấy hồ sơ khách của nhà B không được giao");
            }

            return seesOwn
                ? ProbeOutcome.Allowed(
                    $"Quản lý xem được {list.Items.Count} hồ sơ khách của nhà A được giao và không thấy khách nhà B")
                : ProbeOutcome.Denied("Quản lý không xem được hồ sơ khách của tòa nhà được giao");
        }

        var allList = await tenants.ListAsync(new TenantFilterRequest(), caller);
        return allList.TotalCount >= 1
            ? ProbeOutcome.Allowed($"Chủ trọ xem danh sách toàn bộ khách thuê ({allList.TotalCount} khách)")
            : ProbeOutcome.Denied("Không xem được danh sách khách thuê");
    }

    private static async Task<ProbeOutcome> ProbeOP18_CreateTenantAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.TenantCallerFor(role);
        var tenants = world.NewTenants();
        var req = new CreateTenantRequest
        {
            FullName = "Khach Thue Mới",
            PhoneNumber = "0909000111",
            IdNumber = "0799000111222",
        };

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await tenants.CreateAsync(req, caller);
                return ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại tạo được hồ sơ khách");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ProbeOutcome.Denied($"Bị từ chối tạo hồ sơ khách thuê: {ex.Message}");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            var seq = 0;
            return await ProbeSwitchModesAsync(
                world, "SW-04",
                () => AttemptAsync(async () =>
                {
                    seq++;
                    var res = await tenants.CreateAsync(new CreateTenantRequest
                    {
                        FullName = $"Khach QLSwitch {seq}",
                        PhoneNumber = $"090000{seq:D4}",
                        IdNumber = $"0795000000{seq:D2}",
                    }, caller);
                    return (res.Succeeded, res.ErrorCode);
                }, "Quản lý tạo hồ sơ khách có hiệu lực ngay"),
                () => CountAsync(world));
        }

        var res = await tenants.CreateAsync(req, caller);
        return res.Succeeded
            ? ProbeOutcome.Allowed($"Chủ trọ tạo hồ sơ khách thuê thành công (TenantId={res.Tenant?.Id})")
            : ProbeOutcome.Denied(res.ErrorCode ?? "Lỗi tạo khách thuê");
    }

    private static async Task<ProbeOutcome> ProbeOP19_EditTenantAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.TenantCallerFor(role);
        var tenants = world.NewTenants();

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await tenants.UpdateAsync(world.Data.TenantAId, new UpdateTenantRequest
                {
                    FullName = "Khach A Modified",
                    PhoneNumber = "0911223344",
                    IdNumber = "079201000001",
                }, caller);
                return ProbeOutcome.Allowed("Lỗi: Admin bị cấm lại sửa được hồ sơ khách");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ProbeOutcome.Denied($"Admin bị từ chối sửa hồ sơ khách thuê: {ex.Message}");
            }
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            var ownProfile = await tenants.GetOwnAsync(caller);
            var rowVersion = ownProfile.Tenant?.RowVersion ?? string.Empty;
            var ownRes = await tenants.UpdateOwnAsync(new UpdateOwnProfileRequest
            {
                PhoneNumber = "0988776655",
                RowVersion = rowVersion,
            }, caller);
            var caseAPass = ownRes.Succeeded;

            bool caseBPass;
            try
            {
                var otherRes = await tenants.UpdateAsync(world.Data.TenantBId, new UpdateTenantRequest
                {
                    FullName = "Hacked Other Tenant",
                    PhoneNumber = "0999999999",
                    IdNumber = "079201000002",
                }, caller);
                caseBPass = !otherRes.Succeeded;
            }
            catch (UnauthorizedAccessException)
            {
                caseBPass = true;
            }

            return ProbeOutcome.ScopePair(caseAPass, caseBPass, "sửa hồ sơ mình");
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            var rowVersion = await TenantRowVersionAsync(world, world.Data.TenantAId);
            return await ProbeSwitchModesAsync(
                world, "SW-05",
                () => AttemptAsync(async () =>
                {
                    var res = await tenants.UpdateAsync(world.Data.TenantAId, new UpdateTenantRequest
                    {
                        FullName = "Khach A Switch Edit",
                        PhoneNumber = "0900112233",
                        IdNumber = "079201000001",
                        RowVersion = rowVersion,
                    }, caller);
                    return (res.Succeeded, res.ErrorCode);
                }, "Quản lý sửa hồ sơ khách có hiệu lực ngay"),
                () => TenantStateAsync(world, world.Data.TenantAId));
        }

        var existing = await tenants.GetAsync(world.Data.TenantAId, caller);
        var updateRes = await tenants.UpdateAsync(world.Data.TenantAId, new UpdateTenantRequest
        {
            FullName = "Khach A Master Update",
            PhoneNumber = "0900112233",
            IdNumber = "079201000001",
            RowVersion = existing.Tenant?.RowVersion ?? string.Empty,
        }, caller);

        var applied = await TenantStateAsync(world, world.Data.TenantAId);
        return updateRes.Succeeded && applied.Contains("Khach A Master Update", StringComparison.Ordinal)
            ? ProbeOutcome.Allowed($"Chủ trọ sửa hồ sơ khách có hiệu lực thật trong DB ({applied})")
            : ProbeOutcome.Denied($"Sửa hồ sơ không tạo hiệu lực DB (Succeeded={updateRes.Succeeded}, trạng thái: {applied})");
    }

    /// <summary>
    /// RowVersion hiện hành của hồ sơ khách, đọc thẳng DB kể cả khi hồ sơ đã nằm trong thùng rác
    /// (bản đồ <c>TenantMapping</c> cũng sinh token bằng Base64 của cột rowversion này).
    /// </summary>
    private static async Task<string> TenantRowVersionRawAsync(PermissionMatrixWorld world, int tenantId)
    {
        await using var db = world.NewDb();
        var bytes = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.RowVersion)
            .FirstAsync();
        return Convert.ToBase64String(bytes);
    }

    private static async Task<ProbeOutcome> ProbeOP20_DeleteTenantAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.TenantCallerFor(role);
        var tenants = world.NewTenants();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var tenantId = await NewTenantAsync(world, world.OwnerATenant, NextSequence());
            var before = await TenantStateAsync(world, tenantId);
            try
            {
                var res = await tenants.DeleteAsync(tenantId, await TenantRowVersionRawAsync(world, tenantId), caller);
                return res.Succeeded && await IsTenantTrashedAsync(world, tenantId)
                    ? ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại xóa được khách thuê")
                    : ProbeOutcome.Denied($"Bị từ chối xóa hồ sơ khách (ErrorCode={res.ErrorCode}), dữ liệu không đổi: {before}");
            }
            catch (UnauthorizedAccessException ex)
            {
                var unchanged = string.Equals(await TenantStateAsync(world, tenantId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối đưa hồ sơ khách vào thùng rác: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối nhưng hồ sơ khách vẫn bị xóa");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // Xóa mềm làm đổi IsDeleted thật, nên mỗi chế độ công tắc phải có một hồ sơ khách MỚI.
            return await ProbeSwitchModesAsync(world, "SW-06", async () =>
            {
                var tenantId = await NewTenantAsync(world, world.OwnerATenant, NextSequence());
                var version = await TenantRowVersionRawAsync(world, tenantId);
                return new ModeProbe(
                    () => AttemptAsync(
                        async () =>
                        {
                            var res = await tenants.DeleteAsync(tenantId, version, caller);
                            return (res.Succeeded, res.ErrorCode);
                        },
                        "Quản lý xóa hồ sơ khách có hiệu lực ngay"),
                    () => TenantStateAsync(world, tenantId));
            });
        }

        // Ô ✅ Chủ trọ: xóa thật một hồ sơ riêng, đọc lại DB xác nhận nằm trong thùng rác.
        var target = await NewTenantAsync(world, world.OwnerATenant, NextSequence());
        var liveVersion = await TenantRowVersionRawAsync(world, target);
        var deleteRes = await tenants.DeleteAsync(target, liveVersion, caller);
        var state = await TenantStateAsync(world, target);

        return deleteRes.Succeeded && await IsTenantTrashedAsync(world, target)
            ? ProbeOutcome.Allowed($"Chủ trọ đưa hồ sơ khách vào thùng rác có hiệu lực thật ({state})")
            : ProbeOutcome.Denied($"Xóa hồ sơ không có hiệu lực DB (Succeeded={deleteRes.Succeeded}, {deleteRes.ErrorCode}, trạng thái: {state})");
    }

    private static async Task<ProbeOutcome> ProbeOP21_RestoreTenantAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.TenantCallerFor(role);
        var tenants = world.NewTenants();

        if (symbol == MatrixSymbol.Forbidden)
        {
            // Hồ sơ phải thực sự nằm trong thùng rác thì quyền khôi phục mới là thứ được đem ra xét.
            var trashedId = await NewTrashedTenantAsync(world);
            var before = await TenantStateAsync(world, trashedId);
            try
            {
                var res = await tenants.RestoreAsync(
                    trashedId, await TenantRowVersionRawAsync(world, trashedId), caller);
                return res.Succeeded && !await IsTenantTrashedAsync(world, trashedId)
                    ? ProbeOutcome.Allowed("Lỗi: Bị cấm lại khôi phục được khách thuê")
                    : ProbeOutcome.Denied($"Bị từ chối khôi phục khách thuê (ErrorCode={res.ErrorCode}), dữ liệu không đổi: {before}");
            }
            catch (UnauthorizedAccessException ex)
            {
                var unchanged = string.Equals(await TenantStateAsync(world, trashedId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối khôi phục khách thuê từ thùng rác: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối nhưng hồ sơ khách vẫn bị khôi phục");
            }
        }

        // Ô ✅ Chủ trọ: tạo - xóa - khôi phục thật trên một hồ sơ riêng, kiểm del trở lại false.
        var tenantId = await NewTrashedTenantAsync(world);
        var restoreRes = await tenants.RestoreAsync(
            tenantId, await TenantRowVersionRawAsync(world, tenantId), caller);
        var state = await TenantStateAsync(world, tenantId);

        return restoreRes.Succeeded && !await IsTenantTrashedAsync(world, tenantId)
            ? ProbeOutcome.Allowed($"Chủ trọ khôi phục hồ sơ khách từ thùng rác có hiệu lực thật ({state})")
            : ProbeOutcome.Denied($"Khôi phục không có hiệu lực DB (Succeeded={restoreRes.Succeeded}, {restoreRes.ErrorCode}, trạng thái: {state})");
    }

    /// <summary>Hồ sơ khách của Nhà A đã đưa vào thùng rác, sẵn cho các lần thử khôi phục.</summary>
    private static async Task<int> NewTrashedTenantAsync(PermissionMatrixWorld world)
    {
        var tenantId = await NewTenantAsync(world, world.OwnerATenant, NextSequence());
        var deleteRes = await world.NewTenants().DeleteAsync(
            tenantId, await TenantRowVersionRawAsync(world, tenantId), world.OwnerATenant);
        if (!deleteRes.Succeeded)
        {
            throw new InvalidOperationException($"Không đưa được hồ sơ dò vào thùng rác: {deleteRes.ErrorCode}");
        }

        return tenantId;
    }

    // ==========================================
    // P4: HỢP ĐỒNG VÀ TIỀN CỌC (OP22 - OP27)
    // ==========================================

    private static async Task<ProbeOutcome> ProbeOP22_ViewContractAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var contracts = world.NewContracts();

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await contracts.ListAsync(null, caller);
                return ProbeOutcome.Allowed("Lỗi: Admin bị cấm nhưng lại đọc được hợp đồng");
            }
            catch (Exception ex)
            {
                return ProbeOutcome.Denied($"Admin bị từ chối truy cập hợp đồng P4 (D1 BusinessGuard): {ex.Message}");
            }
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            var viewOwn = await contracts.GetAsync(world.Data.ContractAId, caller);
            var caseAPass = viewOwn is not null;

            bool caseBPass;
            try
            {
                var viewOther = await contracts.GetAsync(world.Data.ContractBId, caller);
                caseBPass = viewOther is null;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or ContractNotFoundException)
            {
                // P4 ẩn đối tượng ngoài phạm vi bằng NotFound (chống dò id) — vẫn là từ chối hợp lệ.
                caseBPass = true;
            }

            return ProbeOutcome.ScopePair(caseAPass, caseBPass, "xem hợp đồng mình");
        }

        if (role == Roles.QuanLy)
        {
            // Ô ✅: "được xem" của Quản lý là xem đúng phạm vi được giao — một tình huống duy nhất,
            // vì ScopePair sinh subcase Denied và ô ✅ bị đọc nhầm thành không làm được (xem OP11).
            var list = await contracts.ListAsync(null, caller);
            var seesOwn = list.Any(c => c.ContractId == world.Data.ContractAId);
            var seesOtherHouse = list.Any(c => c.ContractId == world.Data.ContractBId);

            if (seesOtherHouse)
            {
                return ProbeOutcome.Denied("Lỗi: Quản lý thấy hợp đồng của nhà B không được giao");
            }

            return seesOwn
                ? ProbeOutcome.Allowed(
                    $"Quản lý xem được {list.Count} hợp đồng của nhà A được giao và không thấy hợp đồng nhà B")
                : ProbeOutcome.Denied("Quản lý không xem được hợp đồng của tòa nhà được giao");
        }

        var listAll = await contracts.ListAsync(null, caller);
        return listAll.Count >= 1
            ? ProbeOutcome.Allowed($"Chủ trọ xem toàn bộ danh mục hợp đồng ({listAll.Count} hợp đồng)")
            : ProbeOutcome.Denied("Không xem được danh mục hợp đồng");
    }

    private static async Task<ProbeOutcome> ProbeOP23_CreateContractAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var contracts = world.NewContracts();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var fixture = await CreateIsolatedContractSetupAsync(world);
            var before = await CountAsync(world);
            try
            {
                await contracts.CreateContractAsync(
                    new CreateContractRequest(fixture.RoomId, world.Data.Today, world.Data.Today.AddMonths(6), [fixture.TenantId]),
                    caller);
                return ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại tạo được hợp đồng");
            }
            catch (Exception ex)
            {
                var unchanged = string.Equals(await CountAsync(world), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối tạo hợp đồng, không phát sinh dòng mới: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối nhưng hợp đồng vẫn được ghi");
            }
        }

        // Ô ✅: tạo thật rồi đọc trạng thái trong DB, không tin vào thông điệp trả về.
        var setup = await CreateIsolatedContractSetupAsync(world);
        var response = await contracts.CreateContractAsync(
            new CreateContractRequest(setup.RoomId, world.Data.Today, world.Data.Today.AddMonths(6), [setup.TenantId]),
            caller);
        var status = await ContractStateAsync(world, response.ContractId);

        return status switch
        {
            "Approved" when role == Roles.ChuTro => ProbeOutcome.Allowed($"Chủ trọ tạo hợp đồng có hiệu lực ngay (ContractId={response.ContractId}, Status={status})"),
            "Pending" when role == Roles.QuanLy => ProbeOutcome.Pending($"Hợp đồng của Quản lý vào Chờ duyệt đúng D3 (ContractId={response.ContractId})"),
            "Pending" => ProbeOutcome.Pending($"Hợp đồng tạo ra ở trạng thái Chờ duyệt (Status={status})"),
            _ => ProbeOutcome.Denied($"Hợp đồng tạo ra có trạng thái không mong đợi: {status}"),
        };
    }

    private static async Task<ProbeOutcome> ProbeOP24_ApproveContractAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var contracts = world.NewContracts();

        if (symbol == MatrixSymbol.Forbidden)
        {
            // Duyệt một hợp đồng đang Chờ duyệt: vai trò bị cấm phải bị chặn và trạng thái phải còn Pending.
            var pending = await CreatePendingContractAsync(world);
            try
            {
                await contracts.ApproveContractAsync(pending, new ApproveContractRequestDto { IsApproved = true }, caller);
                return (await ContractStateAsync(world, pending)) == "Approved"
                    ? ProbeOutcome.Allowed("Lỗi: Vai trò không phải Chủ trọ lại duyệt được hợp đồng")
                    : ProbeOutcome.Denied("Lệnh duyệt không tạo hiệu lực (hợp đồng chưa được duyệt)");
            }
            catch (Exception ex)
            {
                var still = await ContractStateAsync(world, pending);
                return still == "Pending"
                    ? ProbeOutcome.Denied($"Bị từ chối duyệt hợp đồng, trạng thái vẫn {still}: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed($"Lỗi: bị từ chối nhưng trạng thái hợp đồng đã thành {still}");
            }
        }

        // Ô ✅ Chủ trọ: duyệt thật hợp đồng Pending do Quản lý tạo, đọc lại DB.
        var target = await CreatePendingContractAsync(world);
        var approveRes = await contracts.ApproveContractAsync(target, new ApproveContractRequestDto { IsApproved = true }, caller);
        var after = await ContractStateAsync(world, target);

        return after == "Approved"
            ? ProbeOutcome.Allowed($"Chủ trọ duyệt hợp đồng có hiệu lực thật (Status={after}, {approveRes.GetType().Name})")
            : ProbeOutcome.Denied($"Duyệt hợp đồng không tạo hiệu lực DB (Status={after})");
    }

    private static async Task<ProbeOutcome> ProbeOP25_LiquidateContractAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var contracts = world.NewContracts();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await ContractStateAsync(world, world.Data.ContractAId);
            try
            {
                await contracts.LiquidateContractAsync(world.Data.ContractAId, caller);
                return ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại thanh lý được hợp đồng");
            }
            catch (Exception ex)
            {
                var unchanged = string.Equals(await ContractStateAsync(world, world.Data.ContractAId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối thanh lý / gia hạn hợp đồng, trạng thái vẫn {before}: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed($"Lỗi: bị từ chối nhưng hợp đồng đã thành {await ContractStateAsync(world, world.Data.ContractAId)}");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // Mỗi chế độ thanh lý một hợp đồng Active RIÊNG và đọc lại chính hợp đồng đó, nên
            // "Duyệt" được nhận ra là không có hiệu lực ngay dù service vẫn trả về thành công.
            return await ProbeSwitchModesAsync(world, "SW-07", async () =>
            {
                var contractId = await CreateApprovedContractAsync(world);
                return new ModeProbe(
                    () => LiquidateAttemptAsync(world, contractId, caller, contracts),
                    () => ContractStateAsync(world, contractId));
            });
        }

        // Ô ✅ Chủ trọ: thanh lý một hợp đồng riêng biệt, hợp đồng nền không bị đụng.
        var owned = await LiquidateFreshContractAsync(world, world.OwnerA, contracts);
        return owned.Verdict == ProbeVerdict.Allowed
            ? ProbeOutcome.Allowed("Chủ trọ thanh lý hợp đồng có hiệu lực ngay (Status=Liquidated)")
            : owned;
    }

    /// <summary>Tạo hợp đồng Active riêng rồi để <paramref name="caller"/> thanh lý; đọc DB để kết luận.</summary>
    private static async Task<ProbeOutcome> LiquidateFreshContractAsync(
        PermissionMatrixWorld world,
        PropertyCaller caller,
        ContractService contracts)
    {
        var contractId = await CreateApprovedContractAsync(world);
        var before = await ContractStateAsync(world, contractId);

        try
        {
            await contracts.LiquidateContractAsync(contractId, caller);
            var after = await ContractStateAsync(world, contractId);
            return after == "Liquidated"
                ? ProbeOutcome.Allowed($"Thanh lý có hiệu lực DB ({before} -> {after})")
                : ProbeOutcome.Denied($"Service nhận lệnh nhưng DB vẫn {after}");
        }
        catch (Exception ex)
        {
            var after = await ContractStateAsync(world, contractId);
            if (!string.Equals(after, before, StringComparison.Ordinal))
            {
                return ProbeOutcome.Allowed($"Lỗi: exception nhưng trạng thái vẫn đổi sang {after}");
            }

            return ApprovalSignal(ex)
                ? ProbeOutcome.Pending($"Chờ duyệt theo công tắc: {ex.GetType().Name} {ex.Message}")
                : ProbeOutcome.Denied($"Bị chặn thanh lý: {ex.GetType().Name}");
        }
    }

    /// <summary>Gọi thanh lý cho MỘT chế độ công tắc; phần kết luận hiệu lực để engine ⚙️ đọc DB quyết.</summary>
    private static async Task<ProbeOutcome> LiquidateAttemptAsync(
        PermissionMatrixWorld world,
        Guid contractId,
        PropertyCaller caller,
        ContractService contracts)
    {
        var before = await ContractStateAsync(world, contractId);
        try
        {
            await contracts.LiquidateContractAsync(contractId, caller);
            return ProbeOutcome.Allowed($"Service chấp nhận lệnh thanh lý hợp đồng {contractId:D}");
        }
        catch (Exception ex)
        {
            var after = await ContractStateAsync(world, contractId);
            return !string.Equals(after, before, StringComparison.Ordinal)
                ? ProbeOutcome.Allowed($"Lỗi: exception nhưng trạng thái đã đổi sang {after}")
                : ApprovalSignal(ex)
                    ? ProbeOutcome.Pending($"Chờ duyệt theo công tắc: {ex.GetType().Name}")
                    : ProbeOutcome.Denied($"Bị chặn thanh lý: {ex.GetType().Name}");
        }
    }

    private sealed record ContractSetup(int RoomId, int TenantId);

    /// <summary>
    /// Phòng trống MỚI + hồ sơ khách MỚI trong Nhà A (nhà mà Quản lý được giao) để mỗi ô hợp đồng
    /// chạy độc lập: không dùng lại hợp đồng nền, không nhiễm trạng thái giữa các probe.
    /// Với vai trò ❌, cùng một dữ liệu hợp lệ chỉ khác người gọi => thất bại chỉ có thể do quyền.
    /// </summary>
    private static async Task<ContractSetup> CreateIsolatedContractSetupAsync(PermissionMatrixWorld world)
    {
        var seq = NextSequence();
        var room = await world.NewRooms().CreateAsync(new RoomSave
        {
            PropertyId = world.Data.PropertyAId,
            Code = $"ISO{seq}",
            StandardPrice = 1_000_000m,
        }, world.OwnerA);
        if (!room.Succeeded || room.Room is null)
        {
            throw new InvalidOperationException($"Không tạo được phòng cô lập cho probe hợp đồng: {room.ErrorCode}");
        }

        return new ContractSetup(room.Room.Id, await NewTenantAsync(world, world.OwnerATenant, 5_000 + seq));
    }

    /// <summary>Hợp đồng Chờ duyệt theo D3 (Quản lý tạo) dùng cho ô duyệt/từ chối hợp đồng.</summary>
    private static async Task<Guid> CreatePendingContractAsync(PermissionMatrixWorld world)
    {
        var setup = await CreateIsolatedContractSetupAsync(world);
        var response = await world.NewContracts().CreateContractAsync(
            new CreateContractRequest(setup.RoomId, world.Data.Today, world.Data.Today.AddMonths(6), [setup.TenantId]),
            world.Manager);
        return response.ContractId;
    }

    /// <summary>Hợp đồng đã duyệt, riêng biệt, để probe thanh lý không phá dữ liệu nền.</summary>
    private static async Task<Guid> CreateApprovedContractAsync(PermissionMatrixWorld world)
    {
        var setup = await CreateIsolatedContractSetupAsync(world);
        var contracts = world.NewContracts();
        var response = await contracts.CreateContractAsync(
            new CreateContractRequest(setup.RoomId, world.Data.Today, world.Data.Today.AddMonths(6), [setup.TenantId]),
            world.OwnerA);

        // D4: Chủ trọ tạo là hiệu lực ngay; nếu backend đổi hành vi thì duyệt tiếp để có điểm xuất phát Active.
        if (await ContractStateAsync(world, response.ContractId) == nameof(ContractStatus.Pending))
        {
            await contracts.ApproveContractAsync(
                response.ContractId, new ApproveContractRequestDto { IsApproved = true }, world.OwnerA);
        }

        return response.ContractId;
    }

    private static async Task<ProbeOutcome> ProbeOP27_UpdateDepositAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var contracts = world.NewContracts();
        var contractId = world.Data.ContractAId;

        if (role == Roles.QuanLy)
        {
            return await ProbeSwitchModesAsync(
                world,
                "SW-08",
                () => Task.FromResult(new ModeProbe(
                    async () =>
                    {
                        var current = (await contracts.GetAsync(contractId, world.OwnerA)).DepositAmount;
                        try
                        {
                            var result = await contracts.UpdateDepositAsync(contractId, current + 50_000m, caller);
                            return result.Outcome == ContractActionOutcome.Applied
                                ? ProbeOutcome.Allowed($"Áp dụng ngay tiền cọc {current + 50_000m:N0} đ")
                                : ProbeOutcome.Pending("Đã tạo yêu cầu duyệt SW-08");
                        }
                        catch (Exception ex)
                        {
                            return TranslateException(ex, "Cập nhật tiền cọc");
                        }
                    },
                    async () => (await contracts.GetAsync(contractId, world.OwnerA))
                        .DepositAmount.ToString(CultureInfo.InvariantCulture))));
        }

        try
        {
            var before = (await contracts.GetAsync(contractId, caller)).DepositAmount;
            var result = await contracts.UpdateDepositAsync(contractId, before + 25_000m, caller);
            var after = (await contracts.GetAsync(contractId, caller)).DepositAmount;
            return result.Outcome == ContractActionOutcome.Applied && after != before
                ? ProbeOutcome.Allowed($"Ghi nhận tiền cọc {before:N0} -> {after:N0} đ")
                : ProbeOutcome.Denied("Không ghi được tiền cọc.");
        }
        catch (Exception ex)
        {
            return TranslateException(ex, "Cập nhật tiền cọc");
        }
    }

    private static async Task<ProbeOutcome> ProbeOP26_ViewDepositAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var contracts = world.NewContracts();

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await contracts.GetAsync(world.Data.ContractAId, caller);
                return ProbeOutcome.Allowed("Lỗi: Admin bị cấm lại xem được tiền cọc");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ProbeOutcome.Denied($"Admin bị từ chối xem tiền cọc hợp đồng: {ex.Message}");
            }
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            var own = await contracts.GetAsync(world.Data.ContractAId, caller);
            var caseAPass = own is not null;

            bool caseBPass;
            try
            {
                var other = await contracts.GetAsync(world.Data.ContractBId, caller);
                caseBPass = other is null;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or ContractNotFoundException)
            {
                caseBPass = true;
            }

            return ProbeOutcome.ScopePair(caseAPass, caseBPass, "xem cọc hợp đồng mình");
        }

        var contract = await contracts.GetAsync(world.Data.ContractAId, caller);
        return contract is not null
            ? ProbeOutcome.Allowed($"Xem tiền cọc thành công ({contract.DepositAmount:N0} đ)")
            : ProbeOutcome.Denied("Không xem được tiền cọc");
    }

    // ==========================================
    // P5: ĐIỆN NƯỚC VÀ DỊCH VỤ (OP28 - OP33)
    // ==========================================

    private static async Task<ProbeOutcome> ProbeOP28_ViewUtilitiesAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var utilities = world.NewUtilityReadings();

        try
        {
            if (symbol == MatrixSymbol.Conditional)
            {
                var own = await utilities.ViewAsync(caller, roomId: world.Data.RoomAId);
                var ownAllowed = own.Count > 0;

                var otherDenied = false;
                try
                {
                    await utilities.ViewAsync(caller, roomId: world.Data.RoomBId);
                }
                catch (Exception)
                {
                    otherDenied = true;
                }

                return ProbeOutcome.ScopePair(
                    ownAllowed,
                    otherDenied,
                    "xem chỉ số phòng mình");
            }

            var rows = await utilities.ViewAsync(caller);
            if (symbol == MatrixSymbol.Forbidden)
            {
                return ProbeOutcome.Allowed(
                    $"Lỗi: vai trò {role} vẫn đọc được {rows.Count} dòng chỉ số.");
            }

            return rows.Count > 0
                ? ProbeOutcome.Allowed($"Đọc {rows.Count} dòng chỉ số/chi phí trong phạm vi.")
                : ProbeOutcome.Denied("Không đọc được chỉ số trong phạm vi.");
        }
        catch (Exception ex)
        {
            return TranslateException(ex, "Xem chỉ số/chi phí");
        }
    }

    private static async Task<ProbeOutcome> ProbeOP29_InputMeterReadingAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var utilities = world.NewUtilityReadings();
        var roomId = await world.CreateRoomAsync($"P5-{role}-{Guid.NewGuid():N}"[..18]);
        var periodStart = DateOnly.FromDateTime(world.Data.Today);

        var request = new RecordUtilityReadingRequest(
            roomId,
            periodStart,
            periodStart.AddMonths(1).AddDays(-1),
            ElectricEnd: 220m,
            WaterEnd: 90m);

        try
        {
            var saved = await utilities.RecordAsync(request, caller);
            var reread = await utilities.ViewAsync(caller, roomId: roomId);
            return reread.Any(row => row.Id == saved.Id)
                ? ProbeOutcome.Allowed($"Ghi và đọc lại được chỉ số phòng {roomId}.")
                : ProbeOutcome.Denied("Ghi chỉ số nhưng không đọc lại được từ DB.");
        }
        catch (Exception ex)
        {
            return TranslateException(ex, "Nhập chỉ số điện nước");
        }
    }

    private static async Task<ProbeOutcome> ProbeOP31_EditPriorPeriodReadingAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var utilities = world.NewUtilityReadings();
        var roomId = await world.CreateRoomAsync($"P5E-{role}-{Guid.NewGuid():N}"[..19]);
        var periodStart = DateOnly.FromDateTime(world.Data.Today.AddMonths(-1));
        var reading = await utilities.RecordAsync(
            new RecordUtilityReadingRequest(
                roomId,
                periodStart,
                DateOnly.FromDateTime(world.Data.Today.AddDays(-1)),
                ElectricEnd: 300m,
                WaterEnd: 100m),
            world.OwnerA);

        if (role == Roles.QuanLy)
        {
            return await ProbeSwitchModesAsync(
                world,
                "SW-09",
                () => Task.FromResult(new ModeProbe(
                    async () =>
                    {
                        try
                        {
                            var updated = await utilities.AdjustAsync(
                                reading.Id,
                                new AdjustUtilityReadingRequest(
                                    reading.ElectricEnd + 10m,
                                    reading.WaterEnd + 5m,
                                    "Điều chỉnh sai số kỳ trước"),
                                caller);
                            return ProbeOutcome.Allowed(
                                $"Áp dụng ngay chỉ số mới {updated.ElectricEnd}/{updated.WaterEnd}.");
                        }
                        catch (Exception ex)
                        {
                            return TranslateException(ex, "Sửa chỉ số kỳ liền trước");
                        }
                    },
                    async () =>
                    {
                        var current = await utilities.ViewAsync(world.OwnerA, roomId: roomId);
                        return current.Single(item => item.Id == reading.Id)
                            .ElectricEnd.ToString(CultureInfo.InvariantCulture);
                    })));
        }

        try
        {
            var updated = await utilities.AdjustAsync(
                reading.Id,
                new AdjustUtilityReadingRequest(
                    reading.ElectricEnd + 10m,
                    reading.WaterEnd + 5m,
                    "Điều chỉnh sai số kỳ trước"),
                caller);
            return updated.ElectricEnd == reading.ElectricEnd + 10m
                ? ProbeOutcome.Allowed("Cập nhật chỉ số kỳ liền trước và ghi lịch sử.")
                : ProbeOutcome.Denied("Không cập nhật được chỉ số.");
        }
        catch (Exception ex)
        {
            return TranslateException(ex, "Sửa chỉ số kỳ liền trước");
        }
    }

    private static async Task<ProbeOutcome> ProbeOP32_ViewReadingHistoryAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var utilities = world.NewUtilityReadings();
        var roomAId = await world.CreateRoomAsync($"P5H-{Guid.NewGuid():N}"[..18]);
        var reading = await utilities.RecordAsync(
            new RecordUtilityReadingRequest(
                roomAId,
                DateOnly.FromDateTime(world.Data.Today.AddMonths(-1)),
                DateOnly.FromDateTime(world.Data.Today.AddDays(-1)),
                ElectricEnd: 400m,
                WaterEnd: 150m),
            world.OwnerA);

        await utilities.AdjustAsync(
            reading.Id,
            new AdjustUtilityReadingRequest(ElectricEnd: 410m, WaterEnd: 155m, Reason: "Sửa lịch sử để kiểm thử"),
            world.OwnerA);

        try
        {
            if (symbol == MatrixSymbol.Conditional)
            {
                var own = await utilities.HistoryAsync(reading.Id, caller);
                var ownAllowed = own.Count > 0;

                var roomBReading = await utilities.RecordAsync(
                    new RecordUtilityReadingRequest(
                        world.Data.RoomBId,
                        DateOnly.FromDateTime(world.Data.Today.AddMonths(-1)),
                        DateOnly.FromDateTime(world.Data.Today.AddDays(-1)),
                        ElectricEnd: 100m,
                        WaterEnd: 50m),
                    world.OwnerB);
                await utilities.AdjustAsync(
                    roomBReading.Id,
                    new AdjustUtilityReadingRequest(110m, 55m, "Sửa ngoài phạm vi"),
                    world.OwnerB);

                var outsideDenied = false;
                try
                {
                    await utilities.HistoryAsync(roomBReading.Id, caller);
                }
                catch (Exception)
                {
                    outsideDenied = true;
                }

                return ProbeOutcome.ScopePair(
                    ownAllowed,
                    outsideDenied,
                    "xem lịch sử sửa chỉ số");
            }

            var history = await utilities.HistoryAsync(reading.Id, caller);
            if (symbol == MatrixSymbol.Forbidden)
            {
                return ProbeOutcome.Allowed(
                    $"Lỗi: vai trò {role} vẫn đọc được {history.Count} dòng lịch sử.");
            }

            return history.Count > 0
                ? ProbeOutcome.Allowed($"Đọc {history.Count} dòng lịch sử sửa chỉ số.")
                : ProbeOutcome.Denied("Không đọc được lịch sử sửa chỉ số.");
        }
        catch (Exception ex)
        {
            return TranslateException(ex, "Xem lịch sử sửa chỉ số");
        }
    }

    private static async Task<ProbeOutcome> ProbeOP33_SetupServiceCatalogAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var catalog = world.NewServiceCatalog();
        var before = await CountAsync(world);
        var req = new CreateServiceCatalogDto
        {
            PropertyId = world.Data.PropertyAId,
            Name = "Giữ xe",
            Unit = "xe/tháng",
            CalculationMethod = "FixedMonthly",
            UnitPrice = 120_000m,
            EffectiveDate = DateOnly.FromDateTime(world.Data.Today),
        };

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await catalog.CreateOrUpdateServicePriceAsync(req, caller);
                return ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại thiết lập được đơn giá dịch vụ");
            }
            catch (UnauthorizedAccessException ex)
            {
                var unchanged = string.Equals(await CountAsync(world), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối thiết lập đơn giá dịch vụ (D7), DB không thêm dòng: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối nhưng danh mục dịch vụ vẫn được ghi");
            }
        }

        // Ô ✅ Chủ trọ: ghi thật danh mục dịch vụ, đọc lại DB để xác nhận hiệu lực (D7).
        var created = await catalog.CreateOrUpdateServicePriceAsync(req, caller);
        var listed = await catalog.GetServiceCatalogsAsync(caller);
        var persisted = listed.Any(item => item.Id == created.Id && item.UnitPrice == 120_000m);

        return persisted
            ? ProbeOutcome.Allowed($"Chủ trọ thiết lập danh mục dịch vụ có hiệu lực thật (ServiceCatalogId={created.Id}, đơn giá 120.000đ).")
            : ProbeOutcome.Denied($"Service trả về Id={created.Id} nhưng không đọc lại được trong DB (danh mục đang có: {listed.Count}).");
    }

    // ==========================================
    // P6: HÓA ĐƠN VÀ THANH TOÁN (OP34 - OP39)
    // ==========================================

    private static async Task<ProbeOutcome> ProbeOP34_ViewInvoiceAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var invoiceQuery = world.NewInvoiceQuery();

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await invoiceQuery.GetInvoicesAsync(new InvoiceFilter(), caller);
                return ProbeOutcome.Allowed("Lỗi: Admin bị cấm nhưng lại xem được hóa đơn");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ProbeOutcome.Denied($"Admin bị từ chối truy cập hóa đơn P6 (D1 BusinessGuard): {ex.Message}");
            }
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            var list = await invoiceQuery.GetInvoicesAsync(new InvoiceFilter { ContractId = world.Data.ContractAId }, caller);
            var caseAPass = list.Items.Count > 0;

            var listOther = await invoiceQuery.GetInvoicesAsync(new InvoiceFilter { ContractId = world.Data.ContractBId }, caller);
            var caseBPass = listOther.Items.Count == 0;

            return ProbeOutcome.ScopePair(caseAPass, caseBPass, $"{list.Items.Count} hóa đơn hợp đồng mình");
        }

        if (role == Roles.QuanLy)
        {
            // Ô ✅: "được xem" của Quản lý là xem đúng hóa đơn nhà được giao — một tình huống duy nhất,
            // vì ScopePair sinh subcase Denied và ô ✅ bị đọc nhầm thành không làm được (xem OP11).
            var list = await invoiceQuery.GetInvoicesAsync(new InvoiceFilter(), caller);
            var seesOwn = list.Items.Any(i => i.ContractId == world.Data.ContractAId);
            var seesOtherHouse = list.Items.Any(i => i.ContractId == world.Data.ContractBId);

            if (seesOtherHouse)
            {
                return ProbeOutcome.Denied("Lỗi: Quản lý thấy hóa đơn của nhà B không được giao");
            }

            return seesOwn
                ? ProbeOutcome.Allowed(
                    $"Quản lý xem được {list.Items.Count} hóa đơn của nhà A được giao và không thấy hóa đơn nhà B")
                : ProbeOutcome.Denied("Quản lý không xem được hóa đơn của tòa nhà được giao");
        }

        var all = await invoiceQuery.GetInvoicesAsync(new InvoiceFilter(), caller);
        return all.TotalCount >= 1
            ? ProbeOutcome.Allowed($"Chủ trọ xem toàn bộ hóa đơn ({all.TotalCount} hóa đơn)")
            : ProbeOutcome.Denied("Không xem được hóa đơn");
    }

    private static async Task<ProbeOutcome> ProbeOP35_CreateInvoiceAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var invoices = world.NewInvoices();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var contractId = (await NewApprovedContractAsync(world, 24)).ContractId;
            var before = await InvoiceCountForContractAsync(world, contractId);
            try
            {
                await invoices.CreateDraftAsync(BuildDraftRequest(contractId, 1), caller);
                return ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại tạo được hóa đơn");
            }
            catch (Exception ex)
            {
                var unchanged = string.Equals(await InvoiceCountForContractAsync(world, contractId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối lập hóa đơn, không phát sinh dòng mới: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed("Lỗi: bị từ chối nhưng hóa đơn vẫn được ghi");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // Mỗi chế độ SW-10 lập nháp trên một hợp đồng riêng để hiệu lực đọc đúng trên đối tượng
            // bị tác động, không đếm toàn bảng (dễ nhiễm từ probe khác).
            return await ProbeSwitchModesAsync(world, "SW-10", async () =>
            {
                var contractId = (await NewApprovedContractAsync(world, 24)).ContractId;
                return new ModeProbe(
                    () => CreateDraftAttemptAsync(world, caller, invoices, contractId),
                    () => InvoiceCountForContractAsync(world, contractId));
            });
        }

        // Ô ✅ Chủ trọ: lập nháp thật và đọc lại DB để chắc chắn còn ở trạng thái Draft.
        var targetContract = (await NewApprovedContractAsync(world, 24)).ContractId;
        var draft = await invoices.CreateDraftAsync(BuildDraftRequest(targetContract, 1), caller);
        var status = await InvoiceStateAsync(world, draft.Id);
        return status == "Draft"
            ? ProbeOutcome.Allowed($"Chủ trọ lập hóa đơn nháp có hiệu lực thật (InvoiceId={draft.Id}, Status={status})")
            : ProbeOutcome.Denied($"Lập hóa đơn không ở trạng thái nháp như mong đợi (Status={status})");
    }

    private static async Task<ProbeOutcome> CreateDraftAttemptAsync(
        PermissionMatrixWorld world, PropertyCaller caller, InvoiceService invoices, Guid contractId)
    {
        try
        {
            var draft = await invoices.CreateDraftAsync(BuildDraftRequest(contractId, 1), caller);
            var status = await InvoiceStateAsync(world, draft.Id);
            return status == "Draft"
                ? ProbeOutcome.Allowed($"Lập hóa đơn nháp có hiệu lực DB (InvoiceId={draft.Id})")
                : ProbeOutcome.Denied($"Service trả Id={draft.Id} nhưng DB ở trạng thái {status}");
        }
        catch (Exception ex)
        {
            return TranslateException(ex, "Lập hóa đơn nháp");
        }
    }

    /// <summary>
    /// Kỳ hóa đơn bắt buộc nằm trong thời hạn hợp đồng (InvoiceService kiểm), nên các ô hóa đơn dùng
    /// hợp đồng 24 tháng riêng thay vì hợp đồng nền 6 tháng.
    /// </summary>
    private static CreateInvoiceRequest BuildDraftRequest(Guid contractId, int monthOffset)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = today.AddMonths(monthOffset);
        return new CreateInvoiceRequest
        {
            ContractId = contractId,
            PeriodStart = start,
            PeriodEnd = start.AddMonths(1).AddDays(-1),
            Items = [new InvoiceItemInputDto
            {
                ItemType = InvoiceItemType.Rent,
                Description = "Tiền thuê kiểm thử ma trận",
                Quantity = 1,
                UnitPrice = 1_500_000m,
            }],
        };
    }

    private sealed record ContractWithTenant(Guid ContractId, int TenantId);

    /// <summary>
    /// Hợp đồng đã duyệt trên phòng + hồ sơ khách MỚI trong Nhà A (tòa mà Quản lý được giao),
    /// thời hạn <paramref name="months"/> tháng. Mỗi lần thử gọi một lần để không chia sẻ trạng thái.
    /// </summary>
    private static async Task<ContractWithTenant> NewApprovedContractAsync(PermissionMatrixWorld world, int months)
    {
        var setup = await CreateIsolatedContractSetupAsync(world);
        var contracts = world.NewContracts();
        var response = await contracts.CreateContractAsync(
            new CreateContractRequest(setup.RoomId, world.Data.Today, world.Data.Today.AddMonths(months), [setup.TenantId]),
            world.OwnerA);

        // D4: Chủ trọ tạo là hiệu lực ngay; nếu backend đổi hành vi thì duyệt tiếp để chắc chắn Active.
        if (await ContractStateAsync(world, response.ContractId) == nameof(ContractStatus.Pending))
        {
            await contracts.ApproveContractAsync(
                response.ContractId, new ApproveContractRequestDto { IsApproved = true }, world.OwnerA);
        }

        return new ContractWithTenant(response.ContractId, setup.TenantId);
    }

    private static async Task<string> InvoiceCountForContractAsync(PermissionMatrixWorld world, Guid contractId)
    {
        await using var db = world.NewDb();
        var count = await db.Invoices.AsNoTracking().CountAsync(i => i.ContractId == contractId);
        return $"invoices={count}";
    }

    private static async Task<ProbeOutcome> ProbeOP36_IssueInvoiceAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var invoices = world.NewInvoices();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await InvoiceStateAsync(world, world.Data.InvoiceDraftId);
            try
            {
                await invoices.IssueAsync(world.Data.InvoiceDraftId, caller);
                return ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại phát hành được hóa đơn");
            }
            catch (Exception ex)
            {
                var unchanged = string.Equals(await InvoiceStateAsync(world, world.Data.InvoiceDraftId), before, StringComparison.Ordinal);
                return unchanged
                    ? ProbeOutcome.Denied($"Bị từ chối phát hành hóa đơn, trạng thái vẫn {before}: {ex.GetType().Name}")
                    : ProbeOutcome.Allowed($"Lỗi: bị từ chối nhưng hóa đơn đã chuyển sang {await InvoiceStateAsync(world, world.Data.InvoiceDraftId)}");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // Mỗi chế độ SW-11 phát hành một nháp riêng; engine đọc chính trạng thái nháp đó.
            return await ProbeSwitchModesAsync(world, "SW-11", async () =>
            {
                var contractId = (await NewApprovedContractAsync(world, 24)).ContractId;
                var draft = await invoices.CreateDraftAsync(BuildDraftRequest(contractId, 1), world.OwnerA);
                return new ModeProbe(
                    () => IssueAttemptAsync(world, draft.Id, caller, invoices),
                    () => InvoiceStateAsync(world, draft.Id));
            });
        }

        // Ô ✅ Chủ trọ: phát hành một nháp riêng, kiểm tra DB chuyển Issued.
        var freshContract = (await NewApprovedContractAsync(world, 24)).ContractId;
        var freshDraft = await invoices.CreateDraftAsync(BuildDraftRequest(freshContract, 1), caller);
        var issued = await invoices.IssueAsync(freshDraft.Id, caller);
        var status = await InvoiceStateAsync(world, freshDraft.Id);

        return status == "Issued" && issued.Id == freshDraft.Id
            ? ProbeOutcome.Allowed($"Chủ trọ phát hành hóa đơn có hiệu lực thật (InvoiceId={freshDraft.Id}, Status={status})")
            : ProbeOutcome.Denied($"Phát hành không tạo hiệu lực DB (InvoiceId={issued.Id}, Status={status})");
    }

    private static async Task<ProbeOutcome> IssueAttemptAsync(
        PermissionMatrixWorld world, int invoiceId, PropertyCaller caller, InvoiceService invoices)
    {
        try
        {
            await invoices.IssueAsync(invoiceId, caller);
            return ProbeOutcome.Allowed($"Service chấp nhận lệnh phát hành hóa đơn {invoiceId}");
        }
        catch (Exception ex)
        {
            var after = await InvoiceStateAsync(world, invoiceId);
            return after != "Draft"
                ? ProbeOutcome.Allowed($"Lỗi: exception nhưng trạng thái vẫn đổi sang {after}")
                : TranslateException(ex, "Phát hành hóa đơn");
        }
    }

    private static async Task<ProbeOutcome> ProbeOP37_RequestPaymentAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var payments = world.NewPayments();
        var invoice = await world.NewInvoiceQuery().GetInvoiceAsync(world.Data.InvoiceIssuedId, world.OwnerA);

        var request = new SubmitPaymentRequest
        {
            Amount = invoice.TotalAmount,
            TransferredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            TransactionCode = "PROBE",
            ProofFileIds = [Guid.NewGuid()],
        };

        try
        {
            var payment = await payments.SubmitAsync(world.Data.InvoiceIssuedId, request, caller);
            if (symbol == MatrixSymbol.Forbidden)
            {
                return ProbeOutcome.Allowed(
                    $"Lỗi: vai trò {role} vẫn gửi được yêu cầu thanh toán PaymentId={payment.Id}");
            }

            return payment.Status == PaymentStatus.Pending
                ? ProbeOutcome.Allowed(
                    $"Khách thuê gửi được minh chứng thanh toán cho hóa đơn {invoice.Id} (PaymentId={payment.Id}, Pending).")
                : ProbeOutcome.Denied($"Yêu cầu thanh toán không ở trạng thái Pending: {payment.Status}");
        }
        catch (Exception ex)
        {
            return TranslateException(ex, "Gửi yêu cầu thanh toán");
        }
    }

    private static async Task<ProbeOutcome> ProbeOP38_ConfirmPaymentAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var payments = world.NewPayments();
        var invoices = world.NewInvoices();

        // VHT-48 3A: mỗi lần thử dùng hóa đơn RIÊNG, vì DB chỉ cho một Payment Confirmed/hóa đơn.
        // Hợp đồng mới trên phòng mới nhưng vẫn gắn Khách thuê A để luồng gửi minh chứng hợp lệ.
        var setup = await CreateIsolatedContractSetupAsync(world);
        var contracts = world.NewContracts();
        var contract = await contracts.CreateContractAsync(
            new CreateContractRequest(
                setup.RoomId, world.Data.Today, world.Data.Today.AddMonths(24), [world.Data.TenantAId]),
            world.OwnerA);
        if (contract.Status == ContractStatus.Pending)
        {
            await contracts.ApproveContractAsync(
                contract.ContractId, new ApproveContractRequestDto { IsApproved = true }, world.OwnerA);
        }
        var draft = await invoices.CreateDraftAsync(BuildDraftRequest(contract.ContractId, 1), world.OwnerA);
        var invoice = await invoices.IssueAsync(draft.Id, world.OwnerA);

        var pending = await payments.SubmitAsync(
            invoice.Id,
            new SubmitPaymentRequest
            {
                Amount = invoice.TotalAmount,
                TransferredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                TransactionCode = "PROBE-CONFIRM",
                ProofFileIds = [Guid.NewGuid()],
            },
            world.TenantAAsPropertyCaller);

        try
        {
            var confirmed = await payments.ConfirmAsync(pending.Id, caller);
            if (symbol == MatrixSymbol.Forbidden)
            {
                return ProbeOutcome.Allowed(
                    $"Lỗi: vai trò {role} vẫn xác nhận được thanh toán PaymentId={pending.Id}");
            }

            return confirmed.Status == PaymentStatus.Confirmed
                ? ProbeOutcome.Allowed(
                    $"Xác nhận thanh toán PaymentId={pending.Id} có hiệu lực thật (Confirmed).")
                : ProbeOutcome.Denied($"Xác nhận thanh toán không đổi trạng thái: {confirmed.Status}");
        }
        catch (Exception ex)
        {
            await using var db = world.NewDb();
            var status = await db.Payments.AsNoTracking()
                .Where(item => item.Id == pending.Id)
                .Select(item => item.Status)
                .SingleAsync();

            return status == PaymentStatus.Pending
                ? TranslateException(ex, "Xác nhận thanh toán")
                : ProbeOutcome.Allowed(
                    $"Lỗi: bị từ chối nhưng thanh toán đã đổi trạng thái sang {status}");
        }
    }

    private static async Task<ProbeOutcome> ProbeOP39_ViewRevenueReportAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var reports = world.NewRevenueReports();

        try
        {
            var report = await reports.GetAsync(new RevenueReportFilter(), caller);
            if (symbol == MatrixSymbol.Forbidden)
            {
                return ProbeOutcome.Allowed(
                    $"Lỗi: vai trò {role} vẫn đọc được báo cáo doanh thu ({report.InvoiceCount} hóa đơn).");
            }

            return report.InvoiceCount > 0
                ? ProbeOutcome.Allowed(
                    $"Chủ trọ đọc báo cáo doanh thu: {report.InvoiceCount} hóa đơn, thu {report.TotalRevenue:N0} đ, công nợ {report.Outstanding:N0} đ.")
                : ProbeOutcome.Denied("Báo cáo doanh thu không có dữ liệu trong phạm vi.");
        }
        catch (Exception ex)
        {
            return TranslateException(ex, "Xem báo cáo doanh thu/công nợ");
        }
    }

    // ==========================================
    // P7: BÁO HỎNG VÀ SỰ CỐ (OP40 - OP43)
    // ==========================================

    private static async Task<ProbeOutcome> ProbeOP40_ViewIncidentsAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var incidents = world.NewIncidents();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var list = await incidents.ListAsync(caller);
            var detail = await incidents.GetAsync(world.Data.IncidentAId, caller);
            return list.Count == 0 && !detail.Succeeded
                ? ProbeOutcome.Denied($"Service sự cố trả 0 phiếu và từ chối đọc phiếu cụ thể cho vai trò {role}")
                : ProbeOutcome.Allowed($"Lỗi: Admin lại xem được sự cố (list={list.Count}, detail={detail.Succeeded})");
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            // Khách thuê: thấy sự cố phòng mình (Case A) và KHÔNG thấy sự cố phòng người khác (Case B).
            var list = await incidents.ListAsync(caller);
            var inScope = list.Any(i => i.Id == world.Data.IncidentAId);
            var outScopeLeak = list.Any(i => i.Id == world.Data.IncidentBId);
            var ownDetail = await incidents.GetAsync(world.Data.IncidentAId, caller);
            var otherDetail = await incidents.GetAsync(world.Data.IncidentBId, caller);

            return ProbeOutcome.ScopePair(
                inScope && ownDetail.Succeeded,
                !outScopeLeak && !otherDetail.Succeeded,
                "đọc phiếu sự cố phòng mình / phòng khác");
        }

        if (role == Roles.QuanLy)
        {
            // Ô ✅: một tình huống "xem đúng phạm vi được giao", không tách case ngoài phạm vi thành
            // subcase Denied vì ô ✅ chỉ chấp nhận tình huống thực hiện được.
            var list = await incidents.ListAsync(caller);
            var seesOwn = list.Any(i => i.Id == world.Data.IncidentAId);
            var leaksOther = list.Any(i => i.Id == world.Data.IncidentBId);

            return seesOwn && !leaksOther
                ? ProbeOutcome.Allowed(
                    $"Quản lý đọc được {list.Count} phiếu sự cố, gồm phiếu nhà A được giao và không thấy phiếu nhà khác")
                : seesOwn
                    ? ProbeOutcome.Denied($"Lỗi: Quản lý thấy cả phiếu sự cố ngoài phạm vi được giao ({list.Count} phiếu)")
                    : ProbeOutcome.Denied("Quản lý không đọc được phiếu sự cố của tòa nhà được giao");
        }

        var chuTroList = await incidents.ListAsync(caller);
        return chuTroList.Count >= 1
            ? ProbeOutcome.Allowed($"Chủ trọ xem toàn bộ phiếu sự cố ({chuTroList.Count} phiếu)")
            : ProbeOutcome.Denied("Không xem được phiếu sự cố");
    }

    private static async Task<ProbeOutcome> ProbeOP41_CreateIncidentAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var incidents = world.NewIncidents();
        var before = await CountAsync(world);

        IncidentCreateRequest Req(string title) => new()
        {
            RoomId = world.Data.RoomAId,
            Title = title,
            Description = "Cần kiểm tra thiết bị",
            Category = IncidentCategory.Equipment,
            Urgency = IncidentUrgency.Normal,
        };

        if (symbol == MatrixSymbol.Forbidden)
        {
            var res = await incidents.CreateAsync(Req("Sự cố vai trò cấm"), caller);
            var unchanged = string.Equals(await CountAsync(world), before, StringComparison.Ordinal);
            return !res.Succeeded && unchanged
                ? ProbeOutcome.Denied($"Bị từ chối tạo phiếu sự cố (ErrorCode={res.ErrorCode}), DB không thêm dòng")
                : ProbeOutcome.Allowed($"Lỗi: vai trò bị cấm vẫn tạo được phiếu sự cố (Succeeded={res.Succeeded})");
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            var seq = 0;
            return await ProbeSwitchModesAsync(
                world, "SW-12",
                () => AttemptAsync(async () =>
                {
                    var res = await incidents.CreateAsync(Req($"Sự cố QL {++seq}"), caller);
                    return (res.Succeeded, res.ErrorCode);
                }, "Quản lý tạo phiếu sự cố có hiệu lực ngay"),
                () => CountAsync(world));
        }

        var created = await incidents.CreateAsync(Req(role == Roles.KhachThue ? "Đèn phòng chập chờn" : "Phiếu bảo trì Chủ trọ"), caller);
        var persisted = created.Succeeded && await IncidentExistsAsync(world, created.Incident?.Id ?? -1);

        return persisted
            ? ProbeOutcome.Allowed($"{role} tạo phiếu sự cố có hiệu lực thật (IncidentId={created.Incident?.Id})")
            : ProbeOutcome.Denied($"Tạo phiếu sự cố không có hiệu lực DB (Succeeded={created.Succeeded}, ErrorCode={created.ErrorCode})");
    }

    private static async Task<ProbeOutcome> ProbeOP42_UpdateIncidentStatusAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var incidents = world.NewIncidents();
        var req = new IncidentUpdateRequest
        {
            Status = IncidentStatus.InProgress,
            TechnicalNotes = "Đang kiểm tra thiết bị",
        };

        if (symbol == MatrixSymbol.Forbidden)
        {
            var incidentId = await NewOpenIncidentAsync(world, "OP42");
            var before = await IncidentStateAsync(world, incidentId);
            var res = await incidents.UpdateAsync(incidentId, req, caller);
            var unchanged = string.Equals(await IncidentStateAsync(world, incidentId), before, StringComparison.Ordinal);
            return !res.Succeeded && unchanged
                ? ProbeOutcome.Denied($"Bị từ chối cập nhật trạng thái sự cố (ErrorCode={res.ErrorCode}), dữ liệu không đổi")
                : ProbeOutcome.Allowed($"Lỗi: vai trò bị cấm vẫn đổi được sự cố (Succeeded={res.Succeeded})");
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // Chuyển trạng thái chỉ hợp lệ New -> InProgress (IncidentRules.CanTransition), nên mỗi
            // chế độ công tắc phải có một phiếu còn đang New của riêng nó.
            return await ProbeSwitchModesAsync(world, "SW-13", async () =>
            {
                var incidentId = await NewOpenIncidentAsync(world, "SW13");
                return new ModeProbe(
                    () => AttemptAsync(
                        async () =>
                        {
                            var res = await incidents.UpdateAsync(incidentId, req, caller);
                            return (res.Succeeded, res.ErrorCode);
                        },
                        "Quản lý cập nhật trạng thái sự cố có hiệu lực ngay"),
                    () => IncidentStateAsync(world, incidentId));
            });
        }

        // Ô ✅ Chủ trọ: đổi trạng thái thật trên phiếu riêng và đọc lại DB.
        var target = await NewOpenIncidentAsync(world, "OP42");
        var stateBefore = await IncidentStateAsync(world, target);
        var updateRes = await incidents.UpdateAsync(target, req, caller);
        var stateAfter = await IncidentStateAsync(world, target);

        return updateRes.Succeeded && stateAfter != stateBefore && stateAfter.Contains("InProgress", StringComparison.Ordinal)
            ? ProbeOutcome.Allowed($"Chủ trọ cập nhật trạng thái sự cố có hiệu lực thật ({stateBefore} -> {stateAfter})")
            : ProbeOutcome.Denied($"Cập nhật không tạo hiệu lực DB (Succeeded={updateRes.Succeeded}, {updateRes.ErrorCode}, {stateBefore} -> {stateAfter})");
    }

    /// <summary>
    /// Phiếu sự cố MỚI của Nhà A còn ở trạng thái mở, để mỗi lần thử chuyển trạng thái có cùng
    /// điểm xuất phát hợp lệ thay vì dùng lại phiếu nền đã bị probe khác chuyển trước đó.
    /// </summary>
    private static async Task<int> NewOpenIncidentAsync(PermissionMatrixWorld world, string prefix)
    {
        var created = await world.NewIncidents().CreateAsync(
            NewIncidentRequest(world, $"{prefix} {NextSequence()}"), world.OwnerA);
        if (!created.Succeeded || created.Incident is null)
        {
            throw new InvalidOperationException($"Không tạo được phiếu sự cố dò {prefix}: {created.ErrorCode}");
        }

        return created.Incident.Id;
    }

    private static async Task<ProbeOutcome> ProbeOP43_AttachResultPhotoAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var storageCaller = new StorageCaller(caller.UserId, caller.Role);
        var photos = world.NewIncidentPhotos();
        var scope = world.NewIncidentFileScope();

        if (symbol == MatrixSymbol.Forbidden)
        {
            var before = await AttachedPhotoCountAsync(world);
            // Admin: UploadCoreAsync chặn ngay từ đầu (caller.IsAdmin) -> không có dòng tệp nào.
            var upload = await photos.UploadAsync(ResultPhotoRequest(world, world.Data.IncidentAId), storageCaller);
            var after = await AttachedPhotoCountAsync(world);
            return !upload.Succeeded && after == before
                ? ProbeOutcome.Denied($"Bị từ chối đính kèm ảnh kết quả (ErrorCode={upload.ErrorCode}), DB không gắn thêm ảnh nào")
                : ProbeOutcome.Allowed($"Lỗi: vai trò {role} vẫn đính kèm được ảnh kết quả (Succeeded={upload.Succeeded}, ErrorCode={upload.ErrorCode})");
        }

        if (symbol == MatrixSymbol.Conditional)
        {
            // deniedExample G44: "Khách thuê xem ảnh nhạy cảm khác" -> chỉ đọc ảnh phòng mình.
            var ownAccess = await scope.ResolveAsync(world.Data.IncidentAId, storageCaller);
            var otherAccess = await scope.ResolveAsync(world.Data.IncidentBId, storageCaller);
            var ownUpload = await photos.UploadAsync(
                ResultPhotoRequest(world, world.Data.IncidentAId), storageCaller);
            var attachDeniedToOwn = !ownUpload.Succeeded;
            var what = $"đọc ảnh sự cố phòng mình ({ownAccess}) / phòng khác ({otherAccess})";
            return ProbeOutcome.Multi(
                ProbeTier.Service,
                new ProbeSubcase(
                    "A-trong-pham-vi",
                    ownAccess == IncidentFileAccess.Read ? ProbeVerdict.Allowed : ProbeVerdict.Denied,
                    ownAccess == IncidentFileAccess.Read
                        ? "Khách thuê đọc được ảnh của phiếu thuộc phòng mình đang thuê"
                        : $"Khách thuê không đọc được ảnh phòng mình (access={ownAccess})"),
                new ProbeSubcase(
                    "B-ngoai-pham-vi",
                    otherAccess == IncidentFileAccess.None && attachDeniedToOwn
                        ? ProbeVerdict.Denied
                        : ProbeVerdict.Allowed,
                    otherAccess == IncidentFileAccess.None && attachDeniedToOwn
                        ? $"Bị chặn ngoài phạm vi đúng: access={otherAccess}, đính kèm ảnh bị từ chối ({ownUpload.ErrorCode})"
                        : $"RÒ RỲ: access={otherAccess}, đính kèm ảnh Succeeded={ownUpload.Succeeded}"));
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // SW-14 chỉ chi phối ảnh KẾT QUẢ (IncidentPhotoKind.Result) của Quản lý. Mỗi chế độ dùng
            // một phiếu mới (slot ảnh kết quả của một phiếu chỉ có 1) và đọc số ảnh gắn trên CHÍNH
            // phiếu đó — đếm toàn bảng sẽ nhiễm ảnh mà probe khác vừa gắn.
            return await ProbeSwitchModesAsync(world, "SW-14", async () =>
            {
                var incidentId = await NewOpenIncidentAsync(world, "SW14");
                return new ModeProbe(
                    async () =>
                    {
                        var upload = await photos.UploadAsync(
                            ResultPhotoRequest(world, incidentId), storageCaller);
                        return !upload.Succeeded
                            ? ProbeOutcome.Denied($"ErrorCode={upload.ErrorCode}")
                            : upload.ApprovalRequestId is null
                                ? ProbeOutcome.Allowed($"ảnh kết quả gắn thẳng vào phiếu {incidentId} (FileId={upload.File?.Id})")
                                : ProbeOutcome.Pending($"phiếu {incidentId} tạo yêu cầu chờ duyệt {upload.ApprovalRequestId} (FileId={upload.File?.Id})");
                    },
                    () => AttachedPhotoCountForIncidentAsync(world, incidentId));
            });
        }

        // Ô ✅ Chủ trọ: upload thật, kiểm tra dòng attachment đã gắn vào phiếu.
        var ownerPhotos = world.NewIncidentPhotos();
        var ownerUpload = await ownerPhotos.UploadAsync(
            ResultPhotoRequest(world, world.Data.IncidentAId), storageCaller);
        var attached = await AttachedPhotoCountAsync(world);
        return ownerUpload.Succeeded && ownerUpload.ApprovalRequestId is null && ownerUpload.File is not null
            ? ProbeOutcome.Allowed($"Chủ trọ đính kèm ảnh kết quả có hiệu lực thật (FileId={ownerUpload.File.Id}, ảnh gắn vào phiếu={attached})")
            : ProbeOutcome.Denied($"Chủ trọ không đính kèm được ảnh kết quả (Succeeded={ownerUpload.Succeeded}, ErrorCode={ownerUpload.ErrorCode})");
    }

    private static UploadFileRequest ResultPhotoRequest(PermissionMatrixWorld world, int incidentId)
    {
        var bytes = Convert.FromHexString("89504E470D0A1A0A00000000");
        return new UploadFileRequest
        {
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "anh-xu-ly.png"),
            FileCategory = FileCategory.IncidentPhoto,
            RelatedEntityType = RelatedEntityType.MaintenanceIncident,
            RelatedEntityId = incidentId.ToString(CultureInfo.InvariantCulture),
            IncidentPhotoKind = IncidentPhotoKind.Result,
            ExpectedPhotoCount = 1,
        };
    }

    private static IncidentCreateRequest NewIncidentRequest(PermissionMatrixWorld world, string title) => new()
    {
        RoomId = world.Data.RoomAId,
        Title = title,
        Description = "Cần kiểm tra thiết bị và chụp ảnh kết quả.",
        Category = IncidentCategory.Equipment,
        Urgency = IncidentUrgency.Normal,
    };

    /// <summary>
    /// Số ảnh ĐÃ gắn vào phiếu thật. Ảnh chờ duyệt chưa gắn (IncidentId còn null) nên không được
    /// tính ở đây — engine ⚙️ tự đếm yêu cầu chờ duyệt, nếu gộp cả hai thì chế độ "Duyệt" bị đọc
    /// nhầm thành có hiệu lực.
    /// </summary>
    private static async Task<string> AttachedPhotoCountAsync(PermissionMatrixWorld world)
    {
        await using var db = world.NewDb();
        var attached = await db.IncidentPhotoAttachments.AsNoTracking().CountAsync(a => a.IncidentId != null);
        return $"attached={attached}";
    }

    /// <summary>
    /// Số ảnh kết quả đã GẮN CHÍNH THỨC trên một phiếu. Ảnh chờ duyệt vẫn mang IncidentId nhưng kèm
    /// ApprovalRequestId và bị từ chối cho khách thuê đọc (IncidentPhotoService.CanReadAsync), nên
    /// phải loại chúng ra thì chế độ "Duyệt" mới hiện đúng là chưa có hiệu lực.
    /// </summary>
    private static async Task<string> AttachedPhotoCountForIncidentAsync(PermissionMatrixWorld world, int incidentId)
    {
        await using var db = world.NewDb();
        var attached = await db.IncidentPhotoAttachments.AsNoTracking()
            .CountAsync(a => a.IncidentId == incidentId && a.ApprovalRequestId == null);
        return $"incident={incidentId} attached={attached}";
    }

    private static async Task<bool> IncidentExistsAsync(PermissionMatrixWorld world, int incidentId)
    {
        if (incidentId <= 0) return false;
        await using var db = world.NewDb();
        return await db.MaintenanceIncidents.AsNoTracking().AnyAsync(i => i.Id == incidentId);
    }

    // ==========================================
    // P8: BÁO CÁO VÀ THỐNG KÊ (OP44 - OP45)
    // ==========================================

    private static async Task<ProbeOutcome> ProbeOP44_ViewReportsAsync(string role, string symbol, PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var reports = world.NewRoomReports();

        if (symbol == MatrixSymbol.Forbidden)
        {
            try
            {
                await reports.GetAsync(new RoomReportFilter(), caller);
                return ProbeOutcome.Allowed("Lỗi: Vai trò bị cấm lại xem được báo cáo tổng hợp");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ProbeOutcome.Denied($"Bị từ chối xem báo cáo tổng hợp: {ex.Message}");
            }
        }

        if (symbol == MatrixSymbol.SpecialModes)
        {
            // SW-15 (và mục phụ SW-15a) chỉ có 2 chế độ theo workbook: Cho phép / Không thể.
            return await ProbeSwitchModesAsync(
                world, "SW-15",
                () => AttemptAsync(async () =>
                {
                    var result = await reports.GetAsync(new RoomReportFilter(), caller);
                    return (result is not null, null);
                }, "Quản lý xem báo cáo phòng theo phạm vi được giao"));
        }

        var report = await reports.GetAsync(new RoomReportFilter(), caller);
        return report is not null
            ? ProbeOutcome.Allowed("Chủ trọ xem báo cáo tỷ lệ lấp đầy và tổng quan thành công")
            : ProbeOutcome.Denied("Không tải được báo cáo");
    }

    private static async Task<ProbeOutcome> ProbeOP45_ExportReportsAsync(
        string role,
        string symbol,
        PermissionMatrixWorld world)
    {
        var caller = world.CallerFor(role);
        var exports = world.NewReportExports();

        if (symbol == MatrixSymbol.SpecialModes)
        {
            return await ProbeSwitchModesAsync(
                world,
                "SW-16",
                () => Task.FromResult(new ModeProbe(
                    async () =>
                    {
                        try
                        {
                            var bytes = await exports.ExportRevenueExcelAsync(caller);
                            return bytes.Length > 0
                                ? ProbeOutcome.Allowed($"Xuất Excel {bytes.Length} byte")
                                : ProbeOutcome.Denied("Xuất Excel trả tệp rỗng");
                        }
                        catch (Exception ex) when (IsAuthorizationException(ex)
                            || ex is ReportExportException)
                        {
                            return TranslateException(ex, "Xuất báo cáo Excel");
                        }
                    })));
        }

        try
        {
            var bytes = await exports.ExportRevenueExcelAsync(caller);
            if (symbol == MatrixSymbol.Forbidden)
            {
                return ProbeOutcome.Allowed(
                    $"Lỗi: vai trò {role} vẫn xuất được báo cáo Excel ({bytes.Length} byte).");
            }

            // Tệp .xlsx là ZIP: 4 byte đầu phải là PK\x03\x04, chứng minh đúng định dạng Excel.
            var isZip = bytes.Length > 4
                && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04;
            return isZip
                ? ProbeOutcome.Allowed($"Chủ trọ xuất Excel hợp lệ ({bytes.Length} byte, chữ ký ZIP).")
                : ProbeOutcome.Denied("Tệp xuất không phải định dạng Excel (.xlsx).");
        }
        catch (Exception ex) when (IsAuthorizationException(ex) || ex is ReportExportException)
        {
            return TranslateException(ex, "Xuất báo cáo Excel");
        }
    }

    // ==========================================
    // HELPER: KIỂM THỬ ĐỦ MỌI CHẾ ĐỘ CÔNG TẮC BẰNG THAO TÁC THẬT
    // ==========================================

    /// <summary>
    /// Một lần thử thao tác cho MỘT chế độ công tắc. <see cref="Operation"/> gọi service thật,
    /// <see cref="ReadState"/> đọc hiệu lực nghiệp vụ của ĐỐI TƯỢNG mà chính lần thử đó tác động
    /// (không đọc số đếm toàn bảng để tránh nhiễu từ probe khác).
    /// </summary>
    public sealed record ModeProbe(Func<Task<ProbeOutcome>> Operation, Func<Task<string>>? ReadState = null);

    /// <summary>
    /// Đặt từng chế độ mà workbook ghi cho công tắc (sheet Công tắc Quản lý), mỗi chế độ gọi THẬT
    /// thao tác nghiệp vụ của Quản lý và ghi lại một tình huống con. Cuối cùng khôi phục chế độ gốc.
    /// Điểm dừng của một lần chạy nằm trong ModeProbe ở trên.
    /// </summary>
    private static Task<ProbeOutcome> ProbeSwitchModesAsync(
        PermissionMatrixWorld world,
        string switchCode,
        Func<Task<ProbeOutcome>> operation,
        Func<Task<string>>? readState = null) =>
        ProbeSwitchModesAsync(world, switchCode, () => Task.FromResult(new ModeProbe(operation, readState)));

    private static async Task<ProbeOutcome> ProbeSwitchModesAsync(
        PermissionMatrixWorld world,
        string switchCode,
        Func<Task<ModeProbe>> probeFactory)
    {
        var sw = PermissionMatrixManifest.Switches.FirstOrDefault(entry => entry.Code == switchCode)
            ?? throw new InvalidOperationException($"Công tắc {switchCode} không có trong manifest workbook.");

        var originalMode = await world.GetSwitchAsync(switchCode);
        var subcases = new List<ProbeSubcase>();

        try
        {
            foreach (var modeName in sw.Modes)
            {
                var mode = modeName switch
                {
                    "Cho phép" => ManagerToggleMode.Allow,
                    "Duyệt" => ManagerToggleMode.Approve,
                    "Không thể" => ManagerToggleMode.Deny,
                    _ => throw new InvalidOperationException($"Chế độ không rõ '{modeName}' của {switchCode}."),
                };

                await world.SetSwitchAsync(switchCode, mode);

                // Mỗi chế độ tự dựng đối tượng mới -> nhà máy probe là async.
                var probe = await probeFactory();
                var pendingBefore = await PendingApprovalCountAsync(world, switchCode);
                var before = probe.ReadState is null ? null : await probe.ReadState();
                var attempt = await probe.Operation();
                var after = probe.ReadState is null ? null : await probe.ReadState();
                var pendingAfter = await PendingApprovalCountAsync(world, switchCode);

                // Thao tác chỉ đọc (không có ReadState) thì không suy được hiệu lực ghi: tin kết quả service.
                var verdict = probe.ReadState is null
                    ? attempt.Verdict
                    : DecideModeVerdict(
                        attempt.Verdict,
                        !string.Equals(before, after, StringComparison.Ordinal),
                        pendingAfter > pendingBefore);

                subcases.Add(new ProbeSubcase(
                    $"mode:{modeName}",
                    verdict,
                    $"{attempt.Evidence} (hiệu lực DB: '{Truncate(before)}' -> '{Truncate(after)}'; " +
                    $"yêu cầu chờ duyệt {switchCode}: {pendingBefore} -> {pendingAfter})"));
            }
        }
        finally
        {
            await world.SetSwitchAsync(switchCode, originalMode);
        }

        return ProbeOutcome.Multi(ProbeTier.Service, [.. subcases]);
    }

    /// <summary>
    /// Phán quyết chế độ theo bằng chứng thật, không theo lời service tự nhận:
    /// có dòng queue mới + DB chưa đổi = Chờ duyệt; DB đổi mà không qua queue = đã có hiệu lực
    /// (kể cả khi service vừa bị từ chối => rò quyền); không có gì xảy ra = Chưa làm được.
    /// </summary>
    private static ProbeVerdict DecideModeVerdict(ProbeVerdict reported, bool stateChanged, bool approvalCreated) =>
        (approvalCreated, stateChanged) switch
        {
            (true, true) => ProbeVerdict.Allowed,
            (true, false) => ProbeVerdict.PendingApproval,
            (false, true) => ProbeVerdict.Allowed,
            (false, false) => reported == ProbeVerdict.Allowed ? ProbeVerdict.Denied : reported,
        };

    /// <summary>Số yêu cầu đang chờ duyệt của đúng một mã công tắc trong hàng đợi VHT-96.</summary>
    private static async Task<int> PendingApprovalCountAsync(PermissionMatrixWorld world, string switchCode)
    {
        await using var db = world.NewDb();
        return await db.ManagerApprovalRequests.AsNoTracking().CountAsync(
            request => request.OperationCode == switchCode
                && request.Status == ApprovalRequestStatus.Pending);
    }

    private static string Truncate(string? value) => value is null
        ? "n/a"
        : value.Length <= 60 ? value : value[..60] + "…";

    /// <summary>Dịch một lần gọi service (result object / exception) thành verdict quyền.</summary>
    private static ProbeOutcome TranslateAttempt(
        bool succeeded,
        string? errorCode,
        string allowedEvidence,
        string? pendingCode = null)
    {
        if (succeeded) return ProbeOutcome.Allowed(allowedEvidence);

        // P2/P3 báo "phải qua Chủ trọ duyệt" bằng mã lỗi ApprovalRequired (result object), không phải exception.
        if (IsApprovalCode(errorCode) || string.Equals(errorCode, pendingCode, StringComparison.OrdinalIgnoreCase))
        {
            return ProbeOutcome.Pending($"chờ duyệt: {errorCode}", errorCode: errorCode);
        }

        // Chỉ mã từ chối/phân quyền mới được coi là Denied. Mã nghiệp vụ (trùng, sai trạng thái,
        // dữ liệu không hợp lệ...) là lỗi của phép thử, phải FAIL thay vì rửa điểm cho ô ❌.
        return IsAuthorizationCode(errorCode)
            ? ProbeOutcome.Denied($"ErrorCode={errorCode}", errorCode: errorCode)
            : ProbeOutcome.Error(
                $"Lỗi nghiệp vụ không phải tín hiệu phân quyền: ErrorCode={Truncate(errorCode)}",
                errorCode: errorCode);
    }

    private static bool IsApprovalCode(string? errorCode) =>
        errorCode is not null
        && (errorCode.Contains("ApprovalRequired", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("PendingApproval", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("Chờ duyệt", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Mã lỗi thể hiện backend chủ động từ chối vì quyền/phạm vi. Các service P2–P8 thường
    /// che sự tồn tại của dữ liệu ngoài phạm vi bằng *NotFound, nên nhóm đó cũng là tín hiệu
    /// từ chối hợp lệ. Mã nghiệp vụ khác (Duplicate/Invalid/Concurrency/Save...) thì không.
    /// </summary>
    private static bool IsAuthorizationCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return false;
        }

        string[] signals =
        [
            "Forbidden", "Unauthorized", "AccessDenied", "NotAllowed",
            "OutOfScope", "OutsideScope", "OutsideOwnerScope", "NotOwner",
            "NotAssigned", "ApprovalDenied", "CannotChangeOwnRole",
            "CannotLockSelf", "CannotLockAdmin", "CannotLockLastAdmin",
            "CannotRemoveLastAdmin", "RoleNotAllowed", "ModeNotAllowed",
            "NotFound", "NotAvailable", "Unavailable", "InUse",
        ];

        return signals.Any(signal => errorCode.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAuthorizationException(Exception exception) =>
        exception is UnauthorizedAccessException
        || IsAuthorizationCode(ProbeRunner.ErrorCodeOf(exception));

    private static ProbeOutcome TranslateException(Exception exception, string allowedEvidence)
    {
        _ = allowedEvidence;
        var errorCode = ProbeRunner.ErrorCodeOf(exception);

        // Các service báo "phải qua duyệt" bằng exception mang mã ApprovalRequired/Chờ duyệt.
        if (ApprovalSignal(exception))
        {
            return ProbeOutcome.Pending(
                $"{exception.GetType().Name}: {exception.Message}",
                errorCode: errorCode);
        }

        // Chỉ exception phân quyền mới là Denied; lỗi khác phải FAIL (không được rửa điểm ô ❌).
        return IsAuthorizationException(exception)
            ? ProbeOutcome.Denied(
                $"{exception.GetType().Name}: {exception.Message}",
                errorCode: errorCode)
            : ProbeOutcome.Error(
                $"Lỗi không phải tín hiệu phân quyền: {exception.GetType().Name}: {exception.Message}",
                errorCode: errorCode);
    }

    private static bool ApprovalSignal(Exception exception)
    {
        var code = ProbeRunner.ErrorCodeOf(exception);
        var text = $"{exception.GetType().Name} {exception.Message} {code}";
        return text.Contains("ApprovalRequired", StringComparison.OrdinalIgnoreCase)
            || text.Contains("chờ duyệt", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Chờ duyệt", StringComparison.OrdinalIgnoreCase)
            || text.Contains("RequiresApproval", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Chạy một thao tác và bắt cả hai convention (result object lẫn exception) của backend.</summary>
    private static async Task<ProbeOutcome> AttemptAsync(
        Func<Task<(bool Succeeded, string? ErrorCode)>> call,
        string allowedEvidence,
        string? pendingCode = null)
    {
        try
        {
            var result = await call();
            return TranslateAttempt(result.Succeeded, result.ErrorCode, allowedEvidence, pendingCode);
        }
        catch (Exception exception)
        {
            return TranslateException(exception, allowedEvidence);
        }
    }

    // ==========================================
    // HELPER: ĐỌC TRẠNG THÁI DỮ LIỆU THẬT TỪ DB (kiểm chứng hiệu lực ghi)
    // ==========================================

    private static async Task<string> RoomStateAsync(PermissionMatrixWorld world, int roomId)
    {
        await using var db = world.NewDb();
        var room = await db.Rooms.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == roomId);
        return room is null
            ? "not-found"
            : $"{room.Code}|{room.Floor}|{room.AreaM2}|{room.StandardPrice}|{room.Status}|del={room.IsDeleted}|active={room.IsActive}";
    }

    /// <summary>
    /// Phòng dùng riêng cho MỘT lần thử chế độ công tắc: mỗi chế độ cần đối tượng chưa bị chế độ
    /// trước tác động, nên tạo mới bằng quyền Chủ trọ trong Nhà A với mã duy nhất.
    /// </summary>
    private static async Task<RoomSnapshot> NewProbeRoomAsync(PermissionMatrixWorld world, string prefix)
    {
        var code = $"{prefix}-{NextSequence():D4}";
        var created = await world.NewRooms().CreateAsync(new RoomSave
        {
            PropertyId = world.Data.PropertyAId,
            Code = code,
            Floor = 3,
            AreaM2 = 25m,
            StandardPrice = 1_500_000m,
        }, world.OwnerA);
        if (!created.Succeeded || created.Room is null)
        {
            throw new InvalidOperationException($"Không tạo được phòng dò {code}: {created.ErrorMessage}");
        }
        return await ReadRoomSnapshotAsync(world, created.Room.Id);
    }

    private static async Task<RoomSnapshot> ReadRoomSnapshotAsync(PermissionMatrixWorld world, int roomId)
    {
        await using var db = world.NewDb();
        var room = await db.Rooms.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == roomId)
            ?? throw new InvalidOperationException($"Phòng {roomId} không còn trong DB test.");
        return new RoomSnapshot(
            room.Id, room.Code, room.PropertyId, room.Floor, room.AreaM2, room.StandardPrice, room.Status, room.IsActive);
    }

    /// <summary>
    /// DTO cập nhật dựng từ đúng trạng thái đang sống, nhờ vậy chỉ trường đang bị kiểm thay đổi và
    /// <c>RoomService.ChangedKinds</c> truy đúng một công tắc duy nhất.
    /// </summary>
    private static RoomSave ToSave(RoomSnapshot room) => new()
    {
        PropertyId = room.PropertyId,
        Code = room.Code,
        Floor = room.Floor,
        AreaM2 = room.AreaM2,
        StandardPrice = room.StandardPrice,
        Status = room.Status,
        IsActive = room.IsActive,
    };

    private sealed record RoomSnapshot(
        int Id,
        string Code,
        int PropertyId,
        int? Floor,
        decimal? AreaM2,
        decimal StandardPrice,
        RoomStatus Status,
        bool IsActive);

    private static async Task<bool> IsRoomDeletedAsync(PermissionMatrixWorld world, int roomId)
    {
        await using var db = world.NewDb();
        return await db.Rooms.IgnoreQueryFilters().Where(r => r.Id == roomId).Select(r => r.IsDeleted).FirstAsync();
    }

    private static async Task<string> TenantStateAsync(PermissionMatrixWorld world, int tenantId)
    {
        await using var db = world.NewDb();
        var tenant = await db.Tenants.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
        return tenant is null
            ? "not-found"
            : $"{tenant.FullName}|{tenant.Phone}|del={tenant.IsDeleted}|active={tenant.IsActive}";
    }

    /// <summary>
    /// RowVersion hiện hành của hồ sơ khách, đọc bằng quyền Chủ trọ (bên được phép) để các case ❌
    /// không bị đánh bại bởi lỗi "rowversion không hợp lệ" thay vì lỗi quyền.
    /// </summary>
    private static async Task<string> TenantRowVersionAsync(PermissionMatrixWorld world, int tenantId)
    {
        var res = await world.NewTenants().GetAsync(tenantId, world.OwnerATenant);
        return res.Tenant?.RowVersion ?? string.Empty;
    }

    /// <summary>
    /// Hồ sơ khách đang nằm trong thùng rác hay không, đọc trực tiếp cột bool (so với chuỗi
    /// "del=true" sẽ không bao giờ khớp vì <c>bool.ToString()</c> là "True").
    /// </summary>
    private static async Task<bool> IsTenantTrashedAsync(PermissionMatrixWorld world, int tenantId)
    {
        await using var db = world.NewDb();
        return await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.IsDeleted)
            .FirstAsync();
    }

    private static async Task<string> ContractStateAsync(PermissionMatrixWorld world, Guid contractId)
    {
        await using var db = world.NewDb();
        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.ContractId == contractId);
        return contract is null ? "not-found" : contract.Status.ToString();
    }

    private static async Task<string> InvoiceStateAsync(PermissionMatrixWorld world, int invoiceId)
    {
        await using var db = world.NewDb();
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId);
        return invoice is null ? "not-found" : invoice.Status.ToString();
    }

    private static async Task<string> IncidentStateAsync(PermissionMatrixWorld world, int incidentId)
    {
        await using var db = world.NewDb();
        var incident = await db.MaintenanceIncidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == incidentId);
        return incident is null ? "not-found" : $"{incident.Status}|hist={incident.History.Count}";
    }

    private static async Task<string> CountAsync(PermissionMatrixWorld world)
    {
        await using var db = world.NewDb();
        var tenants = await db.Tenants.IgnoreQueryFilters().CountAsync();
        var rooms = await db.Rooms.IgnoreQueryFilters().CountAsync();
        var contracts = await db.Contracts.CountAsync();
        var invoices = await db.Invoices.CountAsync();
        var incidents = await db.MaintenanceIncidents.CountAsync();
        var catalogs = await db.ServiceCatalogs.CountAsync();
        return $"tenants={tenants};rooms={rooms};contracts={contracts};invoices={invoices};incidents={incidents};catalogs={catalogs}";
    }
}
