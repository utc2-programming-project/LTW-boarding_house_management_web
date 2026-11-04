using Web.Features.Contracts.DTOs;
using Web.Features.Tenants;

namespace Web.Features.Contracts.Services;

/// <summary>
/// Luồng "Gửi yêu cầu gia hạn phòng" cho khách thuê (VHT-125/2D). Chỉ ghi yêu cầu ở trạng thái
/// chờ xử lý; đổi ngày kết thúc hợp đồng thuộc phân hệ P4.
/// </summary>
public interface IContractRenewalService
{
    /// <summary>Ghi một yêu cầu gia hạn cho hợp đồng đang hiệu lực của chính khách thuê.</summary>
    Task<ContractRenewalResult> SubmitAsync(
        TenantCaller caller,
        Guid contractId,
        int requestedMonths,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy yêu cầu đang chờ của hợp đồng (khách cùng phòng thấy chung một yêu cầu),
    /// hoặc <c>null</c> khi chưa có.
    /// </summary>
    Task<ContractRenewalResult> GetPendingAsync(
        TenantCaller caller,
        Guid contractId,
        CancellationToken cancellationToken = default);
}
