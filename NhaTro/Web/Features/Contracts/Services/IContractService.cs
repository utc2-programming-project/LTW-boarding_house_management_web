using Web.Features.Contracts.DTOs;

namespace Web.Features.Contracts.Services;

public interface IContractService
{
    Task<ContractResponseDto> CreateContractAsync(CreateContractRequest request);

    Task<ContractResponseDto> ApproveContractAsync(
        Guid contractId,
        ApproveContractRequestDto request,
        int userId,
        string userRole);
}
