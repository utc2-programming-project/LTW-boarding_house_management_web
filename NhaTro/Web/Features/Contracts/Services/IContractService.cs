using Web.Features.Contracts.DTOs;
using Web.Features.Properties;

namespace Web.Features.Contracts.Services;

public interface IContractService
{
    Task<ContractCreateOptionsDto> GetCreateOptionsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractListItemDto>> ListAsync(
        string? keyword,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractDetailDto> GetAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractResponseDto> CreateContractAsync(
        CreateContractRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractResponseDto> ApproveContractAsync(
        Guid contractId,
        ApproveContractRequestDto request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractActionResultDto> RenewContractAsync(
        Guid contractId,
        RenewContractRequestDto request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractActionResultDto> LiquidateContractAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
