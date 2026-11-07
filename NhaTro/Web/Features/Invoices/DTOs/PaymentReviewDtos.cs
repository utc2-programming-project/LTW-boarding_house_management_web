using System.ComponentModel.DataAnnotations;
using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Bộ lọc hàng đợi yêu cầu thanh toán cho Quản lý/Chủ trọ (VHT-56).
/// Mặc định chỉ xem yêu cầu đang chờ để xử lý nhanh.
/// </summary>
public sealed class PaymentReviewFilter
{
    public PaymentStatus? Status { get; set; } = PaymentStatus.Pending;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        Page = Math.Max(1, Page);
        PageSize = Math.Clamp(PageSize, 1, 100);
    }
}

/// <summary>
/// Một yêu cầu thanh toán kèm thông tin hóa đơn/phòng/khách để Quản lý đối chiếu.
/// Không chứa đường dẫn tệp nội bộ; minh chứng mở qua endpoint tệp đã phân quyền.
/// </summary>
public sealed class PaymentReviewItemDto
{
    public required PaymentDto Payment { get; init; }

    public required int InvoiceId { get; init; }

    public required string ContractNumber { get; init; }

    public required string RoomCode { get; init; }

    public required string PropertyName { get; init; }

    public required IReadOnlyList<string> TenantNames { get; init; }

    public required DateOnly PeriodStart { get; init; }

    public required DateOnly PeriodEnd { get; init; }

    public required InvoiceStatus InvoiceStatus { get; init; }

    public required decimal InvoiceTotal { get; init; }
}

/// <summary>Tổng hợp hàng đợi để hiển thị KPI, không phụ thuộc trang hiện tại.</summary>
public sealed class PaymentReviewSummaryDto
{
    public required int PendingCount { get; init; }

    public required int ConfirmedCount { get; init; }

    public required int RejectedCount { get; init; }

    /// <summary>Tổng tiền các yêu cầu đang chờ; chưa phải tiền đã thu.</summary>
    public required decimal PendingAmount { get; init; }
}

public sealed class PaymentReviewListResult
{
    public required IReadOnlyList<PaymentReviewItemDto> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required PaymentReviewSummaryDto Summary { get; init; }
}
