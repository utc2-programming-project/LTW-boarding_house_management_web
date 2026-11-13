namespace Web.Features.Utilities.DTOs;

public sealed record ServiceCatalogTrashItemDto(
    int Id,
    int? PropertyId,
    string PropertyName,
    string Name,
    string Unit,
    decimal UnitPrice,
    int PriceVersionCount,
    DateTime? DeletedAt,
    string? DeletedBy);

public sealed record ServiceRegistrationTrashItemDto(
    int Id,
    Guid ContractId,
    string ContractNumber,
    string RoomCode,
    string PropertyName,
    int ServiceCatalogId,
    string ServiceName,
    DateOnly MonthStart,
    DateTime? DeletedAt,
    string? DeletedBy);
