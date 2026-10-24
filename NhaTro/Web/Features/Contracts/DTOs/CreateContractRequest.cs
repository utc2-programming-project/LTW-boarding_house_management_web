namespace Web.Features.Contracts.DTOs;

/// <summary>Dữ liệu cần thiết để yêu cầu tạo hợp đồng thuê.</summary>
public sealed record CreateContractRequest(
    Guid RoomId,
    DateTime StartDate,
    DateTime EndDate,
    List<Guid> TenantIds);
