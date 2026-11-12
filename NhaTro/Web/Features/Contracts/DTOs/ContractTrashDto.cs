using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.DTOs;

public sealed record ContractTrashItemDto(
    Guid ContractId,
    string ContractNumber,
    string RoomCode,
    string PropertyName,
    ContractStatus Status,
    DateTime StartDate,
    DateTime EndDate,
    DateTime? DeletedAt,
    string? DeletedBy);
