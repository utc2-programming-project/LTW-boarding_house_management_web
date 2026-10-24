using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Rooms;
using Web.Features.Rooms.Enums;

namespace Web.Features.Contracts.Services;

public sealed class ContractService : IContractService
{
    private ApplicationDbContext DbContext { get; }
    private IRoomService RoomService { get; }

    public ContractService(ApplicationDbContext dbContext, IRoomService roomService)
    {
        DbContext = dbContext;
        RoomService = roomService;
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

        var room = await DbContext.Rooms
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
        var existingTenantCount = await DbContext.Tenants
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

        var depositAmount = currentRoomPrice * 0.20m;
        _ = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = $"HD-{Guid.NewGuid():N}".ToUpperInvariant(),
            RoomId = room.Id,
            SnapshotPrice = currentRoomPrice,
            DepositAmount = depositAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = ContractStatus.Pending,
        };

        throw new NotImplementedException(
            "Tạo hợp đồng chưa được triển khai sau bước kiểm tra dữ liệu.");
    }
}
