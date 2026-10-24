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
        throw new NotImplementedException();
    }
}
