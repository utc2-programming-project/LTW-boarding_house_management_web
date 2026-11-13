using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Services;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Settings;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Infrastructure.Exports;

namespace Web.Features.Reports.Services;

public static class ReportExportErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string ApprovalRequired = nameof(ApprovalRequired);
    public const string ApprovalDenied = nameof(ApprovalDenied);
    public const string ApprovalFailed = nameof(ApprovalFailed);
    public const string VolumeExceeded = nameof(VolumeExceeded);
}

public sealed class ReportExportAuthorizationException(string message, string errorCode)
    : UnauthorizedAccessException(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ReportExportException(string message, string errorCode)
    : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>
/// D8 — xuất Excel báo cáo doanh thu (chỉ P8). Chủ trọ xuất toàn bộ; Quản lý xuất trong
/// phạm vi tòa được giao và phải qua SW-16. Không đưa số CCCD/ảnh giấy tờ vào workbook.
/// </summary>
public sealed class ReportExportService(
    RevenueReportService revenue,
    IRoomReportService rooms,
    ITenantReportService tenants,
    IContractReportService contracts,
    ICostReportService costs,
    IIncidentReportService incidents,
    ApplicationDbContext db,
    IManagerTogglePolicy? togglePolicy = null,
    IManagerApprovalService? approvals = null,
    ReportExportOptions? options = null)
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<byte[]> ExportRevenueExcelAsync(
        PropertyCaller caller,
        RevenueReportFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        filter ??= new RevenueReportFilter();

        if (caller.IsAdmin || caller.IsKhachThue)
        {
            throw new ReportExportAuthorizationException(
                "Chỉ Chủ trọ hoặc Quản lý được xuất báo cáo Excel.",
                ReportExportErrorCodes.Forbidden);
        }

        RevenueReportDto report;
        try
        {
            report = await revenue.GetAsync(filter, caller, cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ReportExportAuthorizationException(exception.Message, ReportExportErrorCodes.Forbidden);
        }

        if (caller.IsQuanLy)
        {
            var decision = togglePolicy is null
                ? RoomChangeDecision.RequiresApproval
                : await togglePolicy.EvaluateAsync(ManagerAction.SW16, caller.Role, cancellationToken);

            if (decision == RoomChangeDecision.Denied)
            {
                throw new ReportExportException(
                    "Công tắc SW-16 đang chặn xuất báo cáo.",
                    ReportExportErrorCodes.ApprovalDenied);
            }

            if (decision == RoomChangeDecision.RequiresApproval
                && !await HasApprovedRequestAsync(filter, caller, cancellationToken))
            {
                await CreateApprovalRequestAsync(filter, caller, cancellationToken);
                throw new ReportExportException(
                    "Xuất báo cáo đang chờ Chủ trọ duyệt.",
                    ReportExportErrorCodes.ApprovalRequired);
            }
        }

        // RevenueReportService la nguon duy nhat cho bo loc, DateBasis va pham vi doanh thu.
        return CreateRevenueWorkbook(report);
    }

    public async Task<byte[]> ExportRoomsExcelAsync(
        PropertyCaller caller,
        RoomReportFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        filter ??= new RoomReportFilter();
        if (caller.IsAdmin || caller.IsKhachThue)
        {
            throw new ReportExportAuthorizationException("Khong duoc xuat bao cao.", ReportExportErrorCodes.Forbidden);
        }

        filter.Page = 1;
        filter.PageSize = 100;
        RoomReportResult source;
        try
        {
            source = await rooms.GetAsync(filter, caller, cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ReportExportAuthorizationException(exception.Message, ReportExportErrorCodes.Forbidden);
        }

        var limits = options ?? new ReportExportOptions();
        limits.Validate();
        if (source.Rooms.TotalCount > limits.MaxRowsPerSection)
        {
            throw new ReportExportException("Vuot gioi han so dong xuat.", ReportExportErrorCodes.VolumeExceeded);
        }

        await EnsureManagerExportAllowedAsync(
            ReportExportSection.Rooms,
            filter.PropertyId,
            caller,
            cancellationToken);

        var roomRows = source.Rooms.Items.ToList();
        for (var page = 2; roomRows.Count < source.Rooms.TotalCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filter.Page = page;
            var next = await rooms.GetAsync(filter, caller, cancellationToken);
            roomRows.AddRange(next.Rooms.Items);
            if (next.Rooms.Items.Count == 0) break;
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ReportExportSection.Rooms.ToSheetName());
        string[] headers = ["Nha tro", "Ma phong", "Tang", "Dien tich", "Gia chuan", "Trang thai", "Dang dung", "Cho duyet"];
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
            sheet.Cell(1, column + 1).Style.Font.Bold = true;
        }
        sheet.SheetView.FreezeRows(1);
        var rowIndex = 2;
        foreach (var room in roomRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 1), room.PropertyName);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 2), room.Code);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 3), room.Floor?.ToString());
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 4), room.AreaM2?.ToString());
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 5), room.StandardPrice);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 6), room.Status.ToString());
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 7), room.IsActive ? "Co" : "Khong");
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 8), room.HasPendingApproval ? "Co" : "Khong");
            rowIndex++;
        }
        sheet.Columns().AdjustToContents(1, Math.Min(rowIndex - 1, 200));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportTenantsExcelAsync(
        PropertyCaller caller,
        TenantReportFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        filter ??= new TenantReportFilter();
        if (caller.IsAdmin || caller.IsKhachThue)
        {
            throw new ReportExportAuthorizationException("Khong duoc xuat bao cao.", ReportExportErrorCodes.Forbidden);
        }

        filter.Page = 1;
        filter.PageSize = 100;
        TenantReportResult source;
        try
        {
            source = await tenants.GetAsync(filter, new TenantCaller(caller.UserId, caller.Role), cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ReportExportAuthorizationException(exception.Message, ReportExportErrorCodes.Forbidden);
        }

        var limits = options ?? new ReportExportOptions();
        limits.Validate();
        if (source.Tenants.TotalCount > limits.MaxRowsPerSection)
        {
            throw new ReportExportException("Vuot gioi han so dong xuat.", ReportExportErrorCodes.VolumeExceeded);
        }

        await EnsureManagerExportAllowedAsync(
            ReportExportSection.Tenants,
            filter.PropertyId,
            caller,
            cancellationToken);

        var tenantRows = source.Tenants.Items.ToList();
        for (var page = 2; tenantRows.Count < source.Tenants.TotalCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filter.Page = page;
            var next = await tenants.GetAsync(filter, new TenantCaller(caller.UserId, caller.Role), cancellationToken);
            tenantRows.AddRange(next.Tenants.Items);
            if (next.Tenants.Items.Count == 0) break;
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ReportExportSection.Tenants.ToSheetName());
        string[] headers = ["Ho ten", "Dien thoai", "Dang hoat dong", "Co tai khoan", "Dang thue", "Phong", "Nha tro"];
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
            sheet.Cell(1, column + 1).Style.Font.Bold = true;
        }
        sheet.SheetView.FreezeRows(1);
        var rowIndex = 2;
        foreach (var tenant in tenantRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 1), tenant.FullName);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 2), tenant.PhoneNumber);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 3), tenant.IsActive ? "Co" : "Khong");
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 4), tenant.HasAccount is null ? null : tenant.HasAccount.Value ? "Co" : "Khong");
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 5), tenant.HasActiveContract ? "Co" : "Khong");
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 6), tenant.RoomCode);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 7), tenant.PropertyName);
            rowIndex++;
        }
        sheet.Columns().AdjustToContents(1, Math.Min(rowIndex - 1, 200));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportContractsExcelAsync(
        PropertyCaller caller,
        ContractReportFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        filter ??= new ContractReportFilter { Page = 1, PageSize = 100 };
        filter.Page = 1;
        filter.PageSize = 100;
        var source = await contracts.GetAsync(filter, caller, cancellationToken);
        EnsureVolume(source.Contracts.TotalCount);
        await EnsureManagerExportAllowedAsync(
            ReportExportSection.Contracts,
            filter.PropertyId,
            caller,
            cancellationToken);
        var contractRows = source.Contracts.Items.ToList();
        for (var page = 2; contractRows.Count < source.Contracts.TotalCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filter.Page = page;
            var next = await contracts.GetAsync(filter, caller, cancellationToken);
            contractRows.AddRange(next.Contracts.Items);
            if (next.Contracts.Items.Count == 0) break;
        }
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ReportExportSection.Contracts.ToSheetName());
        string[] headers = ["So hop dong", "Nha tro", "Phong", "Bat dau", "Ket thuc", "Trang thai", "Gia thue", "Tien coc", "So thanh vien", "Hieu luc trong ky"];
        WriteHeaders(sheet, headers);
        var rowIndex = 2;
        foreach (var contract in contractRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 1), contract.ContractNumber);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 2), contract.PropertyName);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 3), contract.RoomCode);
            ExcelCellWriter.WriteDate(sheet.Cell(rowIndex, 4), contract.StartDate);
            ExcelCellWriter.WriteDate(sheet.Cell(rowIndex, 5), contract.EndDate);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 6), contract.Status.ToString());
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 7), contract.RentAmount);
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 8), contract.DepositAmount);
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 9), contract.MemberCount);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 10), contract.IsActiveInPeriod ? "Co" : "Khong");
            rowIndex++;
        }
        return SaveWorkbook(workbook, sheet, rowIndex);
    }

    public async Task<byte[]> ExportCostsExcelAsync(
        PropertyCaller caller, CostReportFilter? filter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        filter ??= new CostReportFilter();
        filter.Page = 1;
        filter.PageSize = 100;
        var source = await costs.GetAsync(filter, caller, cancellationToken);
        EnsureVolume(source.Readings.TotalCount);
        await EnsureManagerExportAllowedAsync(
            ReportExportSection.Costs,
            filter.PropertyId,
            caller,
            cancellationToken);
        var costRows = source.Readings.Items.ToList();
        for (var page = 2; costRows.Count < source.Readings.TotalCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filter.Page = page;
            var next = await costs.GetAsync(filter, caller, cancellationToken);
            costRows.AddRange(next.Readings.Items);
            if (next.Readings.Items.Count == 0) break;
        }
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ReportExportSection.Costs.ToSheetName());
        string[] headers = ["Ky tu", "Ky den", "Nha tro", "Phong", "Hop dong", "Dien", "Tien dien", "Nuoc", "Tien nuoc", "Tong", "Co snapshot"];
        WriteHeaders(sheet, headers);
        var rowIndex = 2;
        foreach (var item in costRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExcelCellWriter.WriteDate(sheet.Cell(rowIndex, 1), item.PeriodStart);
            ExcelCellWriter.WriteDate(sheet.Cell(rowIndex, 2), item.PeriodEnd);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 3), item.PropertyName);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 4), item.RoomCode);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 5), item.ContractId?.ToString());
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 6), item.ElectricUsage);
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 7), item.ElectricAmount);
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 8), item.WaterUsage);
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 9), item.WaterAmount);
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 10), item.TotalAmount);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 11), item.HasSnapshot ? "Co" : "Khong");
            rowIndex++;
        }
        return SaveWorkbook(workbook, sheet, rowIndex);
    }

    public async Task<byte[]> ExportIncidentsExcelAsync(
        PropertyCaller caller, IncidentReportFilter? filter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        filter ??= new IncidentReportFilter();
        filter.Page = 1;
        filter.PageSize = 100;
        var source = await incidents.GetAsync(filter, caller, cancellationToken);
        EnsureVolume(source.Incidents.TotalCount);
        await EnsureManagerExportAllowedAsync(
            ReportExportSection.Incidents,
            filter.PropertyId,
            caller,
            cancellationToken);
        var incidentRows = source.Incidents.Items.ToList();
        for (var page = 2; incidentRows.Count < source.Incidents.TotalCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filter.Page = page;
            var next = await incidents.GetAsync(filter, caller, cancellationToken);
            incidentRows.AddRange(next.Incidents.Items);
            if (next.Incidents.Items.Count == 0) break;
        }
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ReportExportSection.Incidents.ToSheetName());
        string[] headers = ["Ma", "Nha tro", "Phong", "Tieu de", "Loai", "Muc do", "Trang thai", "Thoi diem tao", "Chi phi sua chua"];
        WriteHeaders(sheet, headers);
        var rowIndex = 2;
        foreach (var item in incidentRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 1), item.IncidentId);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 2), item.PropertyName);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 3), item.RoomCode);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 4), item.Title);
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 5), item.Category.ToString());
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 6), item.Urgency.ToString());
            ExcelCellWriter.WriteText(sheet.Cell(rowIndex, 7), item.Status.ToString());
            ExcelCellWriter.WriteDateTime(sheet.Cell(rowIndex, 8), item.CreatedAtUtc);
            if (item.RepairCost is { } cost) ExcelCellWriter.WriteNumber(sheet.Cell(rowIndex, 9), cost);
            rowIndex++;
        }
        return SaveWorkbook(workbook, sheet, rowIndex);
    }

    private void EnsureVolume(int total)
    {
        var limits = options ?? new ReportExportOptions();
        limits.Validate();
        if (total > limits.MaxRowsPerSection || total > limits.MaxRowsPerFile)
        {
            throw new ReportExportException("Vuot gioi han so dong xuat.", ReportExportErrorCodes.VolumeExceeded);
        }
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var column = 0; column < headers.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
            sheet.Cell(1, column + 1).Style.Font.Bold = true;
        }
        sheet.SheetView.FreezeRows(1);
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook, IXLWorksheet sheet, int rowIndex)
    {
        sheet.Columns().AdjustToContents(1, Math.Min(rowIndex - 1, 200));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task EnsureManagerExportAllowedAsync(
        ReportExportSection section,
        int? propertyId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (!caller.IsQuanLy) return;
        var decision = togglePolicy is null
            ? RoomChangeDecision.RequiresApproval
            : await togglePolicy.EvaluateAsync(ManagerAction.SW16, caller.Role, cancellationToken);
        if (decision == RoomChangeDecision.Denied)
        {
            throw new ReportExportException("Cong tac SW-16 dang chan xuat bao cao.", ReportExportErrorCodes.ApprovalDenied);
        }
        if (decision != RoomChangeDecision.RequiresApproval) return;

        var targetKey = $"export:{section}:all:all:{propertyId?.ToString() ?? "all"}:{caller.UserId}";
        var approved = await db.Set<ManagerApprovalRequest>().AsNoTracking().AnyAsync(request =>
            request.OperationCode == ManagerToggleCatalog.ToCode(ManagerAction.SW16)
            && request.TargetType == ApprovalTargetType.Report
            && request.Status == ApprovalRequestStatus.Approved
            && request.TargetKey == targetKey
            && request.RequestedByUserId == caller.UserId,
            cancellationToken);
        if (approved) return;
        if (approvals is null)
        {
            throw new ReportExportException("Khong the gui yeu cau duyet xuat bao cao.", ReportExportErrorCodes.ApprovalFailed);
        }

        var request = await approvals.CreateAsync(new CreateApprovalRequest
        {
            OperationCode = ManagerToggleCatalog.ToCode(ManagerAction.SW16),
            TargetType = ApprovalTargetType.Report,
            TargetKey = targetKey,
            PropertyId = propertyId,
            PayloadJson = JsonSerializer.Serialize(new { Section = section, PropertyId = propertyId }),
        }, new ApprovalCaller(caller.UserId, caller.Role), cancellationToken);
        if (!request.Succeeded && request.ErrorCode != ApprovalErrorCodes.DuplicatePendingRequest)
        {
            throw new ReportExportException("Khong the gui yeu cau duyet xuat bao cao.", ReportExportErrorCodes.ApprovalFailed);
        }
        throw new ReportExportException("Xuat bao cao dang cho Chu tro duyet.", ReportExportErrorCodes.ApprovalRequired);
    }

    private IQueryable<int> VisiblePropertyIds(PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return db.Properties.AsNoTracking()
                .Where(property => property.OwnerId == caller.UserId)
                .Select(property => property.Id);
        }

        return db.PropertyManagers.AsNoTracking()
            .Where(assignment => assignment.ManagerUserId == caller.UserId
                && assignment.RevokedAt == null)
            .Select(assignment => assignment.PropertyId);
    }

    private async Task CreateApprovalRequestAsync(
        RevenueReportFilter filter,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (approvals is null)
        {
            throw new ReportExportException(
                "Không thể gửi yêu cầu duyệt xuất báo cáo.",
                ReportExportErrorCodes.ApprovalFailed);
        }

        var targetKey = CreateTargetKey(filter, caller);
        var approval = await approvals.CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = ManagerToggleCatalog.ToCode(ManagerAction.SW16),
                TargetType = ApprovalTargetType.Report,
                TargetKey = targetKey,
                PropertyId = filter.PropertyId,
                PayloadJson = JsonSerializer.Serialize(filter, PayloadJsonOptions),
            },
            new ApprovalCaller(caller.UserId, caller.Role),
            cancellationToken);

        if (!approval.Succeeded)
        {
            throw new ReportExportException(
                approval.ErrorMessage ?? "Không thể gửi yêu cầu duyệt xuất báo cáo.",
                ReportExportErrorCodes.ApprovalFailed);
        }
    }

    private async Task<bool> HasApprovedRequestAsync(
        RevenueReportFilter filter,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var targetKey = CreateTargetKey(filter, caller);
        return await db.Set<ManagerApprovalRequest>().AsNoTracking().AnyAsync(
            request => request.OperationCode == ManagerToggleCatalog.ToCode(ManagerAction.SW16)
                && request.TargetType == ApprovalTargetType.Report
                && request.Status == ApprovalRequestStatus.Approved
                && request.RequestedByUserId == caller.UserId
                && request.TargetKey == targetKey,
            cancellationToken);
    }

    private static string CreateTargetKey(RevenueReportFilter filter, PropertyCaller caller) =>
        $"export:{ReportExportSection.Revenue}:{filter.From:yyyy-MM-dd}:{filter.To:yyyy-MM-dd}:{filter.PropertyId?.ToString() ?? "all"}:{caller.UserId}";

    private static byte[] CreateRevenueWorkbook(RevenueReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ReportExportSection.Revenue.ToSheetName());
        string[] headers = ["Mã hóa đơn", "Nhà trọ", "Phòng", "Kỳ từ", "Kỳ đến", "Tổng hóa đơn", "Đã thu", "Còn lại", "Doanh thu ghi nhận", "Trạng thái"];
        WriteHeaders(sheet, headers);
        var row = 2;
        foreach (var item in report.Lines)
        {
            ExcelCellWriter.WriteNumber(sheet.Cell(row, 1), item.InvoiceId);
            ExcelCellWriter.WriteText(sheet.Cell(row, 2), item.PropertyName);
            ExcelCellWriter.WriteText(sheet.Cell(row, 3), item.RoomCode);
            ExcelCellWriter.WriteDate(sheet.Cell(row, 4), item.PeriodStart);
            ExcelCellWriter.WriteDate(sheet.Cell(row, 5), item.PeriodEnd);
            ExcelCellWriter.WriteNumber(sheet.Cell(row, 6), item.Total);
            ExcelCellWriter.WriteNumber(sheet.Cell(row, 7), item.Paid);
            ExcelCellWriter.WriteNumber(sheet.Cell(row, 8), item.Outstanding);
            ExcelCellWriter.WriteNumber(sheet.Cell(row, 9), item.RecognizedRevenue);
            ExcelCellWriter.WriteText(sheet.Cell(row, 10), item.Status);
            row++;
        }
        return SaveWorkbook(workbook, sheet, row);
    }
}
