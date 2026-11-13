using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Web.Common.Models;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Audit.Entities;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.Entities;
using Web.Infrastructure.Time;

namespace Web.Features.Contracts.Services;

public sealed class ContractService : IContractService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IManagerTogglePolicy? _togglePolicy;
    private readonly IManagerApprovalService? _approvals;

    public ContractService(
        ApplicationDbContext dbContext,
        TimeProvider? timeProvider = null,
        IManagerTogglePolicy? togglePolicy = null,
        IManagerApprovalService? approvals = null)
    {
        _context = dbContext;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _togglePolicy = togglePolicy;
        _approvals = approvals;
    }

    public async Task<ContractCreateOptionsDto> GetCreateOptionsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanCreate(caller);

        var visibleProperties = PropertyScope.Apply(
            _context.Properties.AsNoTracking(),
            _context.PropertyManagers.AsNoTracking(),
            caller);

        var properties = await visibleProperties
            .Where(property => property.IsActive && !property.IsDeleted)
            .OrderBy(property => property.Name)
            .Select(property => new ContractPropertyOptionDto(property.Id, property.Name, property.Address))
            .ToListAsync(cancellationToken);

        // Phòng còn giữ trạng thái Available cho tới khi hợp đồng được duyệt, nên phải loại
        // cả phòng đang có hợp đồng Pending/Approved để người chọn không bấm vào rồi mới bị chặn.
        var busyRoomIds = _context.Contracts.AsNoTracking()
            .Where(contract => contract.Status == ContractStatus.Pending
                || contract.Status == ContractStatus.Approved
                || contract.Status == ContractStatus.AwaitingTenant
                || contract.Status == ContractStatus.ChangeRequested
                || contract.Status == ContractStatus.TenantAccepted)
            .Select(contract => contract.RoomId);

        var rooms = await (
            from room in _context.Rooms.AsNoTracking()
            join property in visibleProperties on room.PropertyId equals property.Id
            where room.IsActive && !room.IsDeleted && room.Status == RoomStatus.Available
                && !busyRoomIds.Contains(room.Id)
            orderby property.Name, room.Code
            select new ContractRoomOptionDto(
                room.Id,
                property.Id,
                room.Code,
                room.StandardPrice,
                room.Floor,
                room.AreaM2))
            .ToListAsync(cancellationToken);

        // VHT-119: danh sách chọn khách dùng chung phạm vi với danh sách hồ sơ,
        // không lộ tên/SĐT khách của chủ trọ khác.
        var tenantRows = await Web.Features.Tenants.TenantVisibility
            .Apply(_context, caller.UserId, caller.Role)
            .AsNoTracking()
            .Where(tenant => tenant.IsActive && !tenant.IsDeleted)
            .OrderBy(tenant => tenant.FullName)
            .Select(tenant => new ContractTenantRow(
                tenant.Id,
                tenant.FullName,
                tenant.Phone,
                tenant.Email,
                tenant.CitizenId,
                tenant.PermanentAddress,
                tenant.IdIssuedPlace,
                tenant.DateOfBirth))
            .ToListAsync(cancellationToken);

        // CCCD đầy đủ chỉ trả cho Chủ trọ; Quản lý chỉ thấy dạng che để lập hợp đồng.
        var tenants = tenantRows
            .Select(tenant => new ContractTenantOptionDto(
                tenant.Id,
                tenant.FullName,
                tenant.Phone,
                tenant.Email,
                caller.IsChuTro ? tenant.CitizenId : MaskCitizenId(tenant.CitizenId),
                tenant.PermanentAddress,
                tenant.IdIssuedPlace,
                tenant.DateOfBirth))
            .ToList();

        var services = await LoadServiceOptionsAsync(
            properties.Select(property => property.Id).ToList(),
            _timeProvider.TodayVietnam(),
            cancellationToken);

        return new ContractCreateOptionsDto(properties, rooms, tenants, services);
    }

    /// <summary>CCCD dạng che cho Quản lý: chỉ giữ 4 số cuối.</summary>
    private static string? MaskCitizenId(string? citizenId)
    {
        if (string.IsNullOrWhiteSpace(citizenId))
        {
            return citizenId;
        }

        var trimmed = citizenId.Trim();
        return trimmed.Length <= 4 ? new string('*', trimmed.Length) : $"****{trimmed[^4..]}";
    }

    private static Dictionary<int, string> NormalizeRelationships(
        IReadOnlyDictionary<int, string>? relations,
        IReadOnlyList<int> tenantIds)
    {
        var result = new Dictionary<int, string>();
        if (relations is null)
        {
            return result;
        }

        foreach (var tenantId in tenantIds)
        {
            if (!relations.TryGetValue(tenantId, out var relationship)
                || string.IsNullOrWhiteSpace(relationship))
            {
                continue;
            }

            var trimmed = relationship.Trim();
            if (trimmed.Length > 50)
            {
                throw new ContractValidationException(
                    ContractErrorMessages.RelationshipTooLong,
                    ContractErrorCodes.RelationshipTooLong,
                    "MemberRelations");
            }

            result[tenantId] = trimmed;
        }

        return result;
    }

    private async Task<IReadOnlyList<ContractServiceOptionDto>> LoadServiceOptionsAsync(
        IReadOnlyList<int> propertyIds,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var catalogs = await _context.ServiceCatalogs.AsNoTracking()
            .Where(catalog => catalog.IsActive
                && (catalog.PropertyId == null || propertyIds.Contains(catalog.PropertyId.Value)))
            .Select(catalog => new { catalog.Id, catalog.PropertyId })
            .ToListAsync(cancellationToken);

        if (catalogs.Count == 0)
        {
            return [];
        }

        var catalogIds = catalogs.Select(catalog => catalog.Id).ToList();
        var prices = await _context.ServicePriceHistories.AsNoTracking()
            .Where(price => catalogIds.Contains(price.ServiceCatalogId) && price.EffectiveDate <= today)
            .OrderByDescending(price => price.EffectiveDate)
            .ThenByDescending(price => price.Id)
            .Select(price => new
            {
                price.ServiceCatalogId,
                price.Name,
                price.Unit,
                price.CalculationMethod,
                price.UnitPrice,
                price.EffectiveTo,
            })
            .ToListAsync(cancellationToken);

        var currentByCatalog = prices
            .GroupBy(price => price.ServiceCatalogId)
            .ToDictionary(group => group.Key, group => group.First());

        return catalogs
            .Select(catalog => (catalog, price: currentByCatalog.GetValueOrDefault(catalog.Id)))
            .Where(item => item.price is not null
                && (item.price.EffectiveTo is null || item.price.EffectiveTo >= today))
            // Dịch vụ dùng chung (điện/nước) đứng trước, sau đó theo tên.
            .OrderBy(item => item.catalog.PropertyId.HasValue ? 1 : 0)
            .ThenBy(item => item.price!.Name)
            .Select(item => new ContractServiceOptionDto(
                item.catalog.Id,
                item.catalog.PropertyId,
                item.price!.Name,
                item.price.Unit,
                item.price.CalculationMethod,
                item.price.UnitPrice))
            .ToList();
    }

    public async Task<IReadOnlyList<ContractListItemDto>> ListAsync(
        string? keyword,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanView(caller);

        var query = ApplyKeyword(ApplyCallerScope(_context.Contracts.AsNoTracking(), caller), keyword);

        var rows = await (
            from contract in query
            join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
            join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
            orderby contract.StartDate descending, contract.ContractNumber
            select new ContractListRow(
                contract.ContractId,
                contract.ContractNumber,
                property.Name,
                room.Code,
                contract.StartDate,
                contract.EndDate,
                contract.SnapshotPrice,
                contract.DepositAmount,
                contract.Status))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var contractIds = rows.Select(row => row.ContractId).ToList();
        var members = await (
            from member in _context.ContractMembers.AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
            where contractIds.Contains(member.ContractId)
            orderby tenant.FullName
            select new ContractMemberRow(member.ContractId, tenant.FullName, tenant.Phone))
            .ToListAsync(cancellationToken);

        var namesByContract = members.GroupBy(member => member.ContractId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(member => member.FullName).ToList());

        return rows.Select(row => new ContractListItemDto(
            row.ContractId,
            row.ContractNumber,
            row.PropertyName,
            row.RoomCode,
            row.StartDate,
            row.EndDate,
            row.SnapshotPrice,
            row.DepositAmount,
            row.Status,
            namesByContract.GetValueOrDefault(row.ContractId, [])))
            .ToList();
    }

    private IQueryable<Contract> ApplyKeyword(IQueryable<Contract> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var term = keyword.Trim();
        return query.Where(contract =>
            contract.ContractNumber.Contains(term)
            || _context.Rooms.IgnoreQueryFilters().Any(room =>
                room.Id == contract.RoomId && room.Code.Contains(term))
            || _context.Properties.IgnoreQueryFilters().Any(property =>
                _context.Rooms.IgnoreQueryFilters().Any(room =>
                    room.Id == contract.RoomId && room.PropertyId == property.Id)
                && property.Name.Contains(term))
            || _context.ContractMembers.Any(member => member.ContractId == contract.ContractId
                && _context.Tenants.Any(tenant => tenant.Id == member.TenantId && tenant.FullName.Contains(term))));
    }

    public async Task<ContractListPageDto> GetListPageAsync(
        ContractListFilter filter,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanView(caller);
        ArgumentNullException.ThrowIfNull(filter);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var today = _timeProvider.TodayVietnam();
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var expiringEnd = today.AddDays(ContractStatusPresentation.ExpiringSoonDays).ToDateTime(TimeOnly.MinValue);

        var scoped = ApplyCallerScope(_context.Contracts.AsNoTracking(), caller);
        // Dùng 4 COUNT đơn giản thay vì GroupBy có điều kiện ngày: dễ dịch SQL và
        // không làm trang kẹt khi provider không tối ưu được aggregate phức tạp.
        var totalContracts = await scoped.CountAsync(cancellationToken);
        var activeContracts = await scoped.CountAsync(contract => contract.Status == ContractStatus.Approved
            && contract.StartDate <= todayStart && contract.EndDate > expiringEnd, cancellationToken);
        var expiringContracts = await scoped.CountAsync(contract => contract.Status == ContractStatus.Approved
            && contract.EndDate >= todayStart && contract.EndDate <= expiringEnd, cancellationToken);
        var pendingContracts = await scoped.CountAsync(
            contract => contract.Status == ContractStatus.Pending, cancellationToken);
        var pendingDepositContracts = await scoped.CountAsync(contract =>
            contract.Status == ContractStatus.Approved
            && contract.DepositPaidAt == null
            && _context.ContractDepositPayments.Any(payment =>
                payment.ContractId == contract.ContractId
                && payment.Status == DepositPaymentStatus.Pending), cancellationToken);
        var stats = new ContractListStatsDto(
            totalContracts, activeContracts, expiringContracts, pendingContracts, pendingDepositContracts);

        var query = ApplyKeyword(scoped, filter.Keyword);
        if (!string.IsNullOrWhiteSpace(filter.PropertyName))
        {
            var propertyName = filter.PropertyName;
            query = query.Where(contract => _context.Rooms.IgnoreQueryFilters().Any(room =>
                room.Id == contract.RoomId
                && _context.Properties.IgnoreQueryFilters().Any(property =>
                    property.Id == room.PropertyId && property.Name == propertyName)));
        }

        if (filter.TermFrom is { } termFrom)
        {
            query = query.Where(contract => contract.EndDate >= termFrom.Date);
        }

        if (filter.TermTo is { } termTo)
        {
            query = query.Where(contract => contract.StartDate <= termTo.Date);
        }

        if (filter.MinRent is { } minRent)
        {
            query = query.Where(contract => contract.SnapshotPrice >= minRent);
        }

        if (filter.MaxRent is { } maxRent)
        {
            query = query.Where(contract => contract.SnapshotPrice <= maxRent);
        }

        query = filter.StatusKey switch
        {
            "draft" => query.Where(contract => contract.Status == ContractStatus.Draft),
            "awaiting-tenant" => query.Where(contract => contract.Status == ContractStatus.AwaitingTenant),
            "change-requested" => query.Where(contract => contract.Status == ContractStatus.ChangeRequested),
            "tenant-accepted" => query.Where(contract => contract.Status == ContractStatus.TenantAccepted),
            "cancelled" => query.Where(contract => contract.Status == ContractStatus.Cancelled),
            "pending" => query.Where(contract => contract.Status == ContractStatus.Pending),
            "deposit-pending" => query.Where(contract => contract.Status == ContractStatus.Approved
                && contract.DepositPaidAt == null
                && _context.ContractDepositPayments.Any(payment =>
                    payment.ContractId == contract.ContractId
                    && payment.Status == DepositPaymentStatus.Pending)),
            "upcoming" => query.Where(contract => contract.Status == ContractStatus.Approved
                && contract.StartDate > todayStart),
            "active" => query.Where(contract => contract.Status == ContractStatus.Approved
                && contract.StartDate <= todayStart && contract.EndDate > expiringEnd),
            "expiring" => query.Where(contract => contract.Status == ContractStatus.Approved
                && contract.EndDate >= todayStart && contract.EndDate <= expiringEnd),
            "expired" => query.Where(contract => contract.Status == ContractStatus.Approved
                && contract.EndDate < todayStart),
            "liquidated" => query.Where(contract => contract.Status == ContractStatus.Liquidated),
            "rejected" => query.Where(contract => contract.Status == ContractStatus.Rejected),
            _ => query,
        };

        var total = await query.CountAsync(cancellationToken);
        var ordered = filter.Sort switch
        {
            "expiring" => query.OrderBy(contract => contract.EndDate)
                .ThenBy(contract => contract.RoomId).ThenBy(contract => contract.ContractId),
            "room" => query.OrderBy(contract => contract.RoomId)
                .ThenBy(contract => contract.ContractId),
            // "Mới nhất" theo ngày tạo bản ghi; hợp đồng tương lai không còn tự nhảy lên đầu.
            _ => query.OrderByDescending(contract => contract.CreatedAt)
                .ThenByDescending(contract => contract.StartDate)
                .ThenBy(contract => contract.ContractId),
        };
        var skip = (page - 1) * pageSize;
        var items = await LoadListItemsAsync(ordered, pageSize, cancellationToken, skip);

        return new ContractListPageDto(items, total, stats);
    }

    public async Task<IReadOnlyList<string>> GetPropertyNamesAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanView(caller);
        return await (
            from contract in ApplyCallerScope(_context.Contracts.AsNoTracking(), caller)
            join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
            join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
            select property.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContractHomeSnapshotDto> GetHomeSnapshotAsync(
        PropertyCaller caller,
        DateOnly today,
        int expiringDays = 30,
        int take = 5,
        CancellationToken cancellationToken = default)
    {
        EnsureCanView(caller);
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var expiringEnd = today.AddDays(expiringDays).ToDateTime(TimeOnly.MinValue);
        var scoped = ApplyCallerScope(_context.Contracts.AsNoTracking(), caller);
        var expiringQuery = scoped.Where(contract =>
            contract.Status == ContractStatus.Approved
            && contract.EndDate >= todayStart
            && contract.EndDate <= expiringEnd);

        var expiringCount = await expiringQuery.CountAsync(cancellationToken);
        var expiring = await LoadListItemsAsync(
            expiringQuery.OrderBy(contract => contract.EndDate), take, cancellationToken);

        IReadOnlyList<ContractListItemDto> activeForTenant = [];
        if (caller.IsKhachThue)
        {
            activeForTenant = await LoadListItemsAsync(
                scoped.Where(contract => contract.Status == ContractStatus.Approved && contract.EndDate >= todayStart)
                    .OrderBy(contract => contract.EndDate),
                50, cancellationToken);
        }

        return new ContractHomeSnapshotDto(expiring, expiringCount, activeForTenant);
    }

    private async Task<List<ContractListItemDto>> LoadListItemsAsync(
        IQueryable<Contract> query, int take, CancellationToken cancellationToken, int skip = 0)
    {
        var rows = await (
            from contract in query
            join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
            join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
            select new ContractListRow(
                contract.ContractId,
                contract.ContractNumber,
                property.Name,
                room.Code,
                contract.StartDate,
                contract.EndDate,
                contract.SnapshotPrice,
                contract.DepositAmount,
                contract.Status))
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var contractIds = rows.Select(row => row.ContractId).ToList();
        var members = await (
            from member in _context.ContractMembers.AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
            where contractIds.Contains(member.ContractId)
            orderby tenant.FullName
            select new ContractMemberRow(member.ContractId, tenant.FullName, tenant.Phone))
            .ToListAsync(cancellationToken);
        var namesByContract = members.GroupBy(member => member.ContractId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(member => member.FullName).ToList());

        return rows.Select(row => new ContractListItemDto(
            row.ContractId,
            row.ContractNumber,
            row.PropertyName,
            row.RoomCode,
            row.StartDate,
            row.EndDate,
            row.SnapshotPrice,
            row.DepositAmount,
            row.Status,
            namesByContract.GetValueOrDefault(row.ContractId, [])))
            .ToList();
    }

    public async Task<ContractDetailDto> GetAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanView(caller);

        var contract = await ApplyCallerScope(_context.Contracts.AsNoTracking(), caller)
            .Where(item => item.ContractId == contractId)
            .Select(item => new
            {
                item.ContractId,
                item.ContractNumber,
                item.RoomId,
                item.StartDate,
                item.EndDate,
                item.SnapshotPrice,
                item.DepositAmount,
                item.Status,
                item.RejectReason,
                item.PaymentCycleMonths,
                item.MeterReadingDay,
                item.Terms,
                item.DepositPaidAmount,
                item.DepositPaidAt,
                item.DepositDueAt,
                item.DepositRefundAmount,
                item.DepositRefundedAt,
                Room = _context.Rooms.IgnoreQueryFilters().Where(room => room.Id == item.RoomId)
                    .Select(room => new { room.Code, room.PropertyId, room.Floor, room.AreaM2 }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (contract?.Room is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound,
                ContractErrorCodes.ContractNotFound);
        }

        var property = await _context.Properties.IgnoreQueryFilters()
            .Where(item => item.Id == contract.Room.PropertyId)
            .Select(item => new { item.Name, item.Address })
            .SingleOrDefaultAsync(cancellationToken);

        if (property is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound,
                ContractErrorCodes.ContractNotFound);
        }

        var members = await (
            from member in _context.ContractMembers.AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
            where member.ContractId == contractId
            orderby member.IsRepresentative descending, tenant.FullName
            select new ContractMemberListItemDto(
                tenant.Id,
                tenant.FullName,
                caller.IsKhachThue && tenant.UserId != caller.UserId ? string.Empty : tenant.Phone,
                member.Relationship,
                member.IsRepresentative))
            .ToListAsync(cancellationToken);

        var services = await ContractServiceLineLoader.LoadAsync(
            _context,
            contract.Room.PropertyId,
            contractId,
            DateOnly.FromDateTime(contract.StartDate),
            cancellationToken);

        var canRenew = false;
        var canLiquidate = false;
        var canEditDeposit = false;
        var pendingApproval = ContractPendingApproval.None;
        var today = _timeProvider.TodayVietnam();
        ContractLinkDto? successor = null;
        ContractLinkDto? previous = null;
        if (caller.IsChuTro || caller.IsQuanLy)
        {
            successor = await FindLinkAsync(contract.RoomId, contract.ContractId, contract.EndDate.AddDays(1), true, cancellationToken);
            previous = await FindLinkAsync(contract.RoomId, contract.ContractId, contract.StartDate.AddDays(-1), false, cancellationToken);
            var pending = await _context.ManagerApprovalRequests.AsNoTracking()
                .Where(x => x.TargetType == ApprovalTargetType.Contract
                    && x.TargetKey == contract.ContractId.ToString("D")
                    && x.Status == ApprovalRequestStatus.Pending)
                .OrderByDescending(x => x.RequestedAt)
                .Select(x => new { x.OperationCode, x.PayloadJson }).FirstOrDefaultAsync(cancellationToken);
            pendingApproval = GetPendingApproval(pending?.OperationCode, pending?.PayloadJson);
            if (pendingApproval == ContractPendingApproval.None && ContractLifecycleOperations.IsActionable(contract.Status, contract.StartDate, contract.EndDate, today))
            {
                if (await EvaluateSw07Async(caller, cancellationToken) != RoomChangeDecision.Denied)
                {
                    canRenew = successor is null;
                    canLiquidate = successor is null;
                }
                canEditDeposit = successor is null && await EvaluateSw08Async(caller, cancellationToken) != RoomChangeDecision.Denied;
            }
        }

        var latestVersion = await _context.ContractVersions.AsNoTracking()
            .Where(version => version.ContractId == contractId)
            .OrderByDescending(version => version.VersionNo)
            .Select(version => new { version.VersionNo, version.ContentHash, version.SentAt })
            .FirstOrDefaultAsync(cancellationToken);
        var acceptance = latestVersion is null
            ? null
            : await _context.ContractAcceptances.AsNoTracking()
                .Where(item => item.ContractId == contractId)
                .OrderByDescending(item => item.Id)
                .Select(item => new { item.AcceptedAt })
                .FirstOrDefaultAsync(cancellationToken);

        return new ContractDetailDto(
            contract.ContractId,
            contract.ContractNumber,
            contract.Room.PropertyId,
            property.Name,
            property.Address,
            contract.Room.Code,
            contract.Room.Floor,
            contract.Room.AreaM2,
            contract.StartDate,
            contract.EndDate,
            contract.SnapshotPrice,
            contract.DepositAmount,
            contract.Status,
            contract.RejectReason,
            contract.PaymentCycleMonths,
            contract.MeterReadingDay,
            contract.Terms,
            members,
            services,
            canRenew,
            canLiquidate,
            canEditDeposit,
            pendingApproval,
            successor,
            previous,
            latestVersion?.VersionNo,
            latestVersion?.ContentHash,
            latestVersion?.SentAt,
            acceptance?.AcceptedAt,
            contract.DepositPaidAmount,
            contract.DepositPaidAt,
            contract.DepositDueAt,
            contract.DepositRefundAmount,
            contract.DepositRefundedAt);
    }

    public async Task<ContractResponseDto> ApproveContractAsync(
        Guid contractId,
        ApproveContractRequestDto request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.Forbidden,
                ContractErrorCodes.Forbidden);
        }

        ArgumentNullException.ThrowIfNull(request);

        string? normalizedRejectReason = null;
        if (!request.IsApproved)
        {
            if (string.IsNullOrWhiteSpace(request.RejectReason))
            {
                throw new ContractValidationException(
                    ContractErrorMessages.RejectReasonRequired,
                    ContractErrorCodes.RejectReasonRequired,
                    nameof(request.RejectReason));
            }

            if (request.RejectReason.Length > ApproveContractRequestDto.MaxRejectReasonLength)
            {
                throw new ContractValidationException(
                    ContractErrorMessages.RejectReasonTooLong,
                    ContractErrorCodes.RejectReasonTooLong,
                    nameof(request.RejectReason));
            }

            normalizedRejectReason = request.RejectReason.Trim();
        }

        var contract = await _context.Contracts
            .SingleOrDefaultAsync(item => item.ContractId == contractId, cancellationToken);

        if (contract is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound,
                ContractErrorCodes.ContractNotFound);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        Room? lockedRoom = null;
        try
        {
            Room? room;
            if (request.IsApproved)
            {
                // Serialize only approvals for the same room; rejection does not block room operations.
                room = await _context.Rooms
                    .FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {contract.RoomId}")
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(cancellationToken);
                lockedRoom = room;
            }
            else
            {
                // Rejections still verify property ownership but deliberately take no room update lock.
                room = await _context.Rooms
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(item => item.Id == contract.RoomId, cancellationToken);
            }

            if (room is null)
            {
                throw new ContractNotFoundException(
                    ContractErrorMessages.RoomNotFound,
                    ContractErrorCodes.RoomNotFound);
            }

            var property = await _context.Properties
                .IgnoreQueryFilters()
                .Where(property => property.Id == room.PropertyId)
                .SingleOrDefaultAsync(cancellationToken);

            if (property is null)
            {
                throw new ContractNotFoundException(
                    ContractErrorMessages.PropertyNotFound,
                    ContractErrorCodes.PropertyNotFound);
            }

            if (!PropertyScope.IsOwner(property.OwnerId, caller.UserId))
            {
                throw new ContractAuthorizationException(
                    ContractErrorMessages.OutsideOwnerScope,
                    ContractErrorCodes.Forbidden);
            }

            // Serialize decisions on one contract, including concurrent reject/approve attempts.
            var lockedContract = await _context.Contracts
                .FromSqlInterpolated($"SELECT * FROM [Contracts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContractId] = {contractId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (lockedContract is null)
            {
                throw new ContractNotFoundException(
                    ContractErrorMessages.ContractNotFound,
                    ContractErrorCodes.ContractNotFound);
            }

            await _context.Entry(contract).ReloadAsync();
            if (contract.Status != ContractStatus.Pending)
            {
                throw new ContractOperationException(
                    ContractErrorMessages.InvalidStatus,
                    ContractErrorCodes.InvalidStatus);
            }

            if (!request.IsApproved)
            {
                MarkDepositRefundObligation(contract);
                contract.Status = ContractStatus.Rejected;
                contract.RejectReason = normalizedRejectReason;
            }
            else
            {
                // Hợp đồng đi qua luồng ký mới phải có bằng chứng khách đã đồng ý.
                var hasSigningVersion = await _context.ContractVersions
                    .AnyAsync(version => version.ContractId == contract.ContractId, cancellationToken);
                var hasAcceptance = await _context.ContractAcceptances
                    .AnyAsync(acceptance => acceptance.ContractId == contract.ContractId, cancellationToken);
                if (hasSigningVersion && !hasAcceptance)
                {
                    throw new ContractOperationException(
                        ContractErrorMessages.TenantAcceptanceRequired,
                        ContractErrorCodes.TenantAcceptanceRequired);
                }

                if (property.IsDeleted)
                {
                    throw new ContractOperationException(
                        ContractErrorMessages.PropertyUnavailable,
                        ContractErrorCodes.PropertyUnavailable);
                }

                if (room.IsDeleted || !room.IsActive)
                {
                    throw new ContractOperationException(
                        ContractErrorMessages.RoomUnavailable,
                        ContractErrorCodes.RoomUnavailable);
                }

                if (room.Status == RoomStatus.Maintenance)
                {
                    throw new ContractOperationException(
                        ContractErrorMessages.RoomUnderMaintenance,
                        ContractErrorCodes.RoomUnderMaintenance);
                }

                if (DateOnly.FromDateTime(contract.StartDate) > _timeProvider.TodayVietnam())
                {
                    throw new ContractOperationException(
                        ContractErrorMessages.FutureStartNotAllowed,
                        ContractErrorCodes.FutureStartNotAllowed);
                }

                var hasOverlap = await _context.Contracts.AnyAsync(other =>
                    other.ContractId != contract.ContractId
                    && other.RoomId == contract.RoomId
                    && other.Status == ContractStatus.Approved
                    && other.StartDate <= contract.EndDate
                    && contract.StartDate <= other.EndDate);

                if (hasOverlap)
                {
                    throw new ContractOperationException(
                        ContractErrorMessages.ContractOverlap,
                        ContractErrorCodes.ContractOverlap);
                }

                if (room.Status != RoomStatus.Available)
                {
                    throw new ContractOperationException(
                        ContractErrorMessages.RoomNotVacant,
                        ContractErrorCodes.RoomNotVacant);
                }

                contract.Status = ContractStatus.Approved;
                contract.RejectReason = null;
                // HĐ do Quản lý tạo chỉ bắt đầu hạn cọc sau khi chủ trọ duyệt.
                if (contract.DepositPaidAt is null && contract.DepositDueAt is null)
                {
                    contract.DepositDueAt = _timeProvider.GetUtcNow()
                        .Add(ContractPaymentRules.DepositPaymentWindow);
                }
                room.Status = RoomStatus.Rented;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ToResponse(contract);
        }
        catch (Exception exception) when (IsContractOverlapConstraint(exception))
        {
            await TryRollbackAndReloadAsync(transaction, contract, lockedRoom);
            throw new ContractOperationException(
                ContractErrorMessages.ContractOverlap,
                ContractErrorCodes.ContractOverlap,
                exception);
        }
        catch
        {
            await TryRollbackAndReloadAsync(transaction, contract, lockedRoom);

            throw;
        }
    }

    public async Task<ContractActionResultDto> RenewContractAsync(
        Guid contractId,
        RenewContractRequestDto request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCanManage(caller);

        var today = _timeProvider.TodayVietnam();
        var (contract, room, property) = await LoadLifecycleTargetAsync(contractId, caller, cancellationToken);
        await ContractLifecycleOperations.EnsureNoPendingApprovalAsync(_context, contract.ContractId, cancellationToken);
        var decision = await EvaluateSw07Async(caller, cancellationToken);

        if (decision == RoomChangeDecision.Denied)
        {
            throw new ContractOperationException(
                ContractErrorMessages.ApprovalDenied,
                ContractErrorCodes.ApprovalDenied);
        }

        if (decision == RoomChangeDecision.RequiresApproval)
        {
            await EnsureRenewalAllowedAsync(
                contract, room, property, request.EndDate, today, cancellationToken);

            var approval = await CreateApprovalRequestAsync(
                contract,
                property.Id,
                new ContractApprovalPayload(ContractApprovalActions.Renew, request.EndDate.Date),
                caller,
                cancellationToken);

            return new ContractActionResultDto(ContractActionOutcome.PendingApproval, null, approval.Id);
        }

        return await ApplyRenewalAsync(contract, room, property, request.EndDate.Date, caller.UserId, cancellationToken);
    }

    public async Task<ContractActionResultDto> LiquidateContractAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(caller);

        var today = _timeProvider.TodayVietnam();
        var (contract, room, property) = await LoadLifecycleTargetAsync(contractId, caller, cancellationToken);
        await ContractLifecycleOperations.EnsureNoPendingApprovalAsync(_context, contract.ContractId, cancellationToken);
        var decision = await EvaluateSw07Async(caller, cancellationToken);

        if (decision == RoomChangeDecision.Denied)
        {
            throw new ContractOperationException(
                ContractErrorMessages.ApprovalDenied,
                ContractErrorCodes.ApprovalDenied);
        }

        if (decision == RoomChangeDecision.RequiresApproval)
        {
            EnsureLiquidationAllowed(contract, room, property, today);

            var approval = await CreateApprovalRequestAsync(
                contract,
                property.Id,
                new ContractApprovalPayload(ContractApprovalActions.Liquidate, null),
                caller,
                cancellationToken);

            return new ContractActionResultDto(ContractActionOutcome.PendingApproval, null, approval.Id);
        }

        return await ApplyLiquidationAsync(contract, room, property, today, caller.UserId, cancellationToken);
    }

    public async Task<ContractActionResultDto> UpdateDepositAsync(
        Guid contractId,
        decimal depositAmount,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(caller);

        if (depositAmount < 0m || depositAmount > 9_999_999_999_999_999.99m)
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidDepositAmount,
                ContractErrorCodes.InvalidDepositAmount,
                nameof(depositAmount));
        }
        var normalizedAmount = MoneyFormatter.RoundVnd(depositAmount);

        var (contract, _, property) = await LoadLifecycleTargetAsync(contractId, caller, cancellationToken);
        if (normalizedAmount == contract.DepositAmount)
            throw new ContractValidationException(ContractErrorMessages.DepositUnchanged, ContractErrorCodes.DepositUnchanged, nameof(depositAmount));
        var decision = await EvaluateSw08Async(caller, cancellationToken);

        if (decision == RoomChangeDecision.Denied)
        {
            throw new ContractOperationException(
                ContractErrorMessages.DepositApprovalDenied,
                ContractErrorCodes.DepositApprovalDenied);
        }

        if (decision == RoomChangeDecision.RequiresApproval)
        {
            ContractLifecycleOperations.EnsureDepositEditable(contract, _timeProvider.TodayVietnam(),
                await ContractLifecycleOperations.HasApprovedSuccessorAsync(_context, contract.ContractId, contract.RoomId, contract.StartDate, cancellationToken));
            await ContractLifecycleOperations.EnsureNoPendingApprovalAsync(_context, contract.ContractId, cancellationToken);
            var approval = await CreateApprovalRequestAsync(
                contract,
                property.Id,
                new ContractApprovalPayload(
                    ContractApprovalActions.UpdateDeposit,
                    null,
                    normalizedAmount,
                    contract.DepositAmount),
                caller,
                cancellationToken,
                operationCode: "SW-08");

            return new ContractActionResultDto(ContractActionOutcome.PendingApproval, null, approval.Id);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await LockContractAsync(contract.ContractId, cancellationToken) is null)
                throw new ContractNotFoundException(ContractErrorMessages.ContractNotFound, ContractErrorCodes.ContractNotFound);
            await _context.Entry(contract).ReloadAsync(cancellationToken);
            await ContractLifecycleOperations.EnsureNoPendingApprovalAsync(_context, contract.ContractId, cancellationToken);
            ContractLifecycleOperations.EnsureDepositEditable(contract, _timeProvider.TodayVietnam(),
                await ContractLifecycleOperations.HasApprovedSuccessorAsync(_context, contract.ContractId, contract.RoomId, contract.StartDate, cancellationToken));
            var oldDeposit = contract.DepositAmount;
            if (oldDeposit == normalizedAmount)
                throw new ContractValidationException(ContractErrorMessages.DepositUnchanged, ContractErrorCodes.DepositUnchanged, nameof(depositAmount));
            contract.DepositAmount = normalizedAmount;
            // Đổi mức cọc thì phải thu lại theo mức mới; xoá xác nhận đã thu cũ.
                contract.DepositPaidAmount = null;
            contract.DepositPaidAt = null;
            contract.DepositPaidByUserId = null;
            contract.DepositRefundAmount = null;
            contract.DepositRefundedAt = null;
            contract.DepositDueAt = _timeProvider.GetUtcNow().Add(ContractPaymentRules.DepositPaymentWindow);
            ContractLifecycleOperations.AddAudit(_context, ContractLifecycleOperations.ContractAuditActions.DepositUpdated,
                caller.UserId, property.Id, JsonSerializer.Serialize(new { contractId, oldDeposit, newDeposit = normalizedAmount }), _timeProvider.GetUtcNow());
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContractActionResultDto(ContractActionOutcome.Applied, contract.ContractId, null);
        }
        catch
        {
            await TryRollbackAndReloadAsync(transaction, contract, null);
            throw;
        }
    }

    public async Task<ContractActionResultDto> RecordDepositPaymentAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(caller);

        var contract = await ApplyCallerScope(_context.Contracts, caller)
            .SingleOrDefaultAsync(item => item.ContractId == contractId, cancellationToken);
        if (contract is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound, ContractErrorCodes.ContractNotFound);
        }

        if (contract.Status is ContractStatus.Draft or ContractStatus.Rejected or ContractStatus.Liquidated)
        {
            throw new ContractOperationException(
                ContractErrorMessages.DepositPaymentNotAllowed,
                ContractErrorCodes.DepositPaymentNotAllowed);
        }

        if (contract.DepositAmount <= 0m)
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidDepositAmount,
                ContractErrorCodes.InvalidDepositAmount,
                nameof(contract.DepositAmount));
        }

        var propertyId = await (
            from room in _context.Rooms.IgnoreQueryFilters()
            join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
            where room.Id == contract.RoomId
            select property.Id)
            .SingleAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();
        contract.DepositPaidAmount = contract.DepositAmount;
        contract.DepositPaidAt = now;
        contract.DepositPaidByUserId = caller.UserId;
        contract.DepositDueAt = null;
        ContractLifecycleOperations.AddAudit(
            _context,
            "ContractDepositReceived",
            caller.UserId,
            propertyId,
            JsonSerializer.Serialize(new { contractId, amount = contract.DepositAmount }),
            now);
        await _context.SaveChangesAsync(cancellationToken);
        return new ContractActionResultDto(ContractActionOutcome.Applied, contractId, null);
    }

    public async Task<ContractResponseDto> CreateContractAsync(
        CreateContractRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCanCreate(caller);

        if (request.StartDate >= request.EndDate)
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidDateRange,
                ContractErrorCodes.InvalidDateRange,
                nameof(request.StartDate));
        }

        if (request.EndDate.Date < request.StartDate.Date.AddMonths(1))
        {
            throw new ContractValidationException(
                ContractErrorMessages.EndDateTooShort,
                ContractErrorCodes.EndDateTooShort,
                nameof(request.EndDate));
        }

        if (DateOnly.FromDateTime(request.StartDate) > _timeProvider.TodayVietnam())
        {
            throw new ContractValidationException(
                ContractErrorMessages.FutureStartNotAllowed,
                ContractErrorCodes.FutureStartNotAllowed,
                nameof(request.StartDate));
        }

        if (request.TenantIds is null || request.TenantIds.Count == 0)
        {
            throw new ContractValidationException(
                ContractErrorMessages.TenantRequired,
                ContractErrorCodes.TenantRequired,
                nameof(request.TenantIds));
        }

        // Kiểm tra dữ liệu thuần (không cần DB) trước khi truy vấn.
        var requestedTenantIds = request.TenantIds.Distinct().ToList();

        if (request.DepositAmount is { } requestedDeposit
            && (requestedDeposit < 0m || requestedDeposit > 9_999_999_999_999_999.99m))
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidDepositAmount,
                ContractErrorCodes.InvalidDepositAmount,
                nameof(request.DepositAmount));
        }

        if (request.MonthlyRent is { } requestedRent
            && (requestedRent < 0m || requestedRent > 9_999_999_999_999_999.99m))
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidMonthlyRent,
                ContractErrorCodes.InvalidMonthlyRent,
                nameof(request.MonthlyRent));
        }

        if (request.PaymentCycleMonths is not (1 or 2 or 3 or 6 or 12))
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidPaymentCycle,
                ContractErrorCodes.InvalidPaymentCycle,
                nameof(request.PaymentCycleMonths));
        }

        if (request.MeterReadingDay is { } meterReadingDay && meterReadingDay is < 1 or > 28)
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidMeterReadingDay,
                ContractErrorCodes.InvalidMeterReadingDay,
                nameof(request.MeterReadingDay));
        }

        var terms = string.IsNullOrWhiteSpace(request.Terms) ? null : request.Terms.Trim();
        if (terms is { Length: > 4000 })
        {
            throw new ContractValidationException(
                ContractErrorMessages.TermsTooLong,
                ContractErrorCodes.TermsTooLong,
                nameof(request.Terms));
        }

        var representativeTenantId = request.RepresentativeTenantId ?? requestedTenantIds[0];
        if (!requestedTenantIds.Contains(representativeTenantId))
        {
            throw new ContractValidationException(
                ContractErrorMessages.RepresentativeInvalid,
                ContractErrorCodes.RepresentativeInvalid,
                nameof(request.RepresentativeTenantId));
        }

        var relationships = NormalizeRelationships(request.MemberRelations, requestedTenantIds);

        // Chỉ được tạo hợp đồng cho phòng thuộc nhà mình (Chủ trọ) hoặc nhà được phân công (Quản lý).
        var visibleProperties = PropertyScope.Apply(
            _context.Properties.AsNoTracking(),
            _context.PropertyManagers.AsNoTracking(),
            caller);

        var room = await (
            from candidate in _context.Rooms.AsNoTracking()
            join property in visibleProperties on candidate.PropertyId equals property.Id
            where candidate.Id == request.RoomId
                && candidate.IsActive
                && !candidate.IsDeleted
                && candidate.Status == RoomStatus.Available
            select candidate)
            .FirstOrDefaultAsync(cancellationToken);

        if (room is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.RoomNotAvailable,
                ContractErrorCodes.RoomNotAvailable);
        }

        var currentRoomPrice = room.StandardPrice;

        // Hồ sơ phải thuộc phạm vi người gọi: đã liên kết với Chủ trọ/Quản lý này hoặc
        // chưa liên kết với ai (kho hồ sơ dùng chung). Hồ sơ đã thuộc Chủ trọ khác bị chặn
        // để Quản lý không gắn hồ sơ của tòa khác vào hợp đồng.
        var visibleTenants = TenantVisibility.Apply(_context, caller.UserId, caller.Role);
        var visibleIds = visibleTenants
            .Where(tenant => requestedTenantIds.Contains(tenant.Id)
                && tenant.IsActive
                && !tenant.IsDeleted)
            .Select(tenant => tenant.Id);
        var pooledIds = _context.Tenants
            .AsNoTracking()
            .Where(tenant => requestedTenantIds.Contains(tenant.Id)
                && tenant.IsActive
                && !tenant.IsDeleted
                && !_context.TenantOwnerLinks.Any(link => link.TenantId == tenant.Id))
            .Select(tenant => tenant.Id);
        var existingTenantCount = await visibleIds
            .Union(pooledIds)
            .CountAsync(cancellationToken);

        if (existingTenantCount != requestedTenantIds.Count)
        {
            throw new ContractValidationException(
                ContractErrorMessages.TenantUnavailable,
                ContractErrorCodes.TenantUnavailable,
                nameof(request.TenantIds));
        }

        // D4: Chủ trọ tự tạo hợp đồng thì hợp đồng có hiệu lực ngay (không cần tự duyệt lại);
        // Quản lý tạo vẫn nằm ở Chờ duyệt để Chủ trọ quyết định.
        // Luồng ký mới: AsDraft = true sẽ tạo bản nháp và chờ gửi khách.
        var ownerCreated = caller.IsChuTro;
        var createdAsDraft = request.AsDraft;
        var monthlyRent = request.MonthlyRent is { } inputRent
            ? MoneyFormatter.RoundVnd(inputRent)
            : currentRoomPrice;
        var depositAmount = request.DepositAmount is { } inputDeposit
            ? MoneyFormatter.RoundVnd(inputDeposit)
            : MoneyFormatter.RoundVnd(monthlyRent);
        var contractId = Guid.NewGuid();
        var contractNumber = $"HD-{contractId:N}".ToUpperInvariant();
        var contract = new Contract
        {
            ContractId = contractId,
            ContractNumber = contractNumber,
            RoomId = room.Id,
            SnapshotPrice = monthlyRent,
            DepositAmount = depositAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = createdAsDraft
                ? ContractStatus.Draft
                : ownerCreated ? ContractStatus.Approved : ContractStatus.Pending,
            PaymentCycleMonths = request.PaymentCycleMonths,
            MeterReadingDay = request.MeterReadingDay,
            Terms = terms,
            CreatedByUserId = caller.UserId,
            CreatedByRole = caller.Role,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Khóa phòng để hai yêu cầu tạo đồng thời không cùng lọt qua bước kiểm trùng.
            var lockedRoom = await _context.Rooms
                .FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {room.Id}")
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);

            if (lockedRoom is null
                || lockedRoom.IsDeleted
                || !lockedRoom.IsActive
                || lockedRoom.Status != RoomStatus.Available)
            {
                throw new ContractOperationException(
                    ContractErrorMessages.RoomUnavailable,
                    ContractErrorCodes.RoomUnavailable);
            }

            var hasOverlappingPendingOrApproved = await _context.Contracts.AnyAsync(other =>
                other.RoomId == room.Id
                && (other.Status == ContractStatus.Pending
                    || other.Status == ContractStatus.Approved
                    || other.Status == ContractStatus.AwaitingTenant
                    || other.Status == ContractStatus.ChangeRequested
                    || other.Status == ContractStatus.TenantAccepted)
                && other.StartDate <= request.EndDate
                && request.StartDate <= other.EndDate,
                cancellationToken);

            if (hasOverlappingPendingOrApproved)
            {
                throw new ContractOperationException(
                    ContractErrorMessages.PendingContractExists,
                    ContractErrorCodes.PendingContractExists);
            }

            _context.Contracts.Add(contract);
            _context.ContractMembers.AddRange(requestedTenantIds.Select(tenantId => new ContractMember
            {
                ContractId = contract.ContractId,
                TenantId = tenantId,
                IsRepresentative = tenantId == representativeTenantId,
                Relationship = relationships.GetValueOrDefault(tenantId),
            }));

            var pendingAssignments = await _context.PendingRoomAssignments
                .Where(assignment => assignment.RoomId == room.Id
                    && requestedTenantIds.Contains(assignment.TenantId)
                    && assignment.Status == PendingRoomAssignmentStatus.Pending)
                .ToListAsync(cancellationToken);
            foreach (var assignment in pendingAssignments)
            {
                assignment.Status = PendingRoomAssignmentStatus.Completed;
            }

            if (ownerCreated && !createdAsDraft)
            {
                lockedRoom.Status = RoomStatus.Rented;
                lockedRoom.UpdatedAt = _timeProvider.UtcNow();
            }

            // PLAN-005 (điều chỉnh): hợp đồng ở tòa của chủ trọ nào thì chủ trọ đó được xem hồ sơ khách,
            // kể cả khách đã thuộc chủ trọ khác — mỗi khách có thể có nhiều liên kết chủ trọ.
            var propertyOwnerId = await _context.Properties.AsNoTracking()
                .Where(property => property.Id == room.PropertyId)
                .Select(property => property.OwnerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(propertyOwnerId))
            {
                var linkedTenantIds = await _context.TenantOwnerLinks
                    .Where(link => link.OwnerId == propertyOwnerId && requestedTenantIds.Contains(link.TenantId))
                    .Select(link => link.TenantId)
                    .ToListAsync(cancellationToken);

                foreach (var tenantId in requestedTenantIds.Except(linkedTenantIds))
                {
                    _context.TenantOwnerLinks.Add(new Web.Features.Tenants.Entities.TenantOwnerLink
                    {
                        TenantId = tenantId,
                        OwnerId = propertyOwnerId,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch
            {
                // Preserve the original write exception if rollback also fails.
            }
            throw;
        }

        return new ContractResponseDto(
            contract.ContractId,
            contract.ContractNumber,
            contract.RoomId,
            contract.StartDate,
            contract.EndDate,
            contract.SnapshotPrice,
            contract.DepositAmount,
            contract.Status,
            contract.RejectReason);
    }

    public async Task<ContractActionResultDto> DeleteAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(caller);

        var contract = await ApplyCallerScope(_context.Contracts.AsNoTracking(), caller)
            .SingleOrDefaultAsync(item => item.ContractId == contractId, cancellationToken);
        if (contract is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound, ContractErrorCodes.ContractNotFound);
        }

        if (contract.Status is not (ContractStatus.Pending or ContractStatus.Rejected
            or ContractStatus.Draft or ContractStatus.AwaitingTenant or ContractStatus.ChangeRequested
            or ContractStatus.Cancelled))
        {
            throw new ContractOperationException(
                ContractErrorMessages.NotDeletable, ContractErrorCodes.NotDeletable);
        }

        if (await HasContractDependenciesAsync(contractId, cancellationToken))
        {
            throw new ContractOperationException(
                ContractErrorMessages.HasDependencies, ContractErrorCodes.HasDependencies);
        }

        var tracked = await _context.Contracts
            .SingleAsync(item => item.ContractId == contractId, cancellationToken);
        tracked.IsDeleted = true;
        tracked.DeletedAt = _timeProvider.GetUtcNow().UtcDateTime;
        tracked.DeletedBy = caller.UserId;
        await _context.SaveChangesAsync(cancellationToken);

        return new ContractActionResultDto(ContractActionOutcome.Applied, contractId, null);
    }

    public async Task<ContractActionResultDto> RestoreAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (!caller.IsChuTro)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.Forbidden, ContractErrorCodes.Forbidden);
        }

        var contract = await _context.Contracts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.ContractId == contractId && item.IsDeleted, cancellationToken)
            ?? throw new ContractNotFoundException(
                ContractErrorMessages.NotInTrash, ContractErrorCodes.NotInTrash);

        var inScope = await ApplyCallerScope(_context.Contracts.IgnoreQueryFilters(), caller)
            .AnyAsync(item => item.ContractId == contractId, cancellationToken);
        if (!inScope)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.Forbidden, ContractErrorCodes.Forbidden);
        }

        if (!await _context.Rooms.AnyAsync(room => room.Id == contract.RoomId, cancellationToken))
        {
            throw new ContractOperationException(
                ContractErrorMessages.RoomDeleted, ContractErrorCodes.RoomDeleted);
        }

        contract.IsDeleted = false;
        contract.DeletedAt = null;
        contract.DeletedBy = null;
        await _context.SaveChangesAsync(cancellationToken);

        return new ContractActionResultDto(ContractActionOutcome.Applied, contractId, null);
    }

    public async Task<IReadOnlyList<ContractTrashItemDto>> ListDeletedAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (!caller.IsChuTro)
        {
            return [];
        }

        return await (
                from contract in _context.Contracts.IgnoreQueryFilters().AsNoTracking()
                join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                where contract.IsDeleted && property.OwnerId == caller.UserId
                orderby contract.DeletedAt descending, contract.ContractNumber
                select new ContractTrashItemDto(
                    contract.ContractId,
                    contract.ContractNumber,
                    room.Code,
                    property.Name,
                    contract.Status,
                    contract.StartDate,
                    contract.EndDate,
                    contract.DeletedAt,
                    contract.DeletedBy))
            .ToListAsync(cancellationToken);
    }

    public async Task<ContractActionResultDto> PurgeAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (!caller.IsChuTro)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.Forbidden, ContractErrorCodes.Forbidden);
        }

        var contract = await _context.Contracts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.ContractId == contractId && item.IsDeleted, cancellationToken)
            ?? throw new ContractNotFoundException(
                ContractErrorMessages.NotInTrash, ContractErrorCodes.NotInTrash);

        var inScope = await ApplyCallerScope(_context.Contracts.IgnoreQueryFilters(), caller)
            .AnyAsync(item => item.ContractId == contractId, cancellationToken);
        if (!inScope)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.Forbidden, ContractErrorCodes.Forbidden);
        }

        if (await HasContractDependenciesAsync(contractId, cancellationToken))
        {
            throw new ContractOperationException(
                ContractErrorMessages.HasDependencies, ContractErrorCodes.HasDependencies);
        }

        var propertyId = await (
                from room in _context.Rooms.IgnoreQueryFilters().AsNoTracking()
                join property in _context.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
                where room.Id == contract.RoomId
                select (int?)property.Id)
            .FirstOrDefaultAsync(cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var acceptances = await _context.ContractAcceptances
                .Where(item => item.ContractId == contractId)
                .ToListAsync(cancellationToken);
            var changeRequests = await _context.ContractChangeRequests
                .Where(item => item.ContractId == contractId)
                .ToListAsync(cancellationToken);
            var versions = await _context.ContractVersions
                .Where(item => item.ContractId == contractId)
                .ToListAsync(cancellationToken);
            _context.ContractAcceptances.RemoveRange(acceptances);
            _context.ContractChangeRequests.RemoveRange(changeRequests);
            _context.ContractVersions.RemoveRange(versions);

            var members = await _context.ContractMembers
                .Where(member => member.ContractId == contractId)
                .ToListAsync(cancellationToken);
            _context.ContractMembers.RemoveRange(members);
            _context.Contracts.Remove(contract);
            if (propertyId is int id)
            {
                _context.AuditLogEntries.Add(new AuditLogEntry
                {
                    Category = AuditLogCategory.Business,
                    Action = "contract.purge",
                    ActorUserId = caller.UserId,
                    PropertyId = id,
                    Metadata = $"Contract {contract.ContractNumber}",
                    OccurredAt = DateTimeOffset.UtcNow,
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ContractActionResultDto(ContractActionOutcome.Applied, contractId, null);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ContractOperationException(
                ContractErrorMessages.HasDependencies, ContractErrorCodes.HasDependencies);
        }
    }

    public async Task<TrashPurgeSummary> PurgeAllAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (!caller.IsChuTro)
        {
            return new TrashPurgeSummary(0, 0);
        }

        var ids = await (
                from contract in _context.Contracts.IgnoreQueryFilters().AsNoTracking()
                join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                where contract.IsDeleted && property.OwnerId == caller.UserId
                select contract.ContractId)
            .ToListAsync(cancellationToken);

        var purged = 0;
        var skipped = 0;
        foreach (var id in ids)
        {
            try
            {
                await PurgeAsync(id, caller, cancellationToken);
                purged++;
            }
            catch (Exception ex) when (ex is ContractOperationException or ContractAuthorizationException or DbUpdateException)
            {
                skipped++;
                _context.ChangeTracker.Clear();
            }
        }

        return new TrashPurgeSummary(purged, skipped);
    }

    private async Task<bool> HasContractDependenciesAsync(Guid contractId, CancellationToken cancellationToken) =>
        await _context.Invoices.IgnoreQueryFilters().AnyAsync(item => item.ContractId == contractId, cancellationToken)
        || await _context.ContractRenewalRequests.AnyAsync(item => item.ContractId == contractId, cancellationToken)
        || await _context.UtilityReadingPeriods.AnyAsync(item => item.ContractId == contractId, cancellationToken);

    /// <summary>Ghi nghĩa vụ hoàn cọc khi hợp đồng kết thúc sau khi đã thu tiền.</summary>
    private static void MarkDepositRefundObligation(Contract contract)
    {
        if (contract.DepositPaidAmount is > 0m
            && contract.DepositRefundAmount is null
            && contract.DepositRefundedAt is null)
        {
            contract.DepositRefundAmount = contract.DepositPaidAmount;
        }
    }

    private IQueryable<Contract> ApplyCallerScope(IQueryable<Contract> contracts, PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return from contract in contracts
                   join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                   where property.OwnerId == caller.UserId
                   select contract;
        }

        if (caller.IsQuanLy)
        {
            return from contract in contracts
                   join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   where _context.PropertyManagers.Any(assignment =>
                       assignment.PropertyId == room.PropertyId
                       && assignment.ManagerUserId == caller.UserId
                       && assignment.RevokedAt == null)
                   select contract;
        }

        if (caller.IsKhachThue)
        {
            return from contract in contracts
                   join member in _context.ContractMembers on contract.ContractId equals member.ContractId
                   join tenant in _context.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
                   where tenant.UserId == caller.UserId
                       && contract.Status != ContractStatus.Draft
                   select contract;
        }

        return contracts.Where(_ => false);
    }

    private static void EnsureCanView(PropertyCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro && !caller.IsQuanLy && !caller.IsKhachThue)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.Forbidden,
                ContractErrorCodes.Forbidden);
        }
    }

    private static void EnsureCanCreate(PropertyCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.Forbidden,
                ContractErrorCodes.Forbidden);
        }
    }

    private static void EnsureCanManage(PropertyCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.Forbidden,
                ContractErrorCodes.Forbidden);
        }
    }

    private async Task<RoomChangeDecision> EvaluateSw07Async(
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (!caller.IsQuanLy)
        {
            return RoomChangeDecision.Allowed;
        }

        return _togglePolicy is null
            ? RoomChangeDecision.RequiresApproval
            : await _togglePolicy.EvaluateAsync(ManagerAction.SW07, caller.Role, cancellationToken);
    }

    private async Task<RoomChangeDecision> EvaluateSw08Async(
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (!caller.IsQuanLy)
        {
            return RoomChangeDecision.Allowed;
        }

        return _togglePolicy is null
            ? RoomChangeDecision.RequiresApproval
            : await _togglePolicy.EvaluateAsync(ManagerAction.SW08, caller.Role, cancellationToken);
    }

    private async Task<(Contract Contract, Room Room, Property Property)> LoadLifecycleTargetAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Contracts
            .SingleOrDefaultAsync(item => item.ContractId == contractId, cancellationToken);
        if (contract is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound,
                ContractErrorCodes.ContractNotFound);
        }

        var room = await _context.Rooms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == contract.RoomId, cancellationToken);
        if (room is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.RoomNotFound,
                ContractErrorCodes.RoomNotFound);
        }

        var property = await PropertyScope.Apply(
                _context.Properties.IgnoreQueryFilters().AsNoTracking(),
                _context.PropertyManagers.AsNoTracking(),
                caller)
            .SingleOrDefaultAsync(item => item.Id == room.PropertyId, cancellationToken);
        if (property is null)
        {
            throw new ContractAuthorizationException(
                ContractErrorMessages.OutsideOwnerScope,
                ContractErrorCodes.Forbidden);
        }

        return (contract, room, property);
    }

    private async Task<ApprovalRequestDto> CreateApprovalRequestAsync(
        Contract contract,
        int propertyId,
        ContractApprovalPayload payload,
        PropertyCaller caller,
        CancellationToken cancellationToken,
        string operationCode = "SW-07")
    {
        if (_approvals is null)
        {
            throw new ContractOperationException(
                ContractErrorMessages.ApprovalFailed,
                ContractErrorCodes.ApprovalFailed);
        }

        var approval = await _approvals.CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = operationCode,
                TargetType = ApprovalTargetType.Contract,
                TargetKey = contract.ContractId.ToString("D"),
                PropertyId = propertyId,
                PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions),
            },
            new ApprovalCaller(caller.UserId, caller.Role),
            cancellationToken);

        if (approval.Succeeded && approval.Request is not null)
        {
            return approval.Request;
        }

        if (approval.ErrorCode == ApprovalErrorCodes.DuplicatePendingRequest)
        {
            throw new ContractOperationException(
                ContractErrorMessages.DuplicatePendingRequest,
                ContractErrorCodes.DuplicatePendingRequest);
        }

        throw new ContractOperationException(
            ContractErrorMessages.ApprovalFailed,
            ContractErrorCodes.ApprovalFailed);
    }

    private async Task EnsureRenewalAllowedAsync(
        Contract contract,
        Room room,
        Property property,
        DateTime newEndDate,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        EnsureLifecycleEligible(contract, room, property, today);
        ContractLifecycleOperations.EnsureRenewalEndDate(contract.EndDate, newEndDate);

        if (await ContractLifecycleOperations.HasApprovedSuccessorAsync(
                _context, contract.ContractId, contract.RoomId, contract.StartDate, cancellationToken))
        {
            throw new ContractOperationException(
                ContractErrorMessages.ContractAlreadyRenewed,
                ContractErrorCodes.ContractAlreadyRenewed);
        }

        if (await ContractLifecycleOperations.HasOverlappingApprovedAsync(
                _context, contract.RoomId, contract.EndDate.AddDays(1), newEndDate, cancellationToken))
        {
            throw new ContractOperationException(
                ContractErrorMessages.ContractOverlap,
                ContractErrorCodes.ContractOverlap);
        }
    }

    private static void EnsureLiquidationAllowed(
        Contract contract,
        Room room,
        Property property,
        DateOnly today) =>
        EnsureLifecycleEligible(contract, room, property, today);

    private static void EnsureLifecycleEligible(
        Contract contract,
        Room room,
        Property property,
        DateOnly today)
    {
        if (!ContractLifecycleOperations.IsActionable(
                contract.Status, contract.StartDate, contract.EndDate, today))
        {
            throw new ContractOperationException(
                ContractErrorMessages.ContractNotActive,
                ContractErrorCodes.ContractNotActive);
        }

        if (property.IsDeleted)
        {
            throw new ContractOperationException(
                ContractErrorMessages.PropertyUnavailable,
                ContractErrorCodes.PropertyUnavailable);
        }

        if (room.IsDeleted || !room.IsActive)
        {
            throw new ContractOperationException(
                ContractErrorMessages.RoomUnavailable,
                ContractErrorCodes.RoomUnavailable);
        }
    }

    private async Task<ContractActionResultDto> ApplyRenewalAsync(
        Contract contract,
        Room room,
        Property property,
        DateTime newEndDate,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var today = _timeProvider.TodayVietnam();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        Room? lockedRoom = null;
        try
        {
            lockedRoom = await LockRoomAsync(room.Id, cancellationToken);
            if (lockedRoom is null)
            {
                throw new ContractNotFoundException(
                    ContractErrorMessages.RoomNotFound,
                    ContractErrorCodes.RoomNotFound);
            }

            if (await LockContractAsync(contract.ContractId, cancellationToken) is null)
            {
                throw new ContractNotFoundException(
                    ContractErrorMessages.ContractNotFound,
                    ContractErrorCodes.ContractNotFound);
            }

            await _context.Entry(contract).ReloadAsync(cancellationToken);
            await EnsureRenewalAllowedAsync(
                contract, lockedRoom, property, newEndDate, today, cancellationToken);

            var renewal = await ContractLifecycleOperations.CreateRenewalAsync(
                _context, contract, lockedRoom, newEndDate, _timeProvider.GetUtcNow(), cancellationToken);
            await ContractLifecycleOperations.SettlePendingRenewalRequestsAsync(_context, contract.ContractId,
                RenewalRequestStatus.Approved, actorUserId, _timeProvider.GetUtcNow().UtcDateTime, null, cancellationToken);
            ContractLifecycleOperations.AddAudit(_context, ContractLifecycleOperations.ContractAuditActions.Renewed,
                actorUserId, property.Id, JsonSerializer.Serialize(new { contractId = contract.ContractId, newContractId = renewal.ContractId, newEndDate }), _timeProvider.GetUtcNow());

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ContractActionResultDto(ContractActionOutcome.Applied, renewal.ContractId, null);
        }
        catch (Exception exception) when (IsContractOverlapConstraint(exception))
        {
            await TryRollbackAndReloadAsync(transaction, contract, lockedRoom);
            throw new ContractOperationException(
                ContractErrorMessages.ContractOverlap,
                ContractErrorCodes.ContractOverlap,
                exception);
        }
        catch
        {
            await TryRollbackAndReloadAsync(transaction, contract, lockedRoom);
            throw;
        }
    }

    private async Task<ContractActionResultDto> ApplyLiquidationAsync(
        Contract contract,
        Room room,
        Property property,
        DateOnly today,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        Room? lockedRoom = null;
        try
        {
            lockedRoom = await LockRoomAsync(room.Id, cancellationToken);
            if (lockedRoom is null)
            {
                throw new ContractNotFoundException(
                    ContractErrorMessages.RoomNotFound,
                    ContractErrorCodes.RoomNotFound);
            }

            if (await LockContractAsync(contract.ContractId, cancellationToken) is null)
            {
                throw new ContractNotFoundException(
                    ContractErrorMessages.ContractNotFound,
                    ContractErrorCodes.ContractNotFound);
            }

            await _context.Entry(contract).ReloadAsync(cancellationToken);
            EnsureLiquidationAllowed(contract, lockedRoom, property, today);

            MarkDepositRefundObligation(contract);
            await ContractLifecycleOperations.LiquidateAsync(
                _context, contract, lockedRoom, today, cancellationToken);
            await ContractLifecycleOperations.SettlePendingRenewalRequestsAsync(_context, contract.ContractId,
                RenewalRequestStatus.Rejected, actorUserId, _timeProvider.GetUtcNow().UtcDateTime, ContractErrorMessages.RenewalRejectedByLiquidation, cancellationToken);
            ContractLifecycleOperations.AddAudit(_context, ContractLifecycleOperations.ContractAuditActions.Liquidated,
                actorUserId, property.Id, JsonSerializer.Serialize(new { contractId = contract.ContractId }), _timeProvider.GetUtcNow());

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ContractActionResultDto(ContractActionOutcome.Applied, contract.ContractId, null);
        }
        catch
        {
            await TryRollbackAndReloadAsync(transaction, contract, lockedRoom);
            throw;
        }
    }

    private Task<Room?> LockRoomAsync(int roomId, CancellationToken cancellationToken) =>
        _context.Rooms
            .FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {roomId}")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Contract?> LockContractAsync(Guid contractId, CancellationToken cancellationToken) =>
        _context.Contracts
            .FromSqlInterpolated(
                $"SELECT * FROM [Contracts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContractId] = {contractId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<ContractLinkDto?> FindLinkAsync(int roomId, Guid contractId, DateTime boundary, bool successor, CancellationToken ct) =>
        await _context.Contracts.AsNoTracking().Where(x => x.RoomId == roomId && x.ContractId != contractId
                && x.Status == ContractStatus.Approved && (successor ? x.StartDate == boundary : x.EndDate == boundary))
            .Select(x => new ContractLinkDto(x.ContractId, x.ContractNumber, x.StartDate, x.EndDate)).SingleOrDefaultAsync(ct);

    private static ContractPendingApproval GetPendingApproval(string? operationCode, string? payloadJson)
    {
        if (operationCode == "SW-08") return ContractPendingApproval.DepositUpdate;
        if (operationCode != "SW-07") return ContractPendingApproval.None;
        try { return JsonSerializer.Deserialize<ContractApprovalPayload>(payloadJson ?? "{}", PayloadJsonOptions)?.Action == ContractApprovalActions.Liquidate ? ContractPendingApproval.Liquidation : ContractPendingApproval.Renewal; }
        catch (JsonException) { return ContractPendingApproval.Renewal; }
    }

    private static ContractResponseDto ToResponse(Contract contract) => new(
        contract.ContractId,
        contract.ContractNumber,
        contract.RoomId,
        contract.StartDate,
        contract.EndDate,
        contract.SnapshotPrice,
        contract.DepositAmount,
        contract.Status,
        contract.RejectReason);

    private async Task TryRollbackAndReloadAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Contract contract,
        Room? room)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch
        {
            // Cleanup must never replace the business/database exception that triggered rollback.
        }

        try
        {
            await _context.Entry(contract).ReloadAsync();
        }
        catch
        {
            // A failed connection can also prevent reloading; keep the original exception.
        }

        if (room is null)
        {
            return;
        }

        try
        {
            await _context.Entry(room).ReloadAsync();
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static bool IsContractOverlapConstraint(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 51030 })
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ContractListRow(
        Guid ContractId,
        string ContractNumber,
        string PropertyName,
        string RoomCode,
        DateTime StartDate,
        DateTime EndDate,
        decimal SnapshotPrice,
        decimal DepositAmount,
        ContractStatus Status);

    private sealed record ContractMemberRow(Guid ContractId, string FullName, string Phone);

    private sealed record ContractTenantRow(
        int Id,
        string FullName,
        string Phone,
        string? Email,
        string? CitizenId,
        string? PermanentAddress,
        string? IdIssuedPlace,
        DateOnly? DateOfBirth);
}
