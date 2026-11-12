namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Một lần Chủ trọ chỉnh sửa hoá đơn đã phát hành (khách chưa thanh toán).
/// Khách thuê thấy <see cref="Reason"/> để biết vì sao hoá đơn thay đổi.
/// </summary>
public sealed class InvoiceRevisionDto
{
    /// <summary>Id bản ghi chỉnh sửa (dùng để đánh dấu đã gửi cho khách).</summary>
    public required int Id { get; init; }

    /// <summary>Lý do chỉnh sửa (bắt buộc).</summary>
    public required string Reason { get; init; }

    /// <summary>Tổng tiền trước khi chỉnh sửa, VND.</summary>
    public required decimal PreviousTotalAmount { get; init; }

    /// <summary>Tổng tiền sau khi chỉnh sửa, VND.</summary>
    public required decimal NewTotalAmount { get; init; }

    /// <summary>Thời điểm chỉnh sửa, lưu UTC.</summary>
    public required DateTime ChangedAt { get; init; }
}
