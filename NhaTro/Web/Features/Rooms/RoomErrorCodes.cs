namespace Web.Features.Rooms;

/// <summary>Mã lỗi service phòng P2 (đợt 1C).</summary>
public static class RoomErrorCodes
{
    public const string InvalidRequest = nameof(InvalidRequest);
    public const string Forbidden = nameof(Forbidden);
    public const string RoomNotFound = nameof(RoomNotFound);
    public const string PropertyNotFound = nameof(PropertyNotFound);
    public const string DuplicateRoomCode = nameof(DuplicateRoomCode);
    public const string RoomInUse = nameof(RoomInUse);
    public const string ApprovalRequired = nameof(ApprovalRequired);
    public const string RoomOperationFailed = nameof(RoomOperationFailed);
    public const string InactiveRoomStatus = nameof(InactiveRoomStatus);
}

/// <summary>Thông báo lỗi tiếng Việt hiển thị trên UI.</summary>
public static class RoomErrorMessages
{
    public const string InvalidRequest = "Dữ liệu gửi lên không hợp lệ";
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này";
    public const string RoomNotFound = "Không tìm thấy phòng";
    public const string PropertyNotFound = "Không tìm thấy nhà trọ";
    public const string DuplicateRoomCode = "Mã phòng đã tồn tại trong nhà trọ này";
    public const string RoomInUse = "Phòng đang có hợp đồng hiệu lực, không thể xóa";
    public const string ApprovalRequired = "Thay đổi đang chờ Chủ trọ duyệt";
    public const string RoomOperationFailed = "Không thể thực hiện, vui lòng thử lại";
    public const string InvalidLength = "Dữ liệu vượt quá độ dài cho phép";
    public const string InvalidPrice = "Giá phòng không hợp lệ";
    public const string InvalidArea = "Diện tích phòng không hợp lệ";
    public const string InvalidFloor = "Tầng không hợp lệ";
    public const string InvalidStatus = "Trạng thái phòng không hợp lệ";
    public const string InactiveRoomStatus = "Phòng ngừng hoạt động phải ở trạng thái Còn trống";
}
