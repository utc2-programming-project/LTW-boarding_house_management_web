using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Infrastructure.Time;

namespace Web.Features.Contracts.Services;

public sealed class ContractService : IContractService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ContractService(ApplicationDbContext dbContext, TimeProvider? timeProvider = null)
    {
        _context = dbContext;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
            .Select(property => new ContractPropertyOptionDto(property.Id, property.Name))
            .ToListAsync(cancellationToken);

        var rooms = await (
            from room in _context.Rooms.AsNoTracking()
            join property in visibleProperties on room.PropertyId equals property.Id
            where room.IsActive && !room.IsDeleted && room.Status == RoomStatus.Available
            orderby property.Name, room.Code
            select new ContractRoomOptionDto(room.Id, room.PropertyId, room.Code, room.StandardPrice))
            .ToListAsync(cancellationToken);

        var tenants = await _context.Tenants.AsNoTracking()
            .Where(tenant => tenant.IsActive && !tenant.IsDeleted)
            .OrderBy(tenant => tenant.FullName)
            .Select(tenant => new ContractTenantOptionDto(tenant.Id, tenant.FullName, tenant.Phone))
            .ToListAsync(cancellationToken);

        return new ContractCreateOptionsDto(properties, rooms, tenants);
    }

    public async Task<IReadOnlyList<ContractListItemDto>> ListAsync(
        string? keyword,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanView(caller);

        var query = ApplyCallerScope(_context.Contracts.AsNoTracking(), caller);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(contract =>
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
                item.StartDate,
                item.EndDate,
                item.SnapshotPrice,
                item.DepositAmount,
                item.Status,
                item.RejectReason,
                Room = _context.Rooms.IgnoreQueryFilters().Where(room => room.Id == item.RoomId)
                    .Select(room => new { room.Code, room.PropertyId }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (contract?.Room is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound,
                ContractErrorCodes.ContractNotFound);
        }

        var propertyName = await _context.Properties.IgnoreQueryFilters()
            .Where(property => property.Id == contract.Room.PropertyId)
            .Select(property => property.Name)
            .SingleOrDefaultAsync(cancellationToken);

        if (propertyName is null)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound,
                ContractErrorCodes.ContractNotFound);
        }

        var members = await (
            from member in _context.ContractMembers.AsNoTracking()
            join tenant in _context.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
            where member.ContractId == contractId
            orderby tenant.FullName
            select new ContractMemberListItemDto(
                tenant.Id,
                tenant.FullName,
                caller.IsKhachThue && tenant.UserId != caller.UserId ? string.Empty : tenant.Phone))
            .ToListAsync(cancellationToken);

        return new ContractDetailDto(
            contract.ContractId,
            contract.ContractNumber,
            propertyName,
            contract.Room.Code,
            contract.StartDate,
            contract.EndDate,
            contract.SnapshotPrice,
            contract.DepositAmount,
            contract.Status,
            contract.RejectReason,
            members);
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
                contract.Status = ContractStatus.Rejected;
                contract.RejectReason = normalizedRejectReason;
            }
            else
            {
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
                contract.SnapshotPrice = room.StandardPrice;
                contract.DepositAmount = MoneyFormatter.RoundVnd(room.StandardPrice * 0.20m);
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

        var requestedTenantIds = request.TenantIds.Distinct().ToList();
        var existingTenantCount = await _context.Tenants
            .AsNoTracking()
            .Where(tenant => requestedTenantIds.Contains(tenant.Id)
                && tenant.IsActive
                && !tenant.IsDeleted)
            .Select(tenant => tenant.Id)
            .CountAsync(cancellationToken);

        if (existingTenantCount != requestedTenantIds.Count)
        {
            throw new ContractValidationException(
                ContractErrorMessages.TenantUnavailable,
                ContractErrorCodes.TenantUnavailable,
                nameof(request.TenantIds));
        }

        var depositAmount = MoneyFormatter.RoundVnd(currentRoomPrice * 0.20m);
        var contractId = Guid.NewGuid();
        var contractNumber = $"HD-{contractId:N}".ToUpperInvariant();
        var contract = new Contract
        {
            ContractId = contractId,
            ContractNumber = contractNumber,
            RoomId = room.Id,
            SnapshotPrice = currentRoomPrice,
            DepositAmount = depositAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = ContractStatus.Pending,
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Khóa phòng để hai yêu cầu tạo đồng thời không cùng lọt qua bước kiểm trùng.
            var lockedRoom = await _context.Rooms
                .FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {room.Id}")
                .IgnoreQueryFilters()
                .AsNoTracking()
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
                && (other.Status == ContractStatus.Pending || other.Status == ContractStatus.Approved)
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
            }));

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
}
