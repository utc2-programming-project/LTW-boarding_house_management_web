namespace Web.Features.Contracts.Enums;

/// <summary>
/// Trạng thái một yêu cầu gia hạn hợp đồng do khách thuê gửi. Giá trị số lưu nguyên vào SQL
/// nên <b>không đổi thứ tự</b>.
/// </summary>
public enum RenewalRequestStatus
{
    /// <summary>Chờ Ban quản lý xử lý: hợp đồng chưa thay đổi ngày kết thúc.</summary>
    Pending = 0,

    /// <summary>Đã chấp thuận: hợp đồng đã được gia hạn theo yêu cầu.</summary>
    Approved = 1,

    /// <summary>Từ chối: khách thuê nhận được lý do, hợp đồng không đổi.</summary>
    Rejected = 2,
}
