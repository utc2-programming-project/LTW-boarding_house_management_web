using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Contracts.DTOs;
using Web.Features.Invoices.DTOs;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Enums;
using Web.Features.Properties.Services;
using Web.Features.Reports.DTOs;
using Web.Features.Settings;
using Web.Features.Utilities.DTOs;

namespace Web.Features.Approvals.Services;

/// <summary>
/// Cầu nối giữa hàng đợi duyệt và giao diện: xác thực caller từ principal máy chủ,
/// đọc danh sách Chờ duyệt rồi bổ sung tên phòng/tòa/công tắc và nội dung đề xuất
/// để Chủ trọ quyết định mà không phải mở từng màn hình nghiệp vụ.
/// </summary>
public sealed class ApprovalQueuePageService(
    P2CallerProvider callers,
    IManagerApprovalService approvals,
    ApplicationDbContext db,
    IEnumerable<IApprovalApplier> appliers)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Mã SW có applier: chưa có thì không cho bấm Duyệt (tránh lỗi ApplierNotRegistered).</summary>
    private readonly HashSet<string> _applicableOperations =
        appliers.Select(applier => applier.OperationCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<ApprovalQueueResult> LoadAsync(CancellationToken ct = default)
    {
        var caller = await CallerAsync();
        if (caller is null)
        {
            return ApprovalQueueResult.Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        var result = await approvals.ListInboxAsync(caller, ct);
        if (!result.Succeeded)
        {
            return ApprovalQueueResult.Failure(result.ErrorCode, result.ErrorMessage);
        }

        return ApprovalQueueResult.Success(await EnrichAsync(result.Items, ct));
    }

    public async Task<ApprovalResult> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var caller = await CallerAsync();
        if (caller is null)
        {
            return ApprovalResult.Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        return await approvals.ApproveAsync(id, caller, ct);
    }

    public async Task<ApprovalResult> RejectAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var caller = await CallerAsync();
        if (caller is null)
        {
            return ApprovalResult.Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        return await approvals.RejectAsync(id, new RejectApprovalRequest { Reason = reason }, caller, ct);
    }

    /// <summary>null khi chưa đăng nhập hoặc principal không có role hợp lệ (trang đã chặn bằng Authorize).</summary>
    private async Task<ApprovalCaller?> CallerAsync()
    {
        try
        {
            var caller = await callers.GetCallerAsync();
            return new ApprovalCaller(caller.UserId, caller.Role);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<ApprovalQueueItem>> EnrichAsync(
        IReadOnlyList<ApprovalRequestDto> requests, CancellationToken ct)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        var tenantIds = IntKeys(requests, ApprovalTargetType.Tenant);
        var contractIds = GuidKeys(requests, ApprovalTargetType.Contract);
        var invoiceIds = IntKeys(requests, ApprovalTargetType.Invoice);
        var readingIds = IntKeys(requests, ApprovalTargetType.UtilityPeriod);
        var incidentIds = IntKeys(requests, ApprovalTargetType.Incident);
        var userIds = requests.Select(request => request.RequestedByUserId).ToHashSet(StringComparer.Ordinal);

        var tenants = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(row => tenantIds.Contains(row.Id))
            .Select(row => new TenantInfo(row.Id, row.FullName))
            .ToDictionaryAsync(row => row.Id, ct);

        var contracts = await db.Contracts.AsNoTracking()
            .Where(row => contractIds.Contains(row.ContractId))
            .Select(row => new ContractInfo(row.ContractId, row.ContractNumber, row.RoomId))
            .ToDictionaryAsync(row => row.ContractId, ct);

        var invoices = await db.Invoices.AsNoTracking()
            .Where(row => invoiceIds.Contains(row.Id))
            .Select(row => new InvoiceInfo(
                row.Id, row.ContractId, row.PeriodStart, row.PeriodEnd, row.TotalAmount))
            .ToDictionaryAsync(row => row.Id, ct);

        // SW-11 chỉ trỏ tới hóa đơn; hợp đồng của hóa đơn chưa chắc nằm trong danh sách yêu cầu.
        var missingContractIds = invoices.Values
            .Select(invoice => invoice.ContractId)
            .Where(id => !contracts.ContainsKey(id))
            .ToHashSet();
        if (missingContractIds.Count > 0)
        {
            var extraContracts = await db.Contracts.AsNoTracking()
                .Where(row => missingContractIds.Contains(row.ContractId))
                .Select(row => new ContractInfo(row.ContractId, row.ContractNumber, row.RoomId))
                .ToListAsync(ct);
            foreach (var contract in extraContracts)
            {
                contracts[contract.ContractId] = contract;
            }
        }

        var readings = await db.UtilityReadings.AsNoTracking()
            .Where(row => readingIds.Contains(row.Id))
            .Select(row => new ReadingInfo(
                row.Id, row.RoomId, row.PeriodStart, row.PeriodEnd, row.ElectricEnd, row.WaterEnd))
            .ToDictionaryAsync(row => row.Id, ct);

        var incidents = await db.MaintenanceIncidents.AsNoTracking()
            .Where(row => incidentIds.Contains(row.Id))
            .Select(row => new IncidentInfo(row.Id, row.RoomId, row.Title))
            .ToDictionaryAsync(row => row.Id, ct);

        // Mọi phòng cần hiển thị: phòng bị tác động trực tiếp + phòng suy ra từ hợp đồng,
        // chỉ số, sự cố. Sau đó lấy tên tòa để ghép nhãn "Phòng · Tòa".
        var roomIds = IntKeys(requests, ApprovalTargetType.Room);
        roomIds.UnionWith(contracts.Values.Select(contract => contract.RoomId));
        roomIds.UnionWith(readings.Values.Select(reading => reading.RoomId));
        roomIds.UnionWith(incidents.Values.Select(incident => incident.RoomId));

        var rooms = await db.Rooms.AsNoTracking().IgnoreQueryFilters()
            .Where(row => roomIds.Contains(row.Id))
            .Select(row => new RoomInfo(row.Id, row.Code, row.PropertyId, row.StandardPrice))
            .ToDictionaryAsync(row => row.Id, ct);

        var propertyIds = rooms.Values.Select(room => room.PropertyId).ToHashSet();
        var properties = await db.Properties.AsNoTracking().IgnoreQueryFilters()
            .Where(row => propertyIds.Contains(row.Id))
            .Select(row => new PropertyInfo(row.Id, row.Name))
            .ToDictionaryAsync(row => row.Id, ct);

        var users = await db.Users.AsNoTracking()
            .Where(row => userIds.Contains(row.Id))
            .Select(row => new UserInfo(row.Id, row.FullName, row.Email, row.UserName))
            .ToDictionaryAsync(row => row.Id, StringComparer.Ordinal, ct);

        var look = new Lookups(rooms, properties, contracts, invoices, readings, incidents, tenants, users);
        return requests.Select(request => BuildItem(request, look)).ToList();
    }

    private ApprovalQueueItem BuildItem(ApprovalRequestDto request, Lookups look)
    {
        var operation = ManagerToggleCatalog.Find(request.OperationCode);
        return new ApprovalQueueItem(
            request.Id,
            request.OperationCode,
            operation?.Group ?? "P9",
            request.TargetType,
            TargetLabel(request, look),
            RequesterName(request, look),
            request.RequestedAt,
            request.Status,
            request.DecisionReason,
            request.DecidedAt,
            _applicableOperations.Contains(request.OperationCode),
            Details(request, look));
    }

    private static string TargetLabel(ApprovalRequestDto request, Lookups look)
    {
        switch (request.TargetType)
        {
            case ApprovalTargetType.Room:
                return RoomLabel(request.TargetKey, look);
            case ApprovalTargetType.Tenant:
                return TryInt(request.TargetKey) is { } tenantId
                    && look.Tenants.TryGetValue(tenantId, out var tenant)
                        ? tenant.FullName
                        : FallbackKey(request.TargetKey);
            case ApprovalTargetType.Contract:
                return TryGuid(request.TargetKey) is { } contractId
                    && look.Contracts.TryGetValue(contractId, out var contract)
                        ? ContractLabel(contract, look)
                        : FallbackKey(request.TargetKey);
            case ApprovalTargetType.UtilityPeriod:
                return TryInt(request.TargetKey) is { } readingId
                    && look.Readings.TryGetValue(readingId, out var reading)
                        ? $"{RoomCode(reading.RoomId, look)} · {Period(reading.PeriodStart, reading.PeriodEnd)}"
                        : FallbackKey(request.TargetKey);
            case ApprovalTargetType.Incident:
                return TryInt(request.TargetKey) is { } incidentId
                    && look.Incidents.TryGetValue(incidentId, out var incident)
                        ? $"{RoomCode(incident.RoomId, look)} · {incident.Title}"
                        : FallbackKey(request.TargetKey);
            case ApprovalTargetType.Invoice:
                return TryInt(request.TargetKey) is { } invoiceId
                    && look.Invoices.TryGetValue(invoiceId, out var invoice)
                        ? InvoiceLabel(invoice, look)
                        : FallbackKey(request.TargetKey);
            case ApprovalTargetType.Report:
                return ReportLabel(request.PayloadJson);
            default:
                return FallbackKey(request.TargetKey);
        }
    }

    private static string RequesterName(ApprovalRequestDto request, Lookups look) =>
        look.Users.TryGetValue(request.RequestedByUserId, out var user)
            ? FirstNonEmpty(user.FullName, user.Email, user.UserName)
            : string.Empty;

    private static IReadOnlyList<ApprovalQueueLine> Details(ApprovalRequestDto request, Lookups look)
    {
        var lines = new List<ApprovalQueueLine>();
        switch (request.OperationCode.ToUpperInvariant())
        {
            case "SW-02":
                if (TryInt(request.TargetKey) is { } roomId && look.Rooms.TryGetValue(roomId, out var room))
                {
                    lines.Add(new ApprovalQueueLine("DetailCurrentRent", MoneyFormatter.Format(room.StandardPrice)));
                }

                if (ReadDecimal(request.PayloadJson, "standardPrice") is { } proposedPrice)
                {
                    lines.Add(new ApprovalQueueLine("DetailProposedRent", MoneyFormatter.Format(proposedPrice)));
                }

                break;

            case "SW-07":
            case "SW-08":
                AddContractLines(request.PayloadJson, lines);
                break;

            case "SW-09":
                AddUtilityLines(request.PayloadJson, look, lines);
                break;

            case "SW-10":
                AddInvoiceDraftLines(request.PayloadJson, lines);
                break;

            case "SW-11":
                AddInvoiceIssueLines(request.PayloadJson, look, lines);
                break;

            case "SW-12":
                AddIncidentCreateLines(request.PayloadJson, lines);
                break;

            case "SW-13":
                AddIncidentUpdateLines(request.PayloadJson, lines);
                break;

            case "SW-14":
                if (Parse<IncidentResultPhotoPayload>(request.PayloadJson) is { } photos && photos.ExpectedPhotoCount > 0)
                {
                    lines.Add(new ApprovalQueueLine("DetailExpectedPhotos", Number(photos.ExpectedPhotoCount)));
                }

                break;

            case "SW-16":
                AddReportLines(request.PayloadJson, lines);
                break;
        }

        return lines;
    }

    private static void AddContractLines(string payloadJson, List<ApprovalQueueLine> lines)
    {
        if (Parse<ContractApprovalPayload>(payloadJson) is not { } payload)
        {
            return;
        }

        var actionKey = payload.Action switch
        {
            ContractApprovalActions.Renew => "ActionRenew",
            ContractApprovalActions.Liquidate => "ActionLiquidate",
            ContractApprovalActions.UpdateDeposit => "ActionUpdateDeposit",
            _ => null,
        };
        if (actionKey is not null)
        {
            lines.Add(new ApprovalQueueLine("DetailAction", ValueKey: actionKey));
        }

        if (payload.EndDate is { } endDate)
        {
            lines.Add(new ApprovalQueueLine(
                "DetailNewEndDate", DateFormatter.Format(DateOnly.FromDateTime(endDate))));
        }

        if (payload.DepositAmount is { } deposit)
        {
            lines.Add(new ApprovalQueueLine("DetailNewDeposit", MoneyFormatter.Format(deposit)));
        }
    }

    private static void AddUtilityLines(string payloadJson, Lookups look, List<ApprovalQueueLine> lines)
    {
        if (Parse<UtilityAdjustmentApprovalPayload>(payloadJson) is not { } payload)
        {
            return;
        }

        var reading = look.Readings.GetValueOrDefault(payload.ReadingId);
        lines.Add(new ApprovalQueueLine(
            "DetailElectricReading",
            reading is null
                ? Number(payload.ElectricEnd)
                : $"{Number(reading.ElectricEnd)} → {Number(payload.ElectricEnd)}"));
        lines.Add(new ApprovalQueueLine(
            "DetailWaterReading",
            reading is null
                ? Number(payload.WaterEnd)
                : $"{Number(reading.WaterEnd)} → {Number(payload.WaterEnd)}"));

        if (!string.IsNullOrWhiteSpace(payload.Reason))
        {
            lines.Add(new ApprovalQueueLine("DetailAdjustReason", payload.Reason.Trim()));
        }
    }

    private static void AddInvoiceDraftLines(string payloadJson, List<ApprovalQueueLine> lines)
    {
        if (Parse<InvoiceDraftApprovalPayload>(payloadJson) is not { } payload)
        {
            return;
        }

        lines.Add(new ApprovalQueueLine("DetailPeriod", Period(payload.PeriodStart, payload.PeriodEnd)));
        lines.Add(new ApprovalQueueLine("DetailInvoiceLineCount", Number(payload.Items.Count)));

        var estimated = payload.Items.Sum(item => item.Amount ?? item.Quantity * item.UnitPrice);
        if (estimated > 0)
        {
            lines.Add(new ApprovalQueueLine("DetailEstimatedTotal", MoneyFormatter.Format(estimated)));
        }
    }

    private static void AddInvoiceIssueLines(string payloadJson, Lookups look, List<ApprovalQueueLine> lines)
    {
        if (Parse<InvoiceIssueApprovalPayload>(payloadJson) is not { } payload
            || !look.Invoices.TryGetValue(payload.InvoiceId, out var invoice))
        {
            return;
        }

        lines.Add(new ApprovalQueueLine("DetailPeriod", Period(invoice.PeriodStart, invoice.PeriodEnd)));
        lines.Add(new ApprovalQueueLine("DetailInvoiceTotal", MoneyFormatter.Format(invoice.TotalAmount)));
    }

    private static void AddIncidentCreateLines(string payloadJson, List<ApprovalQueueLine> lines)
    {
        if (Parse<IncidentCreatePayload>(payloadJson) is not { } payload)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(payload.Title))
        {
            lines.Add(new ApprovalQueueLine("DetailIncidentTitle", payload.Title.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(payload.Description))
        {
            lines.Add(new ApprovalQueueLine("DetailDescription", payload.Description.Trim()));
        }

        if (payload.ExpectedPhotoCount > 0)
        {
            lines.Add(new ApprovalQueueLine("DetailPhotoCount", Number(payload.ExpectedPhotoCount)));
        }
    }

    private static void AddIncidentUpdateLines(string payloadJson, List<ApprovalQueueLine> lines)
    {
        if (Parse<IncidentUpdatePayload>(payloadJson) is not { } payload)
        {
            return;
        }

        var statusKey = payload.Status switch
        {
            IncidentStatus.New => "IncidentStatusNew",
            IncidentStatus.InProgress => "IncidentStatusInProgress",
            IncidentStatus.Completed => "IncidentStatusCompleted",
            _ => null,
        };
        if (statusKey is not null)
        {
            lines.Add(new ApprovalQueueLine("DetailNewIncidentStatus", ValueKey: statusKey));
        }

        if (!string.IsNullOrWhiteSpace(payload.AssigneeName))
        {
            lines.Add(new ApprovalQueueLine("DetailAssignee", payload.AssigneeName.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(payload.TechnicalNotes))
        {
            lines.Add(new ApprovalQueueLine("DetailTechnicalNotes", payload.TechnicalNotes.Trim()));
        }

        if (payload.RepairCost is { } cost)
        {
            lines.Add(new ApprovalQueueLine("DetailRepairCost", MoneyFormatter.Format(cost)));
        }
    }

    private static void AddReportLines(string payloadJson, List<ApprovalQueueLine> lines)
    {
        if (Parse<RevenueReportFilter>(payloadJson) is not { } filter)
        {
            return;
        }

        if (filter.From is null && filter.To is null)
        {
            lines.Add(new ApprovalQueueLine("DetailReportRange", ValueKey: "DetailAllTime"));
            return;
        }

        lines.Add(new ApprovalQueueLine(
            "DetailReportRange", $"{DateFormatter.Format(filter.From)} – {DateFormatter.Format(filter.To)}"));
    }

    private static string RoomLabel(string targetKey, Lookups look) =>
        TryInt(targetKey) is { } roomId && look.Rooms.TryGetValue(roomId, out var room)
            ? $"{room.Code} · {PropertyName(room.PropertyId, look)}"
            : FallbackKey(targetKey);

    private static string RoomCode(int roomId, Lookups look) =>
        look.Rooms.TryGetValue(roomId, out var room) ? room.Code : FallbackKey(roomId.ToString(CultureInfo.InvariantCulture));

    private static string ContractLabel(ContractInfo contract, Lookups look) =>
        $"{contract.ContractNumber} · {RoomCode(contract.RoomId, look)} · {PropertyOfRoom(contract.RoomId, look)}";

    private static string InvoiceLabel(InvoiceInfo invoice, Lookups look) =>
        look.Contracts.TryGetValue(invoice.ContractId, out var contract)
            ? $"HD-{invoice.Id} · {ContractLabel(contract, look)} · {Period(invoice.PeriodStart, invoice.PeriodEnd)}"
            : $"HD-{invoice.Id} · {Period(invoice.PeriodStart, invoice.PeriodEnd)}";

    private static string PropertyOfRoom(int roomId, Lookups look) =>
        look.Rooms.TryGetValue(roomId, out var room) ? PropertyName(room.PropertyId, look) : "-";

    private static string PropertyName(int propertyId, Lookups look) =>
        look.Properties.TryGetValue(propertyId, out var property) ? property.Name : "-";

    private static string ReportLabel(string payloadJson) =>
        Parse<RevenueReportFilter>(payloadJson) is { } filter
            ? $"{DateFormatter.Format(filter.From)} – {DateFormatter.Format(filter.To)}"
            : "-";

    private static string Period(DateOnly? start, DateOnly? end) =>
        $"{DateFormatter.Format(start)} – {DateFormatter.Format(end)}";

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FallbackKey(string targetKey) =>
        string.IsNullOrWhiteSpace(targetKey) ? "-" : $"#{targetKey}";

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static T? Parse<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static decimal? ReadDecimal(string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                    && property.Value.TryGetDecimal(out var value))
                {
                    return value;
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static int? TryInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static Guid? TryGuid(string value) => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static HashSet<int> IntKeys(IReadOnlyList<ApprovalRequestDto> requests, ApprovalTargetType type) =>
        requests
            .Where(request => request.TargetType == type)
            .Select(request => TryInt(request.TargetKey))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();

    private static HashSet<Guid> GuidKeys(IReadOnlyList<ApprovalRequestDto> requests, ApprovalTargetType type) =>
        requests
            .Where(request => request.TargetType == type)
            .Select(request => TryGuid(request.TargetKey))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();

    private sealed record RoomInfo(int Id, string Code, int PropertyId, decimal StandardPrice);

    private sealed record PropertyInfo(int Id, string Name);

    private sealed record ContractInfo(Guid ContractId, string ContractNumber, int RoomId);

    private sealed record InvoiceInfo(
        int Id, Guid ContractId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal TotalAmount);

    private sealed record ReadingInfo(
        int Id, int RoomId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal ElectricEnd, decimal WaterEnd);

    private sealed record IncidentInfo(int Id, int RoomId, string Title);

    private sealed record TenantInfo(int Id, string FullName);

    private sealed record UserInfo(string Id, string? FullName, string? Email, string? UserName);

    private sealed record Lookups(
        IReadOnlyDictionary<int, RoomInfo> Rooms,
        IReadOnlyDictionary<int, PropertyInfo> Properties,
        IReadOnlyDictionary<Guid, ContractInfo> Contracts,
        IReadOnlyDictionary<int, InvoiceInfo> Invoices,
        IReadOnlyDictionary<int, ReadingInfo> Readings,
        IReadOnlyDictionary<int, IncidentInfo> Incidents,
        IReadOnlyDictionary<int, TenantInfo> Tenants,
        IReadOnlyDictionary<string, UserInfo> Users);
}
