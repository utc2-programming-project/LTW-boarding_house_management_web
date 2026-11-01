using Web.Features.Approvals.DTOs;

namespace Web.Features.Approvals.Services;

/// <summary>
/// VHT-96 — hàng đợi duyệt thao tác Quản lý (NEW-03). Nhận yêu cầu từ service nghiệp vụ
/// (P2/P3/P4/P5), Chủ trọ duyệt/từ chối, Quản lý chỉ xem/hủy yêu cầu của mình.
/// </summary>
public interface IManagerApprovalService
{
    /// <summary>
    /// Tạo yêu cầu Chờ duyệt. Không ghi gì vào dữ liệu nghiệp vụ.
    /// Chỉ Quản lý được gọi (Chủ trọ thao tác trực tiếp, không qua hàng đợi — D4).
    /// </summary>
    Task<ApprovalResult> CreateAsync(
        CreateApprovalRequest request, ApprovalCaller caller, CancellationToken ct = default);

    /// <summary>Quản lý xem yêu cầu của chính mình (mọi trạng thái).</summary>
    Task<ApprovalListResult> ListMineAsync(ApprovalCaller caller, CancellationToken ct = default);

    /// <summary>Chủ trọ xem yêu cầu còn Chờ duyệt thuộc tòa mình sở hữu.</summary>
    Task<ApprovalListResult> ListInboxAsync(ApprovalCaller caller, CancellationToken ct = default);

    /// <summary>Quản lý hủy yêu cầu của mình khi còn Chờ duyệt.</summary>
    Task<ApprovalResult> CancelAsync(Guid id, ApprovalCaller caller, CancellationToken ct = default);

    /// <summary>Chủ trọ duyệt: kiểm token, áp dụng thao tác và ghi quyết định trong một transaction.</summary>
    Task<ApprovalResult> ApproveAsync(Guid id, ApprovalCaller caller, CancellationToken ct = default);

    /// <summary>Chủ trọ từ chối: bắt buộc lý do, không đổi dữ liệu nghiệp vụ.</summary>
    Task<ApprovalResult> RejectAsync(
        Guid id, RejectApprovalRequest request, ApprovalCaller caller, CancellationToken ct = default);
}
