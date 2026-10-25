using Web.Features.Contracts.DTOs;

namespace Web.Features.Contracts.Services;

public interface IContractService
{
    Task<ContractResponseDto> CreateContractAsync(CreateContractRequest request);
}
