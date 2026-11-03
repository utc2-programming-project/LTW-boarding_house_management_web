namespace Web.Features.Approvals.Enums;

/// <summary>
/// VHT-96 — trạng thái của một yêu cầu duyệt. Giá trị số lưu nguyên vào SQL
/// nên <b>không đổi thứ tự</b>.
/// </summary>
public enum ApprovalRequestStatus
{
    /// <summary>Chờ duyệt: chưa có thay đổi nào được ghi vào dữ liệu nghiệp vụ.</summary>
    Pending = 0,

    /// <summary>Đã duyệt: thao tác đã được áp dụng trong cùng transaction với quyết định.</summary>
    Approved = 1,

    /// <summary>Từ chối: Chủ trọ không đồng ý, phải có lý do, dữ liệu không đổi.</summary>
    Rejected = 2,

    /// <summary>Hủy: Quản lý tự hủy yêu cầu của mình khi còn Chờ duyệt.</summary>
    Cancelled = 3,
}

/// <summary>
/// Loại đối tượng mà yêu cầu tác động. Chỉ dùng để lọc/đọc, quyết định áp dụng
/// nằm ở <c>OperationCode</c> (mã SW).
/// </summary>
public enum ApprovalTargetType
{
    Unknown = 0,
    Room = 1,
    Tenant = 2,
    Contract = 3,
    UtilityPeriod = 4,
    Incident = 5,
}
