namespace Web.Features.Contracts.DTOs;

public sealed record ContractCreateOptionsDto(
    IReadOnlyList<ContractPropertyOptionDto> Properties,
    IReadOnlyList<ContractRoomOptionDto> Rooms,
    IReadOnlyList<ContractTenantOptionDto> Tenants);

public sealed record ContractPropertyOptionDto(int Id, string Name);
public sealed record ContractRoomOptionDto(int Id, int PropertyId, string Code, decimal StandardPrice);
public sealed record ContractTenantOptionDto(int Id, string FullName, string Phone);
