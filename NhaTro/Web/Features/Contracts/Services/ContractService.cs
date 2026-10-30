using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Rooms;
using Web.Features.Rooms.Enums;

namespace Web.Features.Contracts.Services;

public sealed class ContractService : IContractService
{
    private readonly ApplicationDbContext _context;
    private IRoomService RoomService { get; }

    public ContractService(ApplicationDbContext dbContext, IRoomService roomService)
    {
        _context = dbContext;
        RoomService = roomService;
    }

    public async Task<ContractResponseDto> ApproveContractAsync(
        Guid contractId,
        ApproveContractRequestDto request,
        int userId,
        string userRole)
    {
        if (userRole is not (Roles.ChuTro or "Landlord"))
        {
            throw new UnauthorizedAccessException("Only the landlord can approve or reject a contract.");
        }

        ArgumentNullException.ThrowIfNull(request);

        if (!request.IsApproved && string.IsNullOrWhiteSpace(request.RejectReason))
        {
            throw new ArgumentException(
                "A rejection reason is required when rejecting a contract.",
                nameof(request));
        }

        var contract = await _context.Contracts
            .FirstOrDefaultAsync(item => item.ContractId == contractId);

        if (contract is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy hợp đồng {contractId}.");
        }

        if (contract.Status != ContractStatus.Pending)
        {
            throw new InvalidOperationException("Chỉ hợp đồng đang chờ duyệt mới được xử lý.");
        }

        throw new NotImplementedException();
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
            await transaction.RollbackAsync();
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
            contract.Status);
    }
}
