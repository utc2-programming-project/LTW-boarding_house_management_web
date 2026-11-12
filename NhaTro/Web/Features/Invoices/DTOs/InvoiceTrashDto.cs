using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.DTOs;

public sealed record InvoiceTrashItemDto(
    int Id,
    string ContractNumber,
    string RoomCode,
    string PropertyName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TotalAmount,
    InvoiceStatus Status,
    DateTime? DeletedAt,
    string? DeletedBy);
