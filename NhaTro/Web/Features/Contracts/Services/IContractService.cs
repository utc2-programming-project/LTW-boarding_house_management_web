using Web.Features.Contracts.DTOs;
using Web.Features.Properties;

namespace Web.Features.Contracts.Services;

public interface IContractService
{
    Task<ContractResponseDto> CreateContractAsync(CreateContractRequest request);

    Task<ContractResponseDto> ApproveContractAsync(
        Guid contractId,
        ApproveContractRequestDto request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
