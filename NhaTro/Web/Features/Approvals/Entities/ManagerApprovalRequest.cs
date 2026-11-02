using Web.Features.Approvals.Enums;

namespace Web.Features.Approvals.Entities;

/// <summary>
/// VHT-96 — một yêu cầu Chủ trọ duyệt cho thao tác ⚙️ của Quản lý (NEW-03).
/// Bảng cũng là <b>lịch sử thao tác</b>: sau khi có quyết định, dòng không được sửa lại
/// (service chỉ đổi trạng thái đúng một lần, từ <see cref="ApprovalRequestStatus.Pending"/>).
/// </summary>
public class ManagerApprovalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã công tắc SW-01…SW-16 (xem <c>ManagerToggleCatalog</c>).</summary>
    public string OperationCode { get; set; } = string.Empty;

    public ApprovalTargetType TargetType { get; set; }

    /// <summary>
    /// Khóa đối tượng bị tác động (ví dụ Id phòng dạng chuỗi). Với thao tác tạo mới
    /// chưa có đối tượng, caller truyền khóa tạm riêng để không bị chặn trùng.
    /// </summary>
    public string TargetKey { get; set; } = string.Empty;

    /// <summary>Tòa nhà để lọc phạm vi Chủ trọ; null khi đối tượng chưa gắn tòa.</summary>
    public int? PropertyId { get; set; }

    /// <summary>Dữ liệu đề xuất, JSON do caller của mã SW tạo và applier đọc lại.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Concurrency token của đối tượng tại thời điểm tạo yêu cầu (rowversion).
    /// Applier gán vào <c>OriginalValues</c>; lệch ⇒ dữ liệu đã đổi ⇒ báo xung đột.
    /// </summary>
    public byte[]? ExpectedToken { get; set; }

    public ApprovalRequestStatus Status { get; set; } = ApprovalRequestStatus.Pending;

    public string RequestedByUserId { get; set; } = string.Empty;

    /// <summary>UTC — lấy từ <c>TimeProvider</c>, không dùng giờ hệ thống trực tiếp.</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>Người quyết định (Chủ trọ duyệt/từ chối, hoặc Quản lý hủy).</summary>
    public string? DecidedByUserId { get; set; }

    public DateTime? DecidedAt { get; set; }

    /// <summary>Lý do từ chối (bắt buộc khi từ chối, tối đa 500 ký tự).</summary>
    public string? DecisionReason { get; set; }

    /// <summary>Chống duyệt trùng ở tầng ghi: hai luồng duyệt cùng lúc chỉ một luồng thắng.</summary>
    public byte[] RowVersion { get; set; } = [];
}
