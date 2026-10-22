namespace Web.Features.Settings;

/// <summary>Mã lỗi service công tắc Quản lý (VHT-94).</summary>
public static class ManagerToggleErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string UnknownToggle = nameof(UnknownToggle);
    public const string ModeNotAllowed = nameof(ModeNotAllowed);
    public const string InvalidMode = nameof(InvalidMode);
    public const string OperationFailed = nameof(OperationFailed);
}

/// <summary>Thông báo tiếng Việt hiển thị trên UI (NEW-04).</summary>
public static class ManagerToggleErrorMessages
{
    public const string Forbidden = "Chỉ Chủ trọ được xem và đổi công tắc";
    public const string UnknownToggle = "Không tìm thấy công tắc";
    public const string ModeNotAllowed = "Công tắc này chỉ có Cho phép hoặc Không thể";
    public const string InvalidMode = "Chế độ công tắc không hợp lệ";
    public const string OperationFailed = "Không thể thực hiện, vui lòng thử lại";
}
