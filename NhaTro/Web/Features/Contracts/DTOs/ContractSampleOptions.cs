namespace Web.Features.Contracts.DTOs;

public sealed record ContractPropertySample(int Id, string Name, string Address);

public sealed record ContractRoomSample(
    int Id,
    int PropertyId,
    string Code,
    int Floor,
    decimal AreaM2,
    decimal StandardRentVnd,
    bool IsAvailable,
    DateOnly? AvailableFrom = null)
{
    public bool IsAvailableForLeaseStart(DateOnly? leaseStart) =>
        IsAvailable || (AvailableFrom is DateOnly availableFrom && leaseStart is DateOnly start && availableFrom <= start);
}

public sealed record ContractTenantSample(int Id, string FullName, string PhoneNumber, string MaskedIdNumber);

public sealed record ContractUtilitySample(string NameKey, decimal AmountVnd, string UnitKey);
