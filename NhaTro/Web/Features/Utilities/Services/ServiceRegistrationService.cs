using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;

namespace Web.Features.Utilities.Services;

public sealed record ServiceRegistrationDto(
    int Id,
    Guid ContractId,
    string ContractNumber,
    string RoomCode,
    string PropertyName,
    IReadOnlyList<string> TenantNames,
    IReadOnlyList<string> TenantPhones,
    int ServiceCatalogId,
    string ServiceName,
    decimal Quantity,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Note,
    ServiceRegistrationStatus Status,
    string RequestedByUserId,
    DateTimeOffset RequestedAt,
    string? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    string? RejectReason,
    ServiceRegistrationCancelStatus CancelStatus,
    string? CancelReason,
    string? CancelRejectReason,
    int? InvoiceId);

/// <summary>
/// Quy trình đăng ký dịch vụ có xét duyệt:
/// khách gửi yêu cầu (PENDING_APPROVAL) → chủ trọ duyệt/từ chối (bắt buộc lý do) →
/// chủ trọ tạo hóa đơn từ yêu cầu đã duyệt. Chỉ APPROVED mới tính dịch vụ vào P5.
/// </summary>
public sealed class ServiceRegistrationService(
    ApplicationDbContext db,
    P2CallerProvider callerProvider,
    IInvoiceService invoices,
    Web.Features.Invoices.Services.IInvoiceQueryService invoiceQuery,
    INotificationService notifications,
    ILogger<ServiceRegistrationService> logger)
{
    // Blazor có thể khởi tạo layout + page song song trên cùng circuit; DbContext
    // là scoped nên phải xếp hàng mọi thao tác (giống MonthlyUtilityCalculationPageService).
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    private async Task<T> GateAsync<T>(Func<Task<T>> work)
    {
        await _operationGate.WaitAsync();
        try
        {
            return await work();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public Task<IReadOnlyList<ServiceRegistrationDto>> ListMineAsync(CancellationToken cancellationToken = default) =>
        GateAsync(() => ListMineCoreAsync(cancellationToken));

    public Task<IReadOnlyList<ServiceRegistrationDto>> ListForLandlordAsync(
        int? propertyId = null,
        CancellationToken cancellationToken = default) =>
        GateAsync(() => ListForLandlordCoreAsync(propertyId, cancellationToken));
    private async Task<IReadOnlyList<ServiceRegistrationDto>> ListMineCoreAsync(CancellationToken cancellationToken)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsKhachThue)
        {
            throw new UnauthorizedAccessException("Chi khách thuê mới xem được lịch sử đăng ký của mình.");
        }

        var contractIds = await OwnContractIdsAsync(caller, cancellationToken);
        return await LoadAsync(contractIds, cancellationToken);
    }

    private async Task<IReadOnlyList<ServiceRegistrationDto>> ListForLandlordCoreAsync(
        int? propertyId,
        CancellationToken cancellationToken)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException("Chi chủ trọ/quản lý mới quản lý yêu cầu đăng ký.");
        }

        var propertyIds = PropertyScope.Apply(db.Properties, db.PropertyManagers, caller)
            .Select(property => property.Id);
        var contractIds = await db.Contracts
            .AsNoTracking()
            .Where(contract => contract.Status == ContractStatus.Approved
                && db.Rooms.IgnoreQueryFilters().Any(room => room.Id == contract.RoomId
                    && propertyIds.Contains(room.PropertyId)
                    && (propertyId == null || room.PropertyId == propertyId)))
            .Select(contract => contract.ContractId)
            .ToListAsync(cancellationToken);

        return await LoadAsync(contractIds, cancellationToken);
    }

    public async Task<ServiceRegistrationDto> RequestAsync(
        int serviceCatalogId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsKhachThue)
        {
            throw new UnauthorizedAccessException("Chi khách thuê mới được đăng ký dịch vụ.");
        }

        var catalog = await db.ServiceCatalogs
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceCatalogId, cancellationToken);
        if (catalog is null || catalog.IsReadOnly || catalog.IsDeleted || !catalog.IsActive)
        {
            throw new InvalidOperationException("Dịch vụ này không thể đăng ký.");
        }

        var contractIds = await OwnContractIdsAsync(caller, cancellationToken);
        if (contractIds.Count == 0)
        {
            throw new InvalidOperationException("Không tìm thấy hợp đồng hiệu lực của bạn.");
        }

        // Dịch vụ theo tháng: 1 dịch vụ chỉ đăng ký 1 lần trong cùng một tháng.
        var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthlyDuplicate = await db.ServiceRegistrationRequests
            .AsNoTracking()
            .AnyAsync(
                item => item.Status != ServiceRegistrationStatus.Rejected
                    && item.Status != ServiceRegistrationStatus.Cancelled
                    && item.ServiceCatalogId == serviceCatalogId
                    && item.StartDate.Year == monthStart.Year
                    && item.StartDate.Month == monthStart.Month
                    && contractIds.Contains(item.ContractId),
                cancellationToken);
        if (monthlyDuplicate)
        {
            throw new InvalidOperationException("Dịch vụ này đã được đăng ký trong tháng này.");
        }

        var rows = new List<ServiceRegistrationRequest>();
        foreach (var contractId in contractIds)
        {
            rows.Add(new ServiceRegistrationRequest
            {
                ContractId = contractId,
                ServiceCatalogId = serviceCatalogId,
                ServiceName = await CurrentServiceNameAsync(catalog, db, cancellationToken),
                Quantity = 1m,
                StartDate = monthStart,
                EndDate = monthEnd,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Status = ServiceRegistrationStatus.PendingApproval,
                RequestedByUserId = caller.UserId,
                RequestedAt = DateTimeOffset.UtcNow,
            });
        }

        db.ServiceRegistrationRequests.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Tenant {UserId} requested service {ServiceId} for {Count} contract(s)",
            caller.UserId,
            serviceCatalogId,
            contractIds.Count);

        foreach (var contractId in contractIds)
        {
            var ownerId = await OwnerUserIdAsync(contractId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                await notifications.NotifyAsync(
                    ownerId!,
                    "Yêu cầu đăng ký dịch vụ mới",
                    $"Khách thuê vừa đăng ký dịch vụ {rows[0].ServiceName}.",
                    "/service-registrations");
            }
        }

        var first = rows[0];
        return await ToDtoAsync(first, cancellationToken);
    }

    public async Task<ServiceRegistrationDto> ApproveAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException("Chi chủ trọ/quản lý mới duyệt được yêu cầu.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status == ServiceRegistrationStatus.Approved)
        {
            await transaction.CommitAsync(cancellationToken);
            return await ToDtoAsync(request, cancellationToken);
        }

        if (request.Status != ServiceRegistrationStatus.PendingApproval)
        {
            throw new InvalidOperationException("Yêu cầu này không thể duyệt (đã xử lý trước đó).");
        }

        request.Status = ServiceRegistrationStatus.Approved;
        request.ReviewedByUserId = caller.UserId;
        request.ReviewedAt = DateTimeOffset.UtcNow;

        // Đăng ký đã duyệt được tính vào P5 của đúng tháng yêu cầu.
        var monthStart = new DateOnly(request.StartDate.Year, request.StartDate.Month, 1);
        var exists = await db.ContractServiceRegistrations.AnyAsync(
            item => item.ContractId == request.ContractId
                && item.ServiceCatalogId == request.ServiceCatalogId
                && item.MonthStart == monthStart,
            cancellationToken);
        if (!exists)
        {
            db.ContractServiceRegistrations.Add(new ContractServiceRegistration
            {
                ContractId = request.ContractId,
                ServiceCatalogId = request.ServiceCatalogId,
                MonthStart = monthStart,
                RegisteredByUserId = caller.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var tenantUserId = await TenantUserIdAsync(request.ContractId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tenantUserId))
        {
            await notifications.NotifyAsync(
                tenantUserId!,
                "Yêu cầu đăng ký dịch vụ đã được duyệt",
                $"Dịch vụ {request.ServiceName} của bạn đã được duyệt.",
                "/my/service-registrations");
        }

        return await ToDtoAsync(request, cancellationToken);
    }

    public async Task<ServiceRegistrationDto> RejectAsync(
        int requestId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException("Chi chủ trọ/quản lý mới từ chối được yêu cầu.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Bắt buộc nhập lý do từ chối.");
        }

        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status != ServiceRegistrationStatus.PendingApproval)
        {
            throw new InvalidOperationException("Yêu cầu này không thể từ chối (đã xử lý trước đó).");
        }

        request.Status = ServiceRegistrationStatus.Rejected;
        request.ReviewedByUserId = caller.UserId;
        request.ReviewedAt = DateTimeOffset.UtcNow;
        request.RejectReason = reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        var tenantUserId = await TenantUserIdAsync(request.ContractId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tenantUserId))
        {
            await notifications.NotifyAsync(
                tenantUserId!,
                "Yêu cầu đăng ký dịch vụ bị từ chối",
                $"Dịch vụ {request.ServiceName}: {reason.Trim()}",
                "/my/service-registrations");
        }

        return await ToDtoAsync(request, cancellationToken);
    }

    /// <summary>
    /// Khách hủy đăng ký: chưa duyệt thì hủy ngay; đã duyệt thì gửi yêu cầu hủy kèm lý do
    /// để chủ trọ quyết. Chỉ chủ của hợp đồng mới được hủy.
    /// </summary>
    public async Task<ServiceRegistrationDto> CancelAsync(
        int requestId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsKhachThue)
        {
            throw new UnauthorizedAccessException("Chi khách thuê mới được hủy đăng ký của mình.");
        }

        var ownContractIds = await OwnContractIdsAsync(caller, cancellationToken);
        var request = await db.ServiceRegistrationRequests
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null || !ownContractIds.Contains(request.ContractId))
        {
            throw new InvalidOperationException("Không tìm thấy yêu cầu đăng ký của bạn.");
        }

        // Entity có thể đã bị chủ trọ xử lý ở context khác: nạp lại giá trị hiện tại.
        await db.Entry(request).ReloadAsync(cancellationToken);

        if (request.Status == ServiceRegistrationStatus.PendingApproval)
        {
            request.Status = ServiceRegistrationStatus.Cancelled;
            request.ReviewedByUserId = caller.UserId;
            request.ReviewedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return await ToDtoAsync(request, cancellationToken);
        }

        if (request.Status != ServiceRegistrationStatus.Approved)
        {
            throw new InvalidOperationException("Yêu cầu này không thể hủy.");
        }

        if (request.CancelStatus == ServiceRegistrationCancelStatus.Pending)
        {
            return await ToDtoAsync(request, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Đã duyệt thì phải nhập lý do hủy.");
        }

        request.CancelStatus = ServiceRegistrationCancelStatus.Pending;
        request.CancelReason = reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        var ownerId = await OwnerUserIdAsync(request.ContractId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            await notifications.NotifyAsync(
                ownerId!,
                "Khách thuê xin hủy đăng ký dịch vụ",
                $"Dịch vụ {request.ServiceName}: {reason.Trim()}",
                "/service-registrations");
        }

        return await ToDtoAsync(request, cancellationToken);
    }

    /// <summary>Chủ trọ đồng ý hủy đăng ký đã duyệt: gỡ khỏi P5, khách không phải đóng tiền dịch vụ này.</summary>
    public async Task<ServiceRegistrationDto> ApproveCancelAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException("Chi chủ trọ/quản lý mới duyệt được yêu cầu hủy.");
        }

        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status != ServiceRegistrationStatus.Approved
            || request.CancelStatus != ServiceRegistrationCancelStatus.Pending)
        {
            throw new InvalidOperationException("Không có yêu cầu hủy nào đang chờ duyệt.");
        }

        request.CancelStatus = ServiceRegistrationCancelStatus.Approved;
        request.CancelReviewedByUserId = caller.UserId;
        request.CancelReviewedAt = DateTimeOffset.UtcNow;
        request.Status = ServiceRegistrationStatus.Cancelled;

        var registration = await db.ContractServiceRegistrations
            .SingleOrDefaultAsync(
                item => item.ContractId == request.ContractId
                    && item.ServiceCatalogId == request.ServiceCatalogId
                    && item.MonthStart == new DateOnly(request.StartDate.Year, request.StartDate.Month, 1),
                cancellationToken);
        if (registration is not null)
        {
            db.ContractServiceRegistrations.Remove(registration);
        }

        await db.SaveChangesAsync(cancellationToken);

        var tenantUserId = await TenantUserIdAsync(request.ContractId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tenantUserId))
        {
            await notifications.NotifyAsync(
                tenantUserId!,
                "Yêu cầu hủy đăng ký đã được đồng ý",
                $"Dịch vụ {request.ServiceName} đã hủy. Bạn không phải đóng tiền dịch vụ này.",
                "/my/service-registrations");
        }

        return await ToDtoAsync(request, cancellationToken);
    }

    /// <summary>Chủ trọ không đồng ý hủy: phải có lý do; khách vẫn phải đóng tiền như bình thường.</summary>
    public async Task<ServiceRegistrationDto> RejectCancelAsync(
        int requestId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException("Chi chủ trọ/quản lý mới xử lý được yêu cầu hủy.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Bắt buộc nhập lý do không đồng ý hủy.");
        }

        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status != ServiceRegistrationStatus.Approved
            || request.CancelStatus != ServiceRegistrationCancelStatus.Pending)
        {
            throw new InvalidOperationException("Không có yêu cầu hủy nào đang chờ duyệt.");
        }

        request.CancelStatus = ServiceRegistrationCancelStatus.Rejected;
        request.CancelReviewedByUserId = caller.UserId;
        request.CancelReviewedAt = DateTimeOffset.UtcNow;
        request.CancelRejectReason = reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        var tenantUserId = await TenantUserIdAsync(request.ContractId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tenantUserId))
        {
            await notifications.NotifyAsync(
                tenantUserId!,
                "Yêu cầu hủy đăng ký không được đồng ý",
                $"Dịch vụ {request.ServiceName}: {reason.Trim()}",
                "/my/service-registrations");
        }

        return await ToDtoAsync(request, cancellationToken);
    }

    /// <summary>
    /// Tạo hóa đơn nháp từ yêu cầu đã duyệt. Trả về DTO hóa đơn.
    /// Chỉ chủ trọ/quản lý; yêu cầu chưa duyệt không được tạo hóa đơn.
    /// </summary>
    public async Task<InvoiceDto> CreateInvoiceAsync(
        int requestId,
        decimal amount,
        string content,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException("Chi chủ trọ/quản lý mới tạo được hóa đơn.");
        }

        if (amount <= 0m)
        {
            throw new InvalidOperationException("Số tiền hóa đơn phải lớn hơn 0.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Nội dung hóa đơn không được để trống.");
        }

        if (periodEnd < periodStart)
        {
            throw new InvalidOperationException("Ngày kết thúc kỳ phải bằng hoặc sau ngày bắt đầu.");
        }

        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status != ServiceRegistrationStatus.Approved)
        {
            throw new InvalidOperationException("Chỉ yêu cầu đã duyệt mới được tạo hóa đơn.");
        }

        if (request.InvoiceId is { } existingInvoiceId)
        {
            return await invoiceQuery.GetInvoiceAsync(existingInvoiceId, caller, cancellationToken);
        }

        var unitPrice = amount / Math.Max(request.Quantity, 0.0001m);
        var invoice = await invoices.CreateDraftAsync(
            new CreateInvoiceRequest
            {
                ContractId = request.ContractId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                DueDate = dueDate,
                RegistrationId = request.Id,
                Items =
                [
                    new InvoiceItemInputDto
                    {
                        ItemType = InvoiceItemType.Service,
                        Description = $"{request.ServiceName} ({content.Trim()})",
                        Unit = "lượt",
                        Quantity = request.Quantity,
                        UnitPrice = MoneyRound(unitPrice),
                        Amount = amount,
                    },
                ],
            },
            caller,
            cancellationToken);

        var entity = await db.Invoices
            .SingleAsync(item => item.Id == invoice.Id, cancellationToken);
        request.InvoiceId = invoice.Id;
        await db.SaveChangesAsync(cancellationToken);

        var tenantUserId = await TenantUserIdAsync(request.ContractId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tenantUserId))
        {
            await notifications.NotifyAsync(
                tenantUserId!,
                "Có hóa đơn mới",
                $"Hóa đơn HD-{invoice.Id} cho dịch vụ {request.ServiceName} đã được tạo.",
                $"/my/invoices/{invoice.Id}");
        }

        return await invoiceQuery.GetInvoiceAsync(invoice.Id, caller, cancellationToken);
    }

    private static decimal MoneyRound(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private async Task<ServiceRegistrationDto> ToDtoAsync(
        ServiceRegistrationRequest row,
        CancellationToken cancellationToken)
    {
        var contract = await db.Contracts
            .AsNoTracking()
            .Where(contract => contract.ContractId == row.ContractId)
            .Select(contract => new { contract.ContractNumber, contract.RoomId })
            .SingleOrDefaultAsync(cancellationToken);
        var room = contract is null
            ? null
            : await db.Rooms
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(room => room.Id == contract.RoomId)
                .Select(room => new { room.Code, room.PropertyId })
                .SingleOrDefaultAsync(cancellationToken);
        var propertyName = room is null
            ? string.Empty
            : await db.Properties
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(property => property.Id == room.PropertyId)
                .Select(property => property.Name)
                .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        var tenants = await db.ContractMembers
            .AsNoTracking()
            .Where(member => member.ContractId == row.ContractId)
            .Join(
                db.Tenants.IgnoreQueryFilters().AsNoTracking(),
                member => member.TenantId,
                tenant => tenant.Id,
                (_, tenant) => new { tenant.FullName, tenant.Phone })
            .ToListAsync(cancellationToken);

        return new ServiceRegistrationDto(
            row.Id,
            row.ContractId,
            contract?.ContractNumber ?? string.Empty,
            room?.Code ?? string.Empty,
            propertyName,
            tenants.Select(item => item.FullName).Distinct().ToArray(),
            tenants.Select(item => item.Phone).Distinct().ToArray(),
            row.ServiceCatalogId,
            row.ServiceName,
            row.Quantity,
            row.StartDate,
            row.EndDate,
            row.Note,
            row.Status,
            row.RequestedByUserId,
            row.RequestedAt,
            row.ReviewedByUserId,
            row.ReviewedAt,
            row.RejectReason,
            row.CancelStatus,
            row.CancelReason,
            row.CancelRejectReason,
            row.InvoiceId);
    }

    private async Task<IReadOnlyList<ServiceRegistrationDto>> LoadAsync(
        IReadOnlyList<Guid> contractIds,
        CancellationToken cancellationToken)
    {
        var rows = await db.ServiceRegistrationRequests
            .AsNoTracking()
            .Where(item => contractIds.Contains(item.ContractId))
            .OrderByDescending(item => item.RequestedAt)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var contracts = await db.Contracts
            .AsNoTracking()
            .Where(contract => contractIds.Contains(contract.ContractId))
            .Select(contract => new { contract.ContractId, contract.ContractNumber, contract.RoomId })
            .ToDictionaryAsync(contract => contract.ContractId, cancellationToken);
        var roomIds = contracts.Values.Select(contract => contract.RoomId).Distinct().ToList();
        var rooms = await db.Rooms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(room => roomIds.Contains(room.Id))
            .Select(room => new { room.Id, room.Code, room.PropertyId })
            .ToDictionaryAsync(room => room.Id, cancellationToken);
        var propertyIds = rooms.Values.Select(room => room.PropertyId).Distinct().ToList();
        var properties = await db.Properties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(property => propertyIds.Contains(property.Id))
            .Select(property => new { property.Id, property.Name })
            .ToDictionaryAsync(property => property.Id, cancellationToken);
        var tenantsByContract = await db.ContractMembers
            .AsNoTracking()
            .Where(member => contractIds.Contains(member.ContractId))
            .Join(
                db.Tenants.IgnoreQueryFilters().AsNoTracking(),
                member => member.TenantId,
                tenant => tenant.Id,
                (member, tenant) => new { member.ContractId, tenant.FullName, tenant.Phone })
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            contracts.TryGetValue(row.ContractId, out var contract);
            var room = contract is null ? null : rooms.GetValueOrDefault(contract.RoomId);
            var propertyName = room is null ? string.Empty : properties.GetValueOrDefault(room.PropertyId)?.Name ?? string.Empty;
            var tenants = tenantsByContract.Where(item => item.ContractId == row.ContractId).ToList();
            return new ServiceRegistrationDto(
                row.Id,
                row.ContractId,
                contract?.ContractNumber ?? string.Empty,
                room?.Code ?? string.Empty,
                propertyName,
                tenants.Select(item => item.FullName).Distinct().ToArray(),
                tenants.Select(item => item.Phone).Distinct().ToArray(),
                row.ServiceCatalogId,
                row.ServiceName,
                row.Quantity,
                row.StartDate,
                row.EndDate,
                row.Note,
                row.Status,
                row.RequestedByUserId,
                row.RequestedAt,
                row.ReviewedByUserId,
                row.ReviewedAt,
                row.RejectReason,
                row.CancelStatus,
                row.CancelReason,
                row.CancelRejectReason,
                row.InvoiceId);
        }).ToList();
    }

    private async Task<ServiceRegistrationRequest> LoadManageableAsync(
        int requestId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var propertyIds = PropertyScope.Apply(db.Properties, db.PropertyManagers, caller)
            .Select(property => property.Id);

        var request = await db.ServiceRegistrationRequests
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null)
        {
            throw new InvalidOperationException("Không tìm thấy yêu cầu đăng ký.");
        }

        // Entity có thể đã bị xử lý ở context khác: nạp lại trước khi kiểm tra trạng thái.
        await db.Entry(request).ReloadAsync(cancellationToken);

        var inScope = await db.Contracts
            .AsNoTracking()
            .Where(contract => contract.ContractId == request.ContractId)
            .Where(contract => db.Rooms.IgnoreQueryFilters().Any(room => room.Id == contract.RoomId
                && propertyIds.Contains(room.PropertyId)))
            .AnyAsync(cancellationToken);
        if (!inScope)
        {
            throw new UnauthorizedAccessException("Yêu cầu này nằm ngoài phạm vi quản lý của bạn.");
        }

        return request;
    }

    private async Task<IReadOnlyList<Guid>> OwnContractIdsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var memberContractIds = db.ContractMembers
            .AsNoTracking()
            .Join(
                db.Tenants.IgnoreQueryFilters().AsNoTracking(),
                member => member.TenantId,
                tenant => tenant.Id,
                (member, tenant) => new { member.ContractId, tenant.UserId })
            .Where(item => item.UserId == caller.UserId)
            .Select(item => (Guid?)item.ContractId);

        return await db.Contracts
            .AsNoTracking()
            .Where(contract => contract.Status == ContractStatus.Approved
                && memberContractIds.Contains(contract.ContractId))
            .Select(contract => contract.ContractId)
            .ToListAsync(cancellationToken);
    }

    private async Task<string?> TenantUserIdAsync(Guid contractId, CancellationToken cancellationToken) =>
        await db.ContractMembers
            .AsNoTracking()
            .Where(member => member.ContractId == contractId)
            .Join(
                db.Tenants.IgnoreQueryFilters().AsNoTracking(),
                member => member.TenantId,
                tenant => tenant.Id,
                (_, tenant) => tenant.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<string?> OwnerUserIdAsync(Guid contractId, CancellationToken cancellationToken) =>
        await db.Contracts
            .AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Join(
                db.Rooms.IgnoreQueryFilters().AsNoTracking(),
                contract => contract.RoomId,
                room => room.Id,
                (contract, room) => room.PropertyId)
            .Join(
                db.Properties.IgnoreQueryFilters().AsNoTracking(),
                propertyId => propertyId,
                property => property.Id,
                (_, property) => property.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<string> CurrentServiceNameAsync(
        ServiceCatalog catalog,
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var name = await context.ServicePriceHistories
            .AsNoTracking()
            .Where(version => version.ServiceCatalogId == catalog.Id)
            .OrderByDescending(version => version.EffectiveDate)
            .Select(version => version.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return name ?? catalog.Id.ToString();
    }
}
