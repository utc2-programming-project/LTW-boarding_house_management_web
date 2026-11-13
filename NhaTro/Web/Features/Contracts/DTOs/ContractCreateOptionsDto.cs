namespace Web.Features.Contracts.DTOs;

public sealed record ContractCreateOptionsDto(
    IReadOnlyList<ContractPropertyOptionDto> Properties,
    IReadOnlyList<ContractRoomOptionDto> Rooms,
    IReadOnlyList<ContractTenantOptionDto> Tenants,
    IReadOnlyList<ContractServiceOptionDto> Services);

public sealed record ContractPropertyOptionDto(int Id, string Name, string? Address);

public sealed record ContractRoomOptionDto(
    int Id,
    int PropertyId,
    string Code,
    decimal StandardPrice,
    int? Floor,
    decimal? AreaM2);

public sealed record ContractTenantOptionDto(
    int Id,
    string FullName,
    string Phone,
    string? Email,
    string? CitizenId,
    string? PermanentAddress,
    string? IdIssuedPlace,
    DateOnly? DateOfBirth);

/// <summary>Dịch vụ của nhà trọ kèm giá đang áp, dùng để hiển thị trong hợp đồng (chỉ đọc).</summary>
public sealed record ContractServiceOptionDto(
    int ServiceCatalogId,
    int? PropertyId,
    string Name,
    string Unit,
    string CalculationMethod,
    decimal UnitPrice);
