using Web.Data;
using Web.Features.Contracts.DTOs;
using Web.Features.Rooms;

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

    public Task<ContractResponseDto> CreateContractAsync(CreateContractRequest request)
    {
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

        throw new NotImplementedException(
            "Tạo hợp đồng chưa được triển khai sau bước kiểm tra dữ liệu.");
    }
}
