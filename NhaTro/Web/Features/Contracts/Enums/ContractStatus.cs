namespace Web.Features.Contracts.Enums;

/// <summary>Trạng thái xử lý hợp đồng theo quy định D3–D4.</summary>
public enum ContractStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Liquidated = 3,

    /// <summary>Chủ trọ/Quản lý đang soạn, chưa gửi khách.</summary>
    Draft = 4,

    /// <summary>Đã gửi khách xem, chờ khách đồng ý hoặc yêu cầu sửa.</summary>
    AwaitingTenant = 5,

    /// <summary>Khách đã gửi yêu cầu chỉnh sửa, chờ Chủ trọ/Quản lý xử lý.</summary>
    ChangeRequested = 6,

    /// <summary>Khách đã đồng ý và xác thực OTP; chờ bước duyệt nội bộ (nếu có).</summary>
    TenantAccepted = 7,

    /// <summary>Hợp đồng bị huỷ (ví dụ quá hạn thanh toán cọc).</summary>
    Cancelled = 8,
}
