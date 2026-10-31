namespace Web.Features.Tenants.DTOs;

/// <summary>
/// DTO chứa thông tin phòng đang thuê từ hợp đồng thật (không suy từ Room.Status) cho màn hình MyLeasedRoomView.
/// </summary>
public sealed class LeasedRoomDetailDto
{
    public required Guid ContractId { get; init; }

    public required string ContractNumber { get; init; }

    public required int RoomId { get; init; }

    public required string RoomCode { get; init; }

    public int? Floor { get; init; }

    public decimal? AreaM2 { get; init; }

    public required string PropertyName { get; init; }

    public string? PropertyAddress { get; init; }

    public required DateTime StartDate { get; init; }

    public required DateTime EndDate { get; init; }

    public required decimal MonthlyRent { get; init; }

    public required decimal DepositAmount { get; init; }

    public required string StatusLabel { get; init; }

    public required bool IsActive { get; init; }

    public required IReadOnlyList<RoommateDto> Roommates { get; init; }
}

public sealed class RoommateDto
{
    public required int TenantId { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public bool IsCurrentUser { get; init; }
}
