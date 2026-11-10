using Web.Features.Contracts.DTOs;
using Web.Features.Properties;
using Web.Features.Tenants;

namespace Web.Features.Contracts.Services;

/// <summary>
/// Luồng "Gửi yêu cầu gia hạn phòng" (VHT-125/2D) và duyệt yêu cầu của Ban quản lý (VHT-125/2E).
/// Service tự kiểm quyền và phạm vi bên trong; chấp thuận yêu cầu delegates việc đổi hợp đồng cho P4.
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
    /// Lịch sử mọi yêu cầu của chính hợp đồng đó, mới nhất trước — tab "Kết quả &amp; lịch sử"
    /// phía khách thuê đọc danh sách này, phần tử đầu là yêu cầu hiện hành.
    /// </summary>
    Task<RenewalHistoryResult> ListForContractAsync(
        TenantCaller caller,
        Guid contractId,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách yêu cầu đang chờ trên các nhà trọ thuộc phạm vi của Chủ trọ / Quản lý.</summary>
    Task<RenewalRequestListResult> ListPendingAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chấp thuận (gia hạn hợp đồng sang ngày kết thúc mới = hiện tại + số tháng khách xin)
    /// hoặc từ chối kèm lý do. Quản lý đi qua công tắc SW-07 của P4.
    /// </summary>
    Task<ContractRenewalResult> DecideAsync(
        Guid requestId,
        RenewalDecisionRequest decision,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
