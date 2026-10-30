namespace Web.Features.Properties;

/// <summary>Mã lỗi service phân công Quản lý theo tòa nhà (VHT-24).</summary>
public static class PropertyManagerErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string PropertyNotFound = nameof(PropertyNotFound);
    public const string ManagerNotFound = nameof(ManagerNotFound);
    public const string InvalidManagerRole = nameof(InvalidManagerRole);
    public const string AlreadyAssigned = nameof(AlreadyAssigned);
    public const string NotAssigned = nameof(NotAssigned);
    public const string OperationFailed = nameof(OperationFailed);
}

/// <summary>Thông báo lỗi tiếng Việt hiển thị trên UI.</summary>
public static class PropertyManagerErrorMessages
{
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này";
    public const string PropertyNotFound = "Không tìm thấy nhà trọ";
    public const string ManagerNotFound = "Không tìm thấy tài khoản Quản lý";
    public const string InvalidManagerRole = "Tài khoản được chọn không phải Quản lý";
    public const string AlreadyAssigned = "Quản lý đã được phân công cho nhà trọ này";
    public const string NotAssigned = "Quản lý chưa được phân công cho nhà trọ này";
    public const string OperationFailed = "Không thể thực hiện, vui lòng thử lại";
}
