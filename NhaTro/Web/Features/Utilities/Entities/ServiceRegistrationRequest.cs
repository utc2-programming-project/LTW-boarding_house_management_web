namespace Web.Features.Utilities.Entities;

public enum ServiceRegistrationStatus
{
    /// <summary>Chờ chủ trọ duyệt.</summary>
    PendingApproval = 0,

    /// <summary>Đã duyệt; dịch vụ được tính vào hóa đơn qua đăng ký đã duyệt.</summary>
    Approved = 1,

    /// <summary>Đã bị từ chối (bắt buộc có lý do).</summary>
    Rejected = 2,

    /// <summary>Khách hủy (chờ duyệt thì tự hủy; đã duyệt thì qua phê duyệt hủy).</summary>
    Cancelled = 3,
}

public enum ServiceRegistrationCancelStatus
{
    None = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

/// <summary>
/// Yêu cầu đăng ký dịch vụ hằng tháng của khách thuê cho phòng/hợp đồng của mình.
/// Quy trình: PENDING_APPROVAL → APPROVED / REJECTED. Chỉ APPROVED mới được tính vào P5
/// và tạo hóa đơn. Lịch sử đầy đủ (ai duyệt/từ chối, khi nào, lý do) được giữ trên chính dòng này.
/// </summary>
public sealed class ServiceRegistrationRequest
{
    public int Id { get; set; }

    public Guid ContractId { get; set; }

    public int ServiceCatalogId { get; set; }

    /// <summary>Snapshot tên dịch vụ tại lúc đăng ký (để hiển thị kể cả khi danh mục đổi tên).</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Số lượng/số lần sử dụng (mặc định 1).</summary>
    public decimal Quantity { get; set; } = 1m;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Note { get; set; }

    public ServiceRegistrationStatus Status { get; set; } = ServiceRegistrationStatus.PendingApproval;

    public string RequestedByUserId { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? RejectReason { get; set; }

    /// <summary>Yêu cầu hủy khi đã duyệt (bắt buộc lý do).</summary>
    public string? CancelReason { get; set; }

    public ServiceRegistrationCancelStatus CancelStatus { get; set; } = ServiceRegistrationCancelStatus.None;

    public string? CancelReviewedByUserId { get; set; }

    public DateTimeOffset? CancelReviewedAt { get; set; }

    public string? CancelRejectReason { get; set; }

    /// <summary>Hóa đơn đã tạo từ yêu cầu này (nếu có).</summary>
    public int? InvoiceId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
