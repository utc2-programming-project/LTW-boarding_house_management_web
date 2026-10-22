namespace Web.Features.Settings.Enums;

/// <summary>
/// Chế độ của một công tắc Quản lý (VHT-94).
/// Giá trị số được lưu nguyên vào SQL nên <b>không đổi thứ tự</b>.
/// </summary>
public enum ManagerToggleMode
{
    /// <summary>Cho phép: Quản lý thực thi ngay.</summary>
    Allow = 0,

    /// <summary>Duyệt: trả "cần tạo yêu cầu", chờ Chủ trọ duyệt ở hàng đợi NEW-03 (VHT-96).</summary>
    Approve = 1,

    /// <summary>Không thể: từ chối ngay, không tạo yêu cầu.</summary>
    Deny = 2,
}
