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
using Web.Infrastructure.Time;

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

public static class ServiceRegistrationErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string NoActiveContract = nameof(NoActiveContract);
    public const string ContractNotOwned = nameof(ContractNotOwned);
    public const string CannotRegister = nameof(CannotRegister);
    public const string AlreadyRegistered = nameof(AlreadyRegistered);
    public const string RequestNotFound = nameof(RequestNotFound);
    public const string InvalidState = nameof(InvalidState);
    public const string ReasonRequired = nameof(ReasonRequired);
    public const string PriceNotFound = nameof(PriceNotFound);
    public const string OutsideScope = nameof(OutsideScope);
    public const string EmptyContent = nameof(EmptyContent);
}

public sealed class ServiceRegistrationException(string message, string errorCode)
    : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>Hợp đồng hiệu lực của khách để chọn khi gửi yêu cầu đăng ký dịch vụ.</summary>
public sealed record ServiceRegistrationContractOptionDto(
    Guid ContractId,
    string ContractNumber,
    string RoomCode,
    string PropertyName);

/// <summary>Thông tin hóa đơn sẽ tạo từ yêu cầu đã duyệt, tính theo bảng giá thật.</summary>
public sealed record ServiceRegistrationInvoicePreviewDto(
    string ServiceName,
    decimal Quantity,
    decimal UnitPrice,
    string Unit,
    decimal Amount,
    DateOnly PeriodStart,
    DateOnly PeriodEnd);

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
    ILogger<ServiceRegistrationService> logger,
    TimeProvider clock)
{
    // Blazor có thể khởi tạo layout + page song song trên cùng circuit; DbContext
    // là scoped nên phải xếp hàng mọi thao tác (giống MonthlyUtilityCalculationPageService).
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    private static readonly string[] PerPersonUnits = ["người", "nguoi", "person"];

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

    /// <summary>Hợp đồng hiệu lực của chính khách thuê, dùng cho ô chọn hợp đồng khi đăng ký dịch vụ.</summary>
    public async Task<IReadOnlyList<ServiceRegistrationContractOptionDto>> GetMyContractOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsKhachThue)
        {
            throw new UnauthorizedAccessException("Chi khách thuê mới được đăng ký dịch vụ.");
        }

        var contractIds = await OwnContractIdsAsync(caller, cancellationToken, activeOnly: true);
        if (contractIds.Count == 0)
        {
            return [];
        }

        var contracts = await db.Contracts
            .AsNoTracking()
            .Where(contract => contractIds.Contains(contract.ContractId))
            .Select(contract => new
            {
                contract.ContractId,
                contract.ContractNumber,
                contract.RoomId,
            })
            .ToListAsync(cancellationToken);

        var roomIds = contracts.Select(item => item.RoomId).Distinct().ToList();
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

        return contracts
            .Select(item =>
            {
                var room = rooms.GetValueOrDefault(item.RoomId);
                var propertyName = room is null
                    ? string.Empty
                    : properties.GetValueOrDefault(room.PropertyId)?.Name ?? string.Empty;
                return new ServiceRegistrationContractOptionDto(
                    item.ContractId,
                    item.ContractNumber,
                    room?.Code ?? string.Empty,
                    propertyName);
            })
            .OrderBy(item => item.PropertyName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.RoomCode, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<ServiceRegistrationDto> RequestAsync(
        int serviceCatalogId,
        Guid contractId,
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
            throw new ServiceRegistrationException(
                "Dịch vụ này không thể đăng ký.",
                ServiceRegistrationErrorCodes.CannotRegister);
        }

        var contractIds = await OwnContractIdsAsync(caller, cancellationToken, activeOnly: true);
        if (contractIds.Count == 0)
        {
            throw new ServiceRegistrationException(
                "Không tìm thấy hợp đồng hiệu lực của bạn.",
                ServiceRegistrationErrorCodes.NoActiveContract);
        }

        // Chỉ được gửi cho hợp đồng của chính mình: không tự áp dịch vụ cho mọi phòng đang thuê.
        if (!contractIds.Contains(contractId))
        {
            throw new ServiceRegistrationException(
                "Hợp đồng đã chọn không thuộc về bạn.",
                ServiceRegistrationErrorCodes.ContractNotOwned);
        }

        // Dịch vụ gắn với một tòa chỉ đăng ký được cho hợp đồng ở đúng tòa đó.
        if (catalog.PropertyId is int servicePropertyId)
        {
            var contractPropertyId = await db.Contracts
                .AsNoTracking()
                .Where(contract => contract.ContractId == contractId)
                .Join(
                    db.Rooms.AsNoTracking(),
                    contract => contract.RoomId,
                    room => room.Id,
                    (contract, room) => (int?)room.PropertyId)
                .SingleOrDefaultAsync(cancellationToken);
            if (contractPropertyId != servicePropertyId)
            {
                throw new ServiceRegistrationException(
                    "Dịch vụ này không thuộc tòa nhà của hợp đồng đã chọn.",
                    ServiceRegistrationErrorCodes.CannotRegister);
            }
        }

        // Dịch vụ theo tháng: 1 dịch vụ chỉ đăng ký 1 lần trong cùng một tháng cho cùng hợp đồng.
        var currentMonth = clock.TodayVietnam();
        var monthStart = new DateOnly(currentMonth.Year, currentMonth.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthlyDuplicate = await db.ServiceRegistrationRequests
            .AsNoTracking()
            .AnyAsync(
                item => item.Status != ServiceRegistrationStatus.Rejected
                    && item.Status != ServiceRegistrationStatus.Cancelled
                    && item.ServiceCatalogId == serviceCatalogId
                    && item.StartDate.Year == monthStart.Year
                    && item.StartDate.Month == monthStart.Month
                    && item.ContractId == contractId,
                cancellationToken);
        if (monthlyDuplicate)
        {
            throw new ServiceRegistrationException(
                "Dịch vụ này đã được đăng ký trong tháng này.",
                ServiceRegistrationErrorCodes.AlreadyRegistered);
        }

        var row = new ServiceRegistrationRequest
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
        };

        db.ServiceRegistrationRequests.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Tenant {UserId} requested service {ServiceId} for contract {ContractId}",
            caller.UserId,
            serviceCatalogId,
            contractId);

        var ownerId = await OwnerUserIdAsync(contractId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            await notifications.NotifyAsync(
                ownerId!,
                "Yêu cầu đăng ký dịch vụ mới",
                $"Khách thuê vừa đăng ký dịch vụ {row.ServiceName}.",
                "/service-registrations");
        }

        return await ToDtoAsync(row, cancellationToken);
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
            throw new ServiceRegistrationException(
                "Yêu cầu này không thể duyệt (đã xử lý trước đó).",
                ServiceRegistrationErrorCodes.InvalidState);
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
            throw new ServiceRegistrationException(
                "Bắt buộc nhập lý do từ chối.",
                ServiceRegistrationErrorCodes.ReasonRequired);
        }

        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status != ServiceRegistrationStatus.PendingApproval)
        {
            throw new ServiceRegistrationException(
                "Yêu cầu này không thể từ chối (đã xử lý trước đó).",
                ServiceRegistrationErrorCodes.InvalidState);
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
            throw new ServiceRegistrationException(
                "Không tìm thấy yêu cầu đăng ký của bạn.",
                ServiceRegistrationErrorCodes.RequestNotFound);
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
            throw new ServiceRegistrationException(
                "Yêu cầu này không thể hủy.",
                ServiceRegistrationErrorCodes.InvalidState);
        }

        if (request.CancelStatus == ServiceRegistrationCancelStatus.Pending)
        {
            return await ToDtoAsync(request, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ServiceRegistrationException(
                "Đã duyệt thì phải nhập lý do hủy.",
                ServiceRegistrationErrorCodes.ReasonRequired);
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
            throw new ServiceRegistrationException(
                "Không có yêu cầu hủy nào đang chờ duyệt.",
                ServiceRegistrationErrorCodes.InvalidState);
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
            registration.IsDeleted = true;
            registration.DeletedAt = DateTime.UtcNow;
            registration.DeletedBy = caller.UserId;
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
            throw new ServiceRegistrationException(
                "Bắt buộc nhập lý do không đồng ý hủy.",
                ServiceRegistrationErrorCodes.ReasonRequired);
        }

        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status != ServiceRegistrationStatus.Approved
            || request.CancelStatus != ServiceRegistrationCancelStatus.Pending)
        {
            throw new ServiceRegistrationException(
                "Không có yêu cầu hủy nào đang chờ duyệt.",
                ServiceRegistrationErrorCodes.InvalidState);
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
    /// Tính trước hóa đơn từ yêu cầu đã duyệt: đơn giá lấy từ bảng giá thật của dịch vụ,
    /// kỳ là đúng tháng của yêu cầu. UI chỉ hiển thị, không tự nhập số tiền.
    /// </summary>
    public async Task<ServiceRegistrationInvoicePreviewDto> PreviewInvoiceAsync(
        int requestId,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException("Chi chủ trọ/quản lý mới xem được hóa đơn dịch vụ.");
        }

        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status != ServiceRegistrationStatus.Approved)
        {
            throw new ServiceRegistrationException(
                "Chỉ yêu cầu đã duyệt mới được tạo hóa đơn.",
                ServiceRegistrationErrorCodes.InvalidState);
        }

        return await BuildInvoicePreviewAsync(request, cancellationToken);
    }

    /// <summary>
    /// Tạo hóa đơn nháp từ yêu cầu đã duyệt. Số tiền và kỳ do server tính từ bảng giá,
    /// không nhận số tiền tùy ý từ UI. Chỉ chủ trọ/quản lý; yêu cầu chưa duyệt bị chặn.
    /// </summary>
    public async Task<InvoiceDto> CreateInvoiceAsync(
        int requestId,
        string content,
        DateOnly dueDate,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException("Chi chủ trọ/quản lý mới tạo được hóa đơn.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ServiceRegistrationException(
                "Nội dung hóa đơn không được để trống.",
                ServiceRegistrationErrorCodes.EmptyContent);
        }

        var request = await LoadManageableAsync(requestId, caller, cancellationToken);
        if (request.Status != ServiceRegistrationStatus.Approved)
        {
            throw new ServiceRegistrationException(
                "Chỉ yêu cầu đã duyệt mới được tạo hóa đơn.",
                ServiceRegistrationErrorCodes.InvalidState);
        }

        if (request.InvoiceId is { } existingInvoiceId)
        {
            return await invoiceQuery.GetInvoiceAsync(existingInvoiceId, caller, cancellationToken);
        }

        var preview = await BuildInvoicePreviewAsync(request, cancellationToken);
        var invoice = await invoices.CreateDraftAsync(
            new CreateInvoiceRequest
            {
                ContractId = request.ContractId,
                PeriodStart = preview.PeriodStart,
                PeriodEnd = preview.PeriodEnd,
                DueDate = dueDate,
                RegistrationId = request.Id,
                Items =
                [
                    new InvoiceItemInputDto
                    {
                        ItemType = InvoiceItemType.Service,
                        Description = $"{request.ServiceName} ({content.Trim()})",
                        Unit = preview.Unit,
                        Quantity = preview.Quantity,
                        UnitPrice = preview.UnitPrice,
                        Amount = preview.Amount,
                    },
                ],
            },
            caller,
            cancellationToken);

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

    private async Task<ServiceRegistrationInvoicePreviewDto> BuildInvoicePreviewAsync(
        ServiceRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var periodStart = new DateOnly(request.StartDate.Year, request.StartDate.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        // Kỳ hóa đơn không được vượt ra ngoài thời hạn hợp đồng (hợp đồng bắt đầu/kết thúc giữa tháng).
        var contractRange = await db.Contracts
            .AsNoTracking()
            .Where(contract => contract.ContractId == request.ContractId)
            .Select(contract => new { contract.StartDate, contract.EndDate })
            .FirstOrDefaultAsync(cancellationToken);
        if (contractRange is not null)
        {
            var contractStart = DateOnly.FromDateTime(contractRange.StartDate);
            var contractEnd = DateOnly.FromDateTime(contractRange.EndDate);
            if (periodStart < contractStart)
            {
                periodStart = contractStart;
            }

            if (periodEnd > contractEnd)
            {
                periodEnd = contractEnd;
            }
        }

        // Đơn giá hiệu lực tại ngày bắt đầu kỳ; nếu bảng giá không ghi ngày kết thúc thì vẫn còn hiệu lực.
        var price = await db.ServicePriceHistories
            .AsNoTracking()
            .Where(version => version.ServiceCatalogId == request.ServiceCatalogId
                && version.EffectiveDate <= request.StartDate
                && (version.EffectiveTo == null || version.EffectiveTo >= request.StartDate))
            .OrderByDescending(version => version.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await db.ServicePriceHistories
                .AsNoTracking()
                .Where(version => version.ServiceCatalogId == request.ServiceCatalogId)
                .OrderByDescending(version => version.EffectiveDate)
                .FirstOrDefaultAsync(cancellationToken);

        if (price is null || price.UnitPrice <= 0m)
        {
            throw new ServiceRegistrationException(
                "Dịch vụ chưa có đơn giá hiệu lực.",
                ServiceRegistrationErrorCodes.PriceNotFound);
        }

        var quantity = request.Quantity <= 0m ? 1m : request.Quantity;
        // Dịch vụ tính theo đầu người phải nhân với số cư dân của hợp đồng, giống hóa đơn P5;
        // request.Quantity luôn là 1 nên nếu giữ nguyên sẽ thu thiếu tiền.
        if (IsPerPersonUnit(price.Unit))
        {
            var residents = await db.ContractMembers
                .CountAsync(member => member.ContractId == request.ContractId, cancellationToken);
            quantity = Math.Max(1, residents);
        }

        return new ServiceRegistrationInvoicePreviewDto(
            request.ServiceName,
            quantity,
            price.UnitPrice,
            string.IsNullOrWhiteSpace(price.Unit) ? "lượt" : price.Unit,
            MoneyRound(price.UnitPrice * quantity),
            periodStart,
            periodEnd);
    }

    private static decimal MoneyRound(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool IsPerPersonUnit(string unit) =>
        Array.Exists(PerPersonUnits, candidate =>
            string.Equals(candidate, unit.Trim(), StringComparison.OrdinalIgnoreCase));

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
            throw new ServiceRegistrationException(
                "Không tìm thấy yêu cầu đăng ký.",
                ServiceRegistrationErrorCodes.RequestNotFound);
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
        CancellationToken cancellationToken,
        bool activeOnly = false)
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

        var query = db.Contracts
            .AsNoTracking()
            .Where(contract => contract.Status == ContractStatus.Approved
                && memberContractIds.Contains(contract.ContractId));

        if (activeOnly)
        {
            // ContractStatus không được ghi Expired; hiệu lực phải suy ra từ ngày.
            var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
            var tomorrow = today.AddDays(1);
            query = query.Where(contract => contract.StartDate < tomorrow && contract.EndDate >= today);
        }

        return await query
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
