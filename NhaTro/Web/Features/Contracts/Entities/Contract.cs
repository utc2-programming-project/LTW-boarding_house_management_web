using Web.Common.SoftDelete;
using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.Entities;

/// <summary>Snapshot dữ liệu chính của hợp đồng thuê.</summary>
public sealed class Contract : ISoftDeletable
{
    public Guid ContractId { get; set; } = Guid.NewGuid();

    public string ContractNumber { get; set; } = string.Empty;

    public int RoomId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal SnapshotPrice { get; set; }

    public decimal DepositAmount { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Pending;

    public string? RejectReason { get; set; }

    /// <summary>Số tháng mỗi kỳ thanh toán (1, 2, 3, 6, 12).</summary>
    public int PaymentCycleMonths { get; set; } = 1;

    /// <summary>Ngày chốt số điện/nước trong tháng (1–28); null = ngày cuối tháng.</summary>
    public int? MeterReadingDay { get; set; }

    /// <summary>Điều khoản bổ sung do người lập hợp đồng nhập (tối đa 4000 ký tự).</summary>
    public string? Terms { get; set; }

    /// <summary>Người tạo hợp đồng (audit + quyết định bước duyệt sau khi khách đồng ý).</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>Vai trò người tạo: ChuTro hoặc QuanLy.</summary>
    public string? CreatedByRole { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Hạn thanh toán cọc sau khi khách ký; null khi không còn hạn/chưa ký.</summary>
    public DateTimeOffset? DepositDueAt { get; set; }

    /// <summary>Số tiền cọc chủ trọ đã thực nhận; null = chưa ghi nhận thu cọc.</summary>
    public decimal? DepositPaidAmount { get; set; }

    public DateTimeOffset? DepositPaidAt { get; set; }

    public string? DepositPaidByUserId { get; set; }

    /// <summary>Số tiền cọc cần hoàn khi hủy/từ chối/thanh lý; null = không có nghĩa vụ hoàn.</summary>
    public decimal? DepositRefundAmount { get; set; }

    public DateTimeOffset? DepositRefundedAt { get; set; }

    /// <summary>Đã gửi nhắc hạn cọc 24 giờ / 6 giờ hay chưa (tránh spam mỗi 15 phút).</summary>
    public DateTimeOffset? DepositReminder24hAt { get; set; }

    public DateTimeOffset? DepositReminder6hAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public ICollection<ContractMember> Members { get; set; } = new List<ContractMember>();
}
