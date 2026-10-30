using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;

namespace Web.Features.Contracts.Services;

public sealed class ContractService : IContractService
{
    private readonly ApplicationDbContext _context;

    public ContractService(ApplicationDbContext dbContext) => _context = dbContext;

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

            var propertyOwnerId = await _context.Properties
                .IgnoreQueryFilters()
                .Where(property => property.Id == room.PropertyId)
                .Select(property => property.OwnerId)
                .SingleOrDefaultAsync(cancellationToken);

            if (propertyOwnerId is null)
            {
                throw new ContractNotFoundException(
                    ContractErrorMessages.PropertyNotFound,
                    ContractErrorCodes.PropertyNotFound);
            }

            if (!PropertyScope.IsOwner(propertyOwnerId, caller.UserId))
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

    public async Task<ContractResponseDto> CreateContractAsync(CreateContractRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StartDate >= request.EndDate)
        {
            throw new ArgumentException(
                "Ngày bắt đầu phải trước ngày kết thúc.",
                nameof(request.StartDate));
        }

        if (request.TenantIds is null || request.TenantIds.Count == 0)
        {
            throw new ArgumentException(
                "Hợp đồng phải có ít nhất một khách thuê.",
                nameof(request.TenantIds));
        }

        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(room => room.Id == request.RoomId
                && room.IsActive
                && !room.IsDeleted
                && room.Status == RoomStatus.Available);

        if (room is null)
        {
            throw new ArgumentException(
                "Phòng không tồn tại hoặc hiện không ở trạng thái có thể cho thuê.",
                nameof(request.RoomId));
        }

        var currentRoomPrice = room.StandardPrice;

        var requestedTenantIds = request.TenantIds.Distinct().ToList();
        var existingTenantCount = await _context.Tenants
            .AsNoTracking()
            .Where(tenant => requestedTenantIds.Contains(tenant.Id)
                && tenant.IsActive
                && !tenant.IsDeleted)
            .Select(tenant => tenant.Id)
            .CountAsync();

        if (existingTenantCount != requestedTenantIds.Count)
        {
            throw new ArgumentException(
                "Một hoặc nhiều khách thuê không tồn tại hoặc không còn hoạt động.",
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

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Contracts.Add(contract);
            _context.ContractMembers.AddRange(requestedTenantIds.Select(tenantId => new ContractMember
            {
                ContractId = contract.ContractId,
                TenantId = tenantId,
            }));

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync();
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
}
