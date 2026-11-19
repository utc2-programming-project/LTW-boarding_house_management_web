namespace Web.Features.Rooms.DTOs;

// Hợp đồng đang hiệu lực của phòng kèm người thuê, để dialog chi tiết phòng
// trả lời được "ai đang thuê" thay vì chỉ hiện trạng thái Đang thuê.
public sealed class RoomRentalDetail
{
    public Guid ContractId { get; set; }

    public string ContractNumber { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public IReadOnlyList<RoomTenantDetail> Tenants { get; set; } = [];
}

public sealed class RoomTenantDetail
{
    public int TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Relationship { get; set; }

    public bool IsRepresentative { get; set; }
}
