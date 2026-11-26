using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Services;
using Web.Features.Assistant.DTOs;
using Web.Features.Assistant.Enums;
using Web.Features.Assistant.Rules;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Maintenance.Enums;
using Web.Features.Properties;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Services;
using Web.Features.Tenants;
using Web.Infrastructure.Time;

namespace Web.Features.Assistant.Services;

public sealed record AiContextBuildResult(
    bool Succeeded,
    string ContextJson,
    string InputRefsJson,
    string ScopeJson,
    string UserPrompt,
    int? PropertyId,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AiContextBuildResult Failure(string errorCode, string errorMessage) =>
        new(false, "{}", "{}", "{}", string.Empty, null, errorCode, errorMessage);
}

/// <summary>
/// Gom dữ liệu đúng phạm vi người gọi và chỉ những trường cần cho từng use case.
/// Không đưa CCCD, số tài khoản hay tệp giấy tờ vào ngữ cảnh; JSON gửi model đi qua
/// <see cref="AiPiiMasker"/> lần nữa.
/// </summary>
public sealed class AiContextBuilder(
    ApplicationDbContext db,
    TimeProvider clock,
    RevenueReportService revenue,
    IRoomReportService rooms,
    IIncidentReportService incidents,
    IContractReportService contracts,
    ICostReportService costs,
    IInvoiceQueryService invoices,
    IManagerApprovalService approvals)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxInstructionLength = 1000;

    public async Task<AiContextBuildResult> BuildAsync(
        AiGenerateRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager)
        {
            return AiContextBuildResult.Failure(
                AiErrorCodes.Forbidden, "Vai trò hiện tại không dùng được trợ lý AI.");
        }

        if (!AiUseCaseCatalog.IsKnown(request.UseCase))
        {
            return AiContextBuildResult.Failure(AiErrorCodes.UnknownUseCase, "Use case không hợp lệ.");
        }

        var today = DateOnly.FromDateTime(VietnamTime.ToVietnam(clock.GetUtcNow().UtcDateTime));
        var from = request.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = request.To ?? today;
        if (from > to)
        {
            (from, to) = (to, from);
        }

        if (to.DayNumber - from.DayNumber > 366)
        {
            from = to.AddDays(-366);
        }

        var instruction = NormalizeInstruction(request.Instruction);
        if (request.UseCase == AiUseCase.RoomDraft && instruction.Length < 3)
        {
            return AiContextBuildResult.Failure(
                AiErrorCodes.InvalidRequest, "Hãy mô tả nhà/phòng cần tạo bằng một câu.");
        }

        if (request.UseCase is AiUseCase.TenantDraft or AiUseCase.ServiceDraft
            && instruction.Length < 3)
        {
            return AiContextBuildResult.Failure(
                AiErrorCodes.InvalidRequest, "Hãy mô tả hồ sơ/dịch vụ cần tạo bằng một câu.");
        }

        if (request.UseCase == AiUseCase.ContractDraft && instruction.Length < 5)
        {
            return AiContextBuildResult.Failure(
                AiErrorCodes.InvalidRequest,
                "Hãy mô tả hợp đồng: phòng, khách thuê, thời hạn và giá thuê.");
        }

        if (request.PropertyId is { } propertyId
            && !await ScopedProperties(caller).AnyAsync(property => property.Id == propertyId, ct))
        {
            return AiContextBuildResult.Failure(
                AiErrorCodes.Forbidden, "Tòa nhà ngoài phạm vi của bạn.");
        }

        var common = new
        {
            today = Iso(today),
            from = Iso(from),
            to = Iso(to),
            periodLabel = $"{from:dd/MM/yyyy}–{to:dd/MM/yyyy}",
            propertyId = request.PropertyId,
            instruction = instruction.Length == 0 ? null : instruction,
        };

        var built = request.UseCase switch
        {
            AiUseCase.ReportSummary => await BuildReportSummaryAsync(request, common, from, to, caller, ct),
            AiUseCase.ReminderMessage => await BuildReminderAsync(request, common, caller, today, ct),
            AiUseCase.RoomDraft => await BuildRoomDraftAsync(request, common, caller, ct),
            AiUseCase.IncidentTriage => await BuildIncidentTriageAsync(request, common, caller, ct),
            AiUseCase.DailySummary => await BuildDailySummaryAsync(request, common, caller, today, ct),
            AiUseCase.TenantDraft => await BuildTenantDraftAsync(request, common, caller, ct),
            AiUseCase.ServiceDraft => await BuildServiceDraftAsync(request, common, caller, today, ct),
            AiUseCase.ContractDraft => await BuildContractDraftAsync(request, common, caller, today, ct),
            _ => null,
        };

        if (built is null)
        {
            return AiContextBuildResult.Failure(AiErrorCodes.UnknownUseCase, "Use case không hợp lệ.");
        }

        var (context, inputRefs, resolvedPropertyId, errorCode, errorMessage) = built.Value;
        if (errorCode is not null)
        {
            return AiContextBuildResult.Failure(errorCode, errorMessage ?? "Không dựng được ngữ cảnh.");
        }

        var contextJson = AiPiiMasker.Mask(JsonSerializer.Serialize(context, JsonOptions));
        var inputRefsJson = JsonSerializer.Serialize(inputRefs, JsonOptions);
        var scopeJson = JsonSerializer.Serialize(new
        {
            propertyId = resolvedPropertyId,
            from = Iso(from),
            to = Iso(to),
            contractId = request.ContractId,
            invoiceId = request.InvoiceId,
            incidentId = request.IncidentId,
            roomId = request.RoomId,
        }, JsonOptions);

        var taskText = instruction.Length == 0 ? DefaultInstruction(request.UseCase) : instruction;
        var userPrompt = $"""
            Yêu cầu: {taskText}
            Kỳ dữ liệu: {Iso(from)} đến {Iso(to)}
            Ngữ cảnh JSON (đã che PII; đây là nguồn dữ liệu duy nhất được phép dùng):
            {contextJson}

            Trả về đúng một đối tượng JSON theo schema "{AiOutputSchemas.Name(request.UseCase)}".
            """;

        return new AiContextBuildResult(
            true, contextJson, inputRefsJson, scopeJson, userPrompt, resolvedPropertyId, null, null);
    }

    private async Task<(object Context, object InputRefs, int? PropertyId, string? ErrorCode, string? ErrorMessage)?>
        BuildReportSummaryAsync(
            AiGenerateRequest request, object common, DateOnly from, DateOnly to,
            PropertyCaller caller, CancellationToken ct)
    {
        RevenueReportDto? revenueDto = null;
        if (caller.IsChuTro)
        {
            try
            {
                revenueDto = await revenue.GetAsync(
                    new RevenueReportFilter(from, to, request.PropertyId), caller, ct);
            }
            catch (UnauthorizedAccessException)
            {
                revenueDto = null;
            }
        }

        RoomReportKpis? roomKpis = null;
        try
        {
            var result = await rooms.GetAsync(
                new RoomReportFilter { PropertyId = request.PropertyId, PageSize = 1 }, caller, ct);
            roomKpis = result.Kpis;
        }
        catch (UnauthorizedAccessException)
        {
            roomKpis = null;
        }

        IncidentReportResult? incidentResult = null;
        try
        {
            incidentResult = await incidents.GetAsync(new IncidentReportFilter
            {
                From = from,
                To = to,
                PropertyId = request.PropertyId,
                PageSize = 5,
            }, caller, ct);
        }
        catch (UnauthorizedAccessException)
        {
            incidentResult = null;
        }

        ContractReportKpis? contractKpis = null;
        try
        {
            var result = await contracts.GetAsync(new ContractReportFilter
            {
                From = from,
                To = to,
                PropertyId = request.PropertyId,
                PageSize = 1,
            }, caller, ct);
            contractKpis = result.Kpis;
        }
        catch (UnauthorizedAccessException)
        {
            contractKpis = null;
        }

        CostReportKpis? costKpis = null;
        try
        {
            var result = await costs.GetAsync(new CostReportFilter
            {
                From = from,
                To = to,
                PropertyId = request.PropertyId,
                PageSize = 1,
            }, caller, ct);
            costKpis = result.Kpis;
        }
        catch (UnauthorizedAccessException)
        {
            costKpis = null;
        }

        var scopedIds = ScopedProperties(caller).Select(property => property.Id);
        var today = DateOnly.FromDateTime(VietnamTime.ToVietnam(clock.GetUtcNow().UtcDateTime));
        var horizon = today.AddDays(30).ToDateTime(TimeOnly.MinValue);
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var expiring = await (
                from contract in db.Contracts.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                where contract.Status == ContractStatus.Approved
                    && !contract.IsDeleted
                    && !room.IsDeleted
                    && scopedIds.Contains(room.PropertyId)
                    && contract.EndDate >= todayStart
                    && contract.EndDate < horizon
                select contract.ContractId)
            .CountAsync(ct);

        var propertyNames = await ScopedProperties(caller)
            .OrderBy(property => property.Name)
            .Take(20)
            .Select(property => property.Name)
            .ToListAsync(ct);

        var topOverdue = revenueDto?.Lines
            .Where(line => line.OverdueDays > 0 && line.Outstanding > 0)
            .OrderByDescending(line => line.OverdueDays)
            .ThenByDescending(line => line.Outstanding)
            .Take(5)
            .Select(line => new
            {
                roomCode = line.RoomCode,
                propertyName = line.PropertyName,
                amount = line.Outstanding,
                dueDate = line.DueDate is { } due ? Iso(due) : null,
                overdueDays = line.OverdueDays,
            })
            .ToList()
            ?? [];

        var topIncidents = incidentResult?.Incidents.Items
            .OrderByDescending(item => item.Urgency)
            .ThenByDescending(item => item.CreatedAtUtc)
            .Take(5)
            .Select(item => new
            {
                roomCode = item.RoomCode,
                title = item.Title,
                category = item.Category.ToString(),
                urgency = item.Urgency.ToString(),
                status = item.Status.ToString(),
                createdAt = item.CreatedAtUtc,
            })
            .ToList()
            ?? [];

        var context = new
        {
            common,
            propertyNames,
            revenue = revenueDto is null ? null : new
            {
                totalRevenue = revenueDto.TotalRevenue,
                outstanding = revenueDto.Outstanding,
                overdueOutstanding = revenueDto.OverdueOutstanding,
                overdueInvoiceCount = revenueDto.OverdueInvoiceCount,
                invoiceCount = revenueDto.InvoiceCount,
                paidInvoiceCount = revenueDto.PaidInvoiceCount,
            },
            rooms = roomKpis is null ? null : new
            {
                total = roomKpis.Total,
                available = roomKpis.Available,
                rented = roomKpis.Rented,
                maintenance = roomKpis.Maintenance,
            },
            incidents = incidentResult is null ? null : new
            {
                total = incidentResult.Kpis.Total,
                @new = incidentResult.Kpis.New,
                inProgress = incidentResult.Kpis.InProgress,
                completed = incidentResult.Kpis.Completed,
                urgent = incidentResult.Kpis.Urgent,
            },
            contracts = contractKpis is null ? null : new
            {
                total = contractKpis.Total,
                approved = contractKpis.Approved,
                pending = contractKpis.Pending,
                expiring30 = expiring,
            },
            costs = costKpis is null ? null : new
            {
                totalAmount = costKpis.TotalAmount,
                electricAmount = costKpis.ElectricAmount,
                waterAmount = costKpis.WaterAmount,
                missingSnapshots = costKpis.MissingSnapshotCount,
            },
            topOverdue,
            topIncidents,
        };

        var inputRefs = new
        {
            from = Iso(from),
            to = Iso(to),
            propertyId = request.PropertyId,
            overdueCount = topOverdue.Count,
            incidentCount = topIncidents.Count,
        };

        return (context, inputRefs, request.PropertyId, null, null);
    }

    private async Task<(object Context, object InputRefs, int? PropertyId, string? ErrorCode, string? ErrorMessage)?>
        BuildReminderAsync(
            AiGenerateRequest request, object common, PropertyCaller caller, DateOnly today,
            CancellationToken ct)
    {
        var list = await invoices.GetInvoicesAsync(new InvoiceFilter
        {
            Status = InvoiceStatus.Issued,
            ContractId = request.ContractId,
            Page = 1,
            PageSize = 100,
        }, caller, ct);

        IReadOnlyList<InvoiceListItemDto> candidates;
        if (request.InvoiceId is { } invoiceId)
        {
            var selected = list.Items.FirstOrDefault(item => item.Id == invoiceId);
            if (selected is null)
            {
                return (null!, null!, null, AiErrorCodes.NotFound,
                    "Không tìm thấy hóa đơn trong phạm vi của bạn.");
            }

            candidates = [selected];
        }
        else
        {
            candidates = list.Items
                .Where(item => item.OutstandingAmount > 0)
                .Where(item => request.ContractId is not null
                    || item.DueDate is null
                    || item.DueDate <= today.AddDays(7))
                .OrderBy(item => item.DueDate ?? today)
                .Take(20)
                .ToList();
        }

        if (candidates.Count == 0)
        {
            return (null!, null!, null, AiErrorCodes.InvalidRequest,
                "Không có hóa đơn còn nợ để soạn nhắc thanh toán. Hãy chọn hóa đơn hoặc hợp đồng cụ thể.");
        }

        var contractIds = candidates.Select(item => item.ContractId).Distinct().ToList();
        var contractInfo = await (
                from contract in db.Contracts.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                where contractIds.Contains(contract.ContractId)
                select new
                {
                    contract.ContractId,
                    PropertyId = property.Id,
                    PropertyName = property.Name,
                })
            .ToListAsync(ct);
        var infoByContract = contractInfo.ToDictionary(item => item.ContractId);

        var propertyIds = contractInfo.Select(item => item.PropertyId).Distinct().ToList();
        var resolvedPropertyId = request.PropertyId
            ?? (propertyIds.Count == 1 ? propertyIds[0] : null);

        var debtors = candidates.Select(item => new
        {
            invoiceId = item.Id,
            contractId = item.ContractId,
            contractNumber = item.ContractNumber,
            roomCode = item.RoomCode,
            propertyName = infoByContract.TryGetValue(item.ContractId, out var info)
                ? info.PropertyName
                : item.PropertyName,
            tenantNames = item.TenantNames,
            amount = item.OutstandingAmount,
            dueDate = item.DueDate is { } due ? Iso(due) : null,
            overdueDays = item.DueDate is { } dueDate && dueDate < today
                ? today.DayNumber - dueDate.DayNumber
                : 0,
        }).ToList();

        var context = new
        {
            common,
            debtors,
        };
        var inputRefs = new
        {
            invoiceIds = candidates.Select(item => item.Id).ToArray(),
            contractIds = contractIds.ToArray(),
            propertyIds = propertyIds.ToArray(),
        };

        return (context, inputRefs, resolvedPropertyId, null, null);
    }

    private async Task<(object Context, object InputRefs, int? PropertyId, string? ErrorCode, string? ErrorMessage)?>
        BuildRoomDraftAsync(
            AiGenerateRequest request, object common, PropertyCaller caller, CancellationToken ct)
    {
        var properties = await ScopedProperties(caller)
            .OrderBy(property => property.Name)
            .Take(20)
            .Select(property => new
            {
                id = property.Id,
                name = property.Name,
                address = property.Address,
            })
            .ToListAsync(ct);

        List<string> existingCodes = [];
        if (request.PropertyId is { } propertyId)
        {
            existingCodes = await db.Rooms
                .AsNoTracking()
                .Where(room => room.PropertyId == propertyId && !room.IsDeleted)
                .OrderBy(room => room.Code)
                .Select(room => room.Code)
                .Take(200)
                .ToListAsync(ct);
        }

        var context = new
        {
            common,
            properties,
            existingRoomCodes = existingCodes,
        };
        var inputRefs = new
        {
            propertyId = request.PropertyId,
            existingRoomCodes = existingCodes.Count == 0 ? null : existingCodes,
        };

        return (context, inputRefs, request.PropertyId, null, null);
    }

    private async Task<(object Context, object InputRefs, int? PropertyId, string? ErrorCode, string? ErrorMessage)?>
        BuildIncidentTriageAsync(
            AiGenerateRequest request, object common, PropertyCaller caller, CancellationToken ct)
    {
        var scopedIds = ScopedProperties(caller).Select(property => property.Id);
        int? roomId = request.RoomId;
        string? roomCode = null;
        string? propertyName = null;
        var history = new List<object>();
        string? incidentText = null;

        if (request.IncidentId is { } incidentId)
        {
            var incident = await (
                    from row in db.MaintenanceIncidents.AsNoTracking()
                    join room in db.Rooms.AsNoTracking() on row.RoomId equals room.Id
                    join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                    where row.Id == incidentId
                        && !row.IsDeleted
                        && !room.IsDeleted
                        && scopedIds.Contains(room.PropertyId)
                    select new
                    {
                        row.Id,
                        row.RoomId,
                        room.Code,
                        property.Name,
                        row.Title,
                        row.Description,
                    })
                .FirstOrDefaultAsync(ct);

            if (incident is null)
            {
                return (null!, null!, null, AiErrorCodes.NotFound,
                    "Không tìm thấy sự cố trong phạm vi của bạn.");
            }

            roomId = incident.RoomId;
            roomCode = incident.Code;
            propertyName = incident.Name;
            incidentText = $"{incident.Title}\n{incident.Description}";
        }

        if (roomId is { } id)
        {
            var room = await (
                    from row in db.Rooms.AsNoTracking()
                    join property in db.Properties.AsNoTracking() on row.PropertyId equals property.Id
                    where row.Id == id && !row.IsDeleted && scopedIds.Contains(row.PropertyId)
                    select new { row.Code, property.Name })
                .FirstOrDefaultAsync(ct);
            if (room is null)
            {
                return (null!, null!, null, AiErrorCodes.Forbidden,
                    "Phòng ngoài phạm vi của bạn.");
            }

            roomCode ??= room.Code;
            propertyName ??= room.Name;

            var rows = await db.MaintenanceIncidents
                .AsNoTracking()
                .Where(row => row.RoomId == id && !row.IsDeleted)
                .OrderByDescending(row => row.CreatedAt)
                .ThenByDescending(row => row.Id)
                .Take(5)
                .Select(row => new
                {
                    row.Title,
                    row.Category,
                    row.Urgency,
                    row.Status,
                    row.CreatedAt,
                })
                .ToListAsync(ct);
            history = rows.Select(row => (object)new
            {
                title = row.Title,
                category = row.Category.ToString(),
                urgency = row.Urgency.ToString(),
                status = row.Status.ToString(),
                createdAt = row.CreatedAt,
            }).ToList();
        }

        var text = NormalizeInstruction(request.Instruction);
        if (text.Length == 0)
        {
            text = incidentText ?? string.Empty;
        }

        if (text.Length < 5)
        {
            return (null!, null!, null, AiErrorCodes.InvalidRequest,
                "Hãy mô tả sự cố (ít nhất vài từ) để AI phân loại.");
        }

        var context = new
        {
            common,
            incidentText = text,
            roomId,
            roomCode,
            propertyName,
            categories = Enum.GetNames<IncidentCategory>(),
            urgencies = Enum.GetNames<IncidentUrgency>(),
            history,
        };
        var inputRefs = new
        {
            incidentId = request.IncidentId,
            roomId,
        };

        return (context, inputRefs, request.PropertyId, null, null);
    }

    private async Task<(object Context, object InputRefs, int? PropertyId, string? ErrorCode, string? ErrorMessage)?>
        BuildDailySummaryAsync(
            AiGenerateRequest request, object common, PropertyCaller caller, DateOnly today,
            CancellationToken ct)
    {
        var scopedIds = ScopedProperties(caller).Select(property => property.Id);

        var invoiceList = await invoices.GetInvoicesAsync(new InvoiceFilter
        {
            Status = InvoiceStatus.Issued,
            Page = 1,
            PageSize = 100,
        }, caller, ct);

        var open = invoiceList.Items.Where(item => item.OutstandingAmount > 0).ToList();
        var overdue = open
            .Where(item => item.DueDate is { } due && due < today)
            .OrderBy(item => item.DueDate)
            .ToList();
        var dueSoon = open
            .Where(item => item.DueDate is { } due && due >= today && due <= today.AddDays(7))
            .OrderBy(item => item.DueDate)
            .ToList();

        var incidentRows = await (
                from row in db.MaintenanceIncidents.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on row.RoomId equals room.Id
                where !row.IsDeleted
                    && !room.IsDeleted
                    && row.Status != IncidentStatus.Completed
                    && scopedIds.Contains(room.PropertyId)
                    && (request.PropertyId == null || room.PropertyId == request.PropertyId)
                select new
                {
                    row.Id,
                    room.Code,
                    row.Title,
                    row.Urgency,
                    row.Status,
                    row.CreatedAt,
                })
            .OrderByDescending(row => row.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        var openIncidents = await (
                from row in db.MaintenanceIncidents.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on row.RoomId equals room.Id
                where !row.IsDeleted
                    && !room.IsDeleted
                    && row.Status != IncidentStatus.Completed
                    && scopedIds.Contains(room.PropertyId)
                    && (request.PropertyId == null || room.PropertyId == request.PropertyId)
                select row.Urgency)
            .ToListAsync(ct);

        var approvalCaller = new ApprovalCaller(caller.UserId, caller.Role);
        var approvalResult = caller.IsChuTro
            ? await approvals.ListInboxAsync(approvalCaller, ct)
            : await approvals.ListMineAsync(approvalCaller, ct);
        var pendingApprovals = approvalResult.Succeeded ? approvalResult.Items.Count : 0;

        var aiPending = caller.IsChuTro
            ? await db.AiSuggestions.CountAsync(
                item => item.Status == AiSuggestionStatus.Pending
                    && (item.PropertyId == null || scopedIds.Contains(item.PropertyId.Value)),
                ct)
            : await db.AiSuggestions.CountAsync(
                item => item.Status == AiSuggestionStatus.Pending
                    && item.RequestedByUserId == caller.UserId,
                ct);

        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var horizon = today.AddDays(30).ToDateTime(TimeOnly.MinValue);
        var expiringItems = await (
                from contract in db.Contracts.AsNoTracking()
                join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                where contract.Status == ContractStatus.Approved
                    && !contract.IsDeleted
                    && !room.IsDeleted
                    && scopedIds.Contains(room.PropertyId)
                    && contract.EndDate >= todayStart
                    && contract.EndDate < horizon
                    && (request.PropertyId == null || room.PropertyId == request.PropertyId)
                orderby contract.EndDate
                select new
                {
                    contract.ContractNumber,
                    room.Code,
                    contract.EndDate,
                })
            .Take(5)
            .ToListAsync(ct);

        var context = new
        {
            common,
            invoiceDue = new
            {
                overdueCount = overdue.Count,
                overdueAmount = overdue.Sum(item => item.OutstandingAmount),
                dueSoonCount = dueSoon.Count,
                dueSoonAmount = dueSoon.Sum(item => item.OutstandingAmount),
                topOverdue = overdue.Take(5).Select(item => new
                {
                    item.RoomCode,
                    item.PropertyName,
                    tenantNames = item.TenantNames,
                    amount = item.OutstandingAmount,
                    dueDate = item.DueDate is { } due ? Iso(due) : null,
                }),
            },
            incidents = new
            {
                open = openIncidents.Count,
                urgent = openIncidents.Count(urgency =>
                    urgency is IncidentUrgency.Urgent or IncidentUrgency.Emergency),
                top = incidentRows.Select(row => new
                {
                    row.Code,
                    row.Title,
                    urgency = row.Urgency.ToString(),
                    status = row.Status.ToString(),
                    createdAt = row.CreatedAt,
                }),
            },
            approvals = new
            {
                pending = pendingApprovals,
            },
            aiPending,
            expiringContracts = new
            {
                count = expiringItems.Count,
                items = expiringItems.Select(item => new
                {
                    item.ContractNumber,
                    roomCode = item.Code,
                    endDate = item.EndDate,
                }),
            },
        };
        var inputRefs = new
        {
            propertyId = request.PropertyId,
            invoiceIds = open.Take(20).Select(item => item.Id).ToArray(),
        };

        return (context, inputRefs, request.PropertyId, null, null);
    }

    private async Task<(object Context, object InputRefs, int? PropertyId, string? ErrorCode, string? ErrorMessage)?>
        BuildTenantDraftAsync(
            AiGenerateRequest request, object common, PropertyCaller caller, CancellationToken ct)
    {
        var properties = await ScopedProperties(caller)
            .OrderBy(property => property.Name)
            .Take(20)
            .Select(property => new { id = property.Id, name = property.Name, address = property.Address })
            .ToListAsync(ct);

        var context = new
        {
            common,
            properties,
        };
        var inputRefs = new { propertyId = request.PropertyId };

        return (context, inputRefs, request.PropertyId, null, null);
    }

    private async Task<(object Context, object InputRefs, int? PropertyId, string? ErrorCode, string? ErrorMessage)?>
        BuildServiceDraftAsync(
            AiGenerateRequest request, object common, PropertyCaller caller, DateOnly today,
            CancellationToken ct)
    {
        int propertyId;
        if (request.PropertyId is { } requested)
        {
            propertyId = requested;
        }
        else
        {
            var candidates = await ScopedProperties(caller)
                .OrderBy(property => property.Id)
                .Select(property => property.Id)
                .Take(2)
                .ToListAsync(ct);
            if (candidates.Count != 1)
            {
                return (null!, null!, null, AiErrorCodes.InvalidRequest,
                    "Chọn một nhà trọ trước khi tạo dịch vụ.");
            }

            propertyId = candidates[0];
        }

        var property = await ScopedProperties(caller)
            .Where(item => item.Id == propertyId)
            .Select(item => new { id = item.Id, name = item.Name })
            .FirstOrDefaultAsync(ct);
        if (property is null)
        {
            return (null!, null!, null, AiErrorCodes.Forbidden,
                "Nhà trọ ngoài phạm vi của bạn.");
        }

        var catalogs = await db.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
            .Where(catalog => catalog.PropertyId == propertyId && !catalog.IsDeleted)
            .OrderBy(catalog => catalog.Id)
            .Take(50)
            .ToListAsync(ct);
        var existingServices = catalogs
            .Where(catalog => catalog.PriceHistory.Count > 0)
            .Select(catalog => catalog.PriceHistory.OrderByDescending(version => version.Id).First())
            .Select(version => new
            {
                name = version.Name,
                unit = version.Unit,
                calculationMethod = version.CalculationMethod,
                unitPrice = version.UnitPrice,
            })
            .ToList();

        var context = new
        {
            common,
            property,
            today = Iso(today),
            units = new[] { "phòng", "người", "xe", "lượt", "kWh", "m³" },
            calculationMethods = new[] { "fixed-monthly", "meter", "tiered", "custom" },
            existingServices,
        };
        var inputRefs = new { propertyId };

        return (context, inputRefs, propertyId, null, null);
    }

    private async Task<(object Context, object InputRefs, int? PropertyId, string? ErrorCode, string? ErrorMessage)?>
        BuildContractDraftAsync(
            AiGenerateRequest request, object common, PropertyCaller caller, DateOnly today,
            CancellationToken ct)
    {
        var scopedIds = ScopedProperties(caller).Select(property => property.Id);

        // Phòng còn hiện Available nhưng đã có hợp đồng chờ/đang hiệu lực vẫn không lập được,
        // nên loại khỏi ngữ cảnh để AI không gợi ý bản nháp chắc chắn bị từ chối.
        var busyRoomIds = db.Contracts.AsNoTracking()
            .Where(contract => contract.Status == Web.Features.Contracts.Enums.ContractStatus.Pending
                || contract.Status == Web.Features.Contracts.Enums.ContractStatus.Approved
                || contract.Status == Web.Features.Contracts.Enums.ContractStatus.AwaitingTenant
                || contract.Status == Web.Features.Contracts.Enums.ContractStatus.ChangeRequested
                || contract.Status == Web.Features.Contracts.Enums.ContractStatus.TenantAccepted)
            .Select(contract => contract.RoomId);

        var rooms = await (
                from room in db.Rooms.AsNoTracking()
                join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                where room.IsActive
                    && !room.IsDeleted
                    && room.Status == Web.Features.Rooms.Enums.RoomStatus.Available
                    && !busyRoomIds.Contains(room.Id)
                    && scopedIds.Contains(room.PropertyId)
                    && (request.PropertyId == null || room.PropertyId == request.PropertyId)
                orderby property.Name, room.Code
                select new
                {
                    id = room.Id,
                    propertyId = room.PropertyId,
                    propertyName = property.Name,
                    code = room.Code,
                    floor = room.Floor,
                    areaM2 = room.AreaM2,
                    standardPrice = room.StandardPrice,
                    status = room.Status.ToString(),
                    isAvailable = true,
                })
            .Take(100)
            .ToListAsync(ct);

        // Danh sách khách bị giới hạn để vừa ngữ cảnh, nên ưu tiên khách có tên/số điện thoại
        // xuất hiện trong câu lệnh; nếu không, khách tên muộn theo alphabet sẽ không bao giờ tới AI.
        var instructionText = NormalizeInstruction(request.Instruction);

        var tenants = await TenantVisibility.Apply(db, caller.UserId, caller.Role)
            .AsNoTracking()
            .Where(tenant => tenant.IsActive && !tenant.IsDeleted)
            .OrderByDescending(tenant =>
                EF.Functions.Like(instructionText, "%" + tenant.FullName + "%")
                || (tenant.Phone != null && tenant.Phone != ""
                    && EF.Functions.Like(instructionText, "%" + tenant.Phone + "%")))
            .ThenBy(tenant => tenant.FullName)
            .Select(tenant => new
            {
                id = tenant.Id,
                fullName = tenant.FullName,
                phone = tenant.Phone,
                email = tenant.Email,
            })
            .Take(200)
            .ToListAsync(ct);

        if (rooms.Count == 0)
        {
            return (null!, null!, null, AiErrorCodes.InvalidRequest,
                "Không có phòng nào trong phạm vi để lập hợp đồng.");
        }

        if (tenants.Count == 0)
        {
            return (null!, null!, null, AiErrorCodes.InvalidRequest,
                "Chưa có hồ sơ khách thuê trong phạm vi. Hãy thêm khách trước.");
        }

        var context = new
        {
            common,
            today = Iso(today),
            rooms,
            tenants,
            paymentCycles = new[] { 1, 2, 3, 6, 12 },
        };
        var inputRefs = new
        {
            propertyId = request.PropertyId,
            roomIds = rooms.Select(room => room.id).ToArray(),
            tenantIds = tenants.Select(tenant => tenant.id).ToArray(),
        };

        return (context, inputRefs, request.PropertyId, null, null);
    }

    private IQueryable<Web.Features.Properties.Entities.Property> ScopedProperties(PropertyCaller caller) =>
        PropertyScope.Apply(
                db.Properties.AsNoTracking(),
                db.PropertyManagers.AsNoTracking(),
                caller)
            .Where(property => !property.IsDeleted);

    private static string NormalizeInstruction(string? instruction)
    {
        var text = instruction?.Trim() ?? string.Empty;
        return text.Length > MaxInstructionLength ? text[..MaxInstructionLength] : text;
    }

    private static string DefaultInstruction(AiUseCase useCase) => useCase switch
    {
        AiUseCase.ReportSummary => "Tóm tắt tình hình và đề xuất việc nên làm ngay.",
        AiUseCase.ReminderMessage => "Soạn tin nhắc thanh toán lịch sự, ngắn gọn.",
        AiUseCase.RoomDraft => "Tạo nháp nhà trọ và các phòng.",
        AiUseCase.IncidentTriage => "Phân loại sự cố và mức ưu tiên.",
        AiUseCase.DailySummary => "Tóm tắt việc cần làm hôm nay.",
        AiUseCase.TenantDraft => "Tạo hồ sơ khách thuê từ mô tả.",
        AiUseCase.ServiceDraft => "Tạo danh mục dịch vụ kèm giá ban đầu.",
        AiUseCase.ContractDraft => "Lập hợp đồng thuê từ phòng và khách có sẵn.",
        _ => "Hỗ trợ vận hành nhà trọ.",
    };

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
