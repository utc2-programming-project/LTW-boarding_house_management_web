using Web.Common.SoftDelete;
using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.Entities;

/// <summary>
/// Hóa đơn P6 của một hợp đồng trong một kỳ. Kỳ là khoảng ngày thuần do P5 chốt,
/// tính cả ngày đầu và ngày cuối. Một hợp đồng chỉ có một hóa đơn cho mỗi ngày bắt đầu kỳ.
/// </summary>
public sealed class Invoice : ISoftDeletable
{
    public int Id { get; set; }

    public Guid ContractId { get; set; }

    /// <summary>Ngày đầu kỳ (bao gồm), kiểu date không cộng trừ múi giờ.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Ngày cuối kỳ (bao gồm), kiểu date không cộng trừ múi giờ.</summary>
    public DateOnly PeriodEnd { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Hạn thanh toán; null nghĩa là không áp hạn (hiển thị không quá hạn).</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Yêu cầu đăng ký dịch vụ mà hóa đơn này được tạo từ (nếu có).</summary>
    public int? RegistrationId { get; set; }

    /// <summary>Tổng hóa đơn VND, bằng tổng thành tiền các dòng đã làm tròn.</summary>
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Thời điểm chuyển từ Draft sang Issued; null khi còn nháp.</summary>
    public DateTime? IssuedAt { get; set; }

    /// <summary>
    /// Id bản ghi chỉnh sửa cuối cùng đã gửi cho khách. Null nghĩa là chưa gửi lần nào.
    /// Dùng để biết hoá đơn đã chủ trọ sửa nhưng khách chưa nhận được bản mới.
    /// </summary>
    public int? LastSentRevisionId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}
