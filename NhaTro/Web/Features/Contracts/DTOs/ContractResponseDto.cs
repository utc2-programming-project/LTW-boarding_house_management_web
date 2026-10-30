using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.DTOs;

/// <summary>Kết quả tạo hợp đồng cùng giá và tiền cọc được lưu làm snapshot.</summary>
public sealed record ContractResponseDto(
    Guid ContractId,
    string ContractNumber,
    int RoomId,
    DateTime StartDate,
    DateTime EndDate,
    decimal SnapshotPrice,
    decimal DepositAmount,
    ContractStatus Status,
    string? RejectReason = null);
