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

    /// <summary>OP27 / SW-08: ghi nhận tiền cọc; Quản lý đi qua công tắc, Chủ trọ áp dụng ngay.</summary>
    Task<ContractActionResultDto> UpdateDepositAsync(
        Guid contractId,
        decimal depositAmount,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
