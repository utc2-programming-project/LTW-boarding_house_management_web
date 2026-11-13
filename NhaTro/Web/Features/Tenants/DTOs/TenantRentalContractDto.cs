using Web.Features.Contracts.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Một hợp đồng thuê gắn với hồ sơ khách, kèm phòng/nhà trọ lấy từ liên kết thật
/// (Tenant -> ContractMember -> Contract -> Room -> Property), không suy từ trạng thái <c>Room.Status</c>.
/// </summary>
public sealed class TenantRentalContractDto
{
    public required Guid ContractId { get; init; }

    public required string ContractNumber { get; init; }

    public required int RoomId { get; init; }

    public required string RoomCode { get; init; }

    public required int PropertyId { get; init; }

    public required string PropertyName { get; init; }

    // Bổ sung bởi VHT-36-2B (Phát approved)
    public string? PropertyAddress { get; init; }

    // Bổ sung bởi VHT-36-2B (Phát approved)
    public decimal? AreaM2 { get; init; }

    // Bổ sung bởi VHT-36-2B (Phát approved)
    public IReadOnlyList<TenantRoommateDto> Roommates { get; init; } = [];

    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public required decimal SnapshotPrice { get; init; }

    public required decimal DepositAmount { get; init; }

    /// <summary>Số tháng mỗi kỳ thanh toán (1, 2, 3, 6, 12).</summary>
    public int PaymentCycleMonths { get; init; } = 1;

    /// <summary>Ngày chốt điện/nước trong tháng; null = ngày cuối tháng.</summary>
    public int? MeterReadingDay { get; init; }

    /// <summary>Điều khoản bổ sung do Chủ trọ nhập; null khi không có.</summary>
    public string? Terms { get; init; }

    /// <summary>
    /// Tiền cọc đã đóng thực tế và ngày thanh toán (null khi chưa có biên lai/chứng từ ghi nhận).
    /// </summary>
    public decimal? PaidDepositAmount { get; init; }

    public DateOnly? DepositPaidDate { get; init; }

    /// <summary>Hạn thanh toán cọc sau khi ký; null khi đã thu hoặc chưa ký.</summary>
    public DateOnly? DepositDueDate { get; init; }

    public required ContractStatus Status { get; init; }

    /// <summary>
    /// True khi hợp đồng đã duyệt và hôm nay nằm trong khoảng ngày thuê.
    /// Chỉ một cờ này quyết định "đang thuê"; không dựa vào <c>Room.Status</c>.
    /// </summary>
    public required bool IsCurrent { get; init; }
}
