using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.Entities;

/// <summary>
/// Một yêu cầu gia hạn hợp đồng do khách thuê gửi từ trang "Phòng đang thuê" (VHT-125/2D).
/// Dòng này là bằng chứng nghiệp vụ: khách gửi gì, gửi lúc nào, đang chờ hay đã xử lý.
/// Việc đổi <c>Contract.EndDate</c> thuộc phân hệ Hợp đồng (P4), không xảy ra ở tầng ghi này.
/// </summary>
public sealed class ContractRenewalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ContractId { get; set; }

    /// <summary>Hồ sơ khách thuê gửi yêu cầu; một hợp đồng có nhiều khách, mỗi người gửi riêng.</summary>
    public int RequestedByTenantId { get; set; }

    /// <summary>Số tháng xin gia hạn, luôn lớn hơn 0.</summary>
    public int RequestedMonths { get; set; }

    /// <summary>Lời nhắn của khách cho Ban quản lý; tối đa 500 ký tự, có thể trống.</summary>
    public string? Note { get; set; }

    public RenewalRequestStatus Status { get; set; } = RenewalRequestStatus.Pending;

    /// <summary>UTC — lấy từ <c>TimeProvider</c>, không dùng giờ hệ thống trực tiếp.</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>Chống hai luồng xử lý cùng một yêu cầu ở tầng ghi.</summary>
    public byte[] RowVersion { get; set; } = [];
}
