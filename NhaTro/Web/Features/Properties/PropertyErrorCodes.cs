namespace Web.Features.Properties;

/// <summary>Mã lỗi service nhà trọ P2 (đợt 1C).</summary>
public static class PropertyErrorCodes
{
    public const string InvalidRequest = nameof(InvalidRequest);
    public const string Forbidden = nameof(Forbidden);
    public const string PropertyNotFound = nameof(PropertyNotFound);
    public const string PropertyHasRooms = nameof(PropertyHasRooms);
    public const string PropertyOperationFailed = nameof(PropertyOperationFailed);
    public const string ApprovalRequired = nameof(ApprovalRequired);
}

/// <summary>Thông báo lỗi tiếng Việt hiển thị trên UI.</summary>
public static class PropertyErrorMessages
{
    public const string InvalidRequest = "Dữ liệu gửi lên không hợp lệ";
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này";
    public const string PropertyNotFound = "Không tìm thấy nhà trọ";
    public const string PropertyHasRooms = "Nhà trọ còn phòng, không thể xóa";
    public const string PropertyOperationFailed = "Không thể thực hiện, vui lòng thử lại";
    public const string ApprovalRequired = "Thao tác đang chờ Chủ trọ duyệt";
    public const string InvalidName = "Tên nhà trọ là bắt buộc";
    public const string InvalidLength = "Dữ liệu vượt quá độ dài cho phép";
}
