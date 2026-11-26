using Web.Features.Approvals.Enums;

namespace Web.Features.Approvals.DTOs;

/// <summary>
/// Một dòng của Hàng đợi duyệt đã được làm giàu để hiển thị: tên đối tượng,
/// người gửi và phần đề xuất đã đọc từ payload. Service không trả chuỗi đã dịch;
/// <see cref="ApprovalQueueLine"/> giữ khóa resource để tầng giao diện tự dịch.
/// </summary>
public sealed record ApprovalQueueItem(
    Guid Id,
    string OperationCode,
    string Group,
    ApprovalTargetType TargetType,
    string TargetLabel,
    string RequesterName,
    DateTime RequestedAt,
    ApprovalRequestStatus Status,
    string? DecisionReason,
    DateTime? DecidedAt,
    bool CanApply,
    IReadOnlyList<ApprovalQueueLine> Details);

/// <summary>
/// Một dòng "nhãn — giá trị" trong phần nội dung đề xuất. <see cref="Value"/> là chuỗi
/// đã định dạng (ngày, tiền, số); <see cref="ValueKey"/> dùng khi giá trị là nhãn trạng thái
/// cần dịch theo ngôn ngữ giao diện.
/// </summary>
public sealed record ApprovalQueueLine(string LabelKey, string? Value = null, string? ValueKey = null);

/// <summary>Kết quả đọc Hàng đợi duyệt cho giao diện.</summary>
public sealed class ApprovalQueueResult
{
    private ApprovalQueueResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<ApprovalQueueItem> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static ApprovalQueueResult Success(IReadOnlyList<ApprovalQueueItem> items) =>
        new() { Succeeded = true, Items = items };

    public static ApprovalQueueResult Failure(string? errorCode, string? errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Một trang Hàng đợi duyệt: chỉ trang hiện tại được làm giàu,
/// số liệu thẻ KPI lấy từ aggregate SQL.
/// </summary>
public sealed class ApprovalQueuePageResult
{
    private ApprovalQueuePageResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<ApprovalQueueItem> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public ApprovalInboxStats Stats { get; init; } = new(0, 0, null);

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static ApprovalQueuePageResult Success(
        IReadOnlyList<ApprovalQueueItem> items, int totalCount, int page, int pageSize, ApprovalInboxStats stats) =>
        new()
        {
            Succeeded = true,
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Stats = stats,
        };

    public static ApprovalQueuePageResult Failure(string? errorCode, string? errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
