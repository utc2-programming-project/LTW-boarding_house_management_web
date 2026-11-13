namespace Web.Features.Invoices.Entities;

/// <summary>
/// Lịch sử Chủ trọ chỉnh sửa hoá đơn đã phát hành (chưa thanh toán).
/// Mỗi lần sửa tạo một bản ghi: lý do bắt buộc + tổng tiền trước/sau để
/// khách thuê thấy "hoá đơn đã được chỉnh sửa" kèm nội dung lý do.
/// </summary>
public sealed class InvoiceRevision
{
    public int Id { get; set; }

    /// <summary>Id hoá đơn được chỉnh sửa (liên kết logic, không đặt FK constraint).</summary>
    public int InvoiceId { get; set; }

    /// <summary>Lý do chỉnh sửa do Chủ trọ nhập (bắt buộc), tối đa 1000 ký tự.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Tổng tiền trước khi chỉnh sửa, VND.</summary>
    public decimal PreviousTotalAmount { get; set; }

    /// <summary>Tổng tiền sau khi chỉnh sửa, VND.</summary>
    public decimal NewTotalAmount { get; set; }

    /// <summary>Thời điểm chỉnh sửa, lưu UTC.</summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>Người thực hiện chỉnh sửa (Chủ trọ).</summary>
    public string ChangedByUserId { get; set; } = string.Empty;
}
