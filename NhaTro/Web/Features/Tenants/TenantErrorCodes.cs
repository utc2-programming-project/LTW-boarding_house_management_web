namespace Web.Features.Tenants;

/// <summary>Mã lỗi trả về của service khách thuê (mục 9).</summary>
public static class TenantErrorCodes
{
    public const string InvalidRequest = nameof(InvalidRequest);
    public const string Unauthorized = nameof(Unauthorized);
    public const string Forbidden = nameof(Forbidden);
    public const string TenantNotFound = nameof(TenantNotFound);
    public const string DuplicateTenant = nameof(DuplicateTenant);
    public const string ConcurrencyConflict = nameof(ConcurrencyConflict);
    public const string TenantInUse = nameof(TenantInUse);
    public const string AccountAlreadyLinked = nameof(AccountAlreadyLinked);
    public const string TenantOperationFailed = nameof(TenantOperationFailed);

    /// <summary>
    /// Nghi trùng số điện thoại cần xác nhận (mục 3). Chưa có trong 9 mã ở mục 9,
    /// cần nhóm chốt tên mã; tạm dùng để UI hỏi "vẫn lưu".
    /// </summary>
    public const string DuplicatePhone = nameof(DuplicatePhone);
}

/// <summary>Thông báo lỗi tiếng Việt hiển thị (mục 9).</summary>
public static class TenantErrorMessages
{
    public const string InvalidRequest = "Dữ liệu gửi lên không hợp lệ";
    public const string Unauthorized = "Vui lòng đăng nhập để tiếp tục";
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này";
    public const string TenantNotFound = "Không tìm thấy khách thuê";
    public const string DuplicateTenant = "Đã có khách thuê với số giấy tờ này";
    public const string DuplicatePhone = "Đã có khách thuê với số điện thoại này, xác nhận để tiếp tục";
    public const string ConcurrencyConflict = "Hồ sơ vừa được cập nhật, vui lòng tải lại";
    public const string TenantInUse = "Khách thuê đang có hợp đồng, không thể ngừng sử dụng";
    public const string AccountAlreadyLinked = "Tài khoản này đã được gắn với một hồ sơ khác";
    public const string TenantOperationFailed = "Không thể thực hiện, vui lòng thử lại";
    public const string InvalidPhone = "Số điện thoại không hợp lệ (10-11 chữ số, bắt đầu bằng 0)";
    public const string InvalidDateOfBirth = "Ngày sinh không được ở tương lai";
    public const string InvalidIdIssuedDate = "Ngày cấp giấy tờ không được trước ngày sinh";
    public const string InvalidIdNumber = "Số giấy tờ không hợp lệ (tối thiểu 8 chữ hoặc số, không chứa dấu *)";

    /// <summary>
    /// Vượt độ dài cột DB sau chuẩn hóa. DTO có [MaxLength] nhưng nó chạy trên chuỗi thô
    /// TRƯỚC Normalize (có thể dài hơn kết quả), nên service phải kiểm lại sau chuẩn hóa.
    /// </summary>
    public const string InvalidLength = "Dữ liệu vượt quá độ dài cho phép";
    public const string AccountNotFound = "Không tìm thấy tài khoản";
    public const string AccountNotTenant = "Tài khoản không phải khách thuê";
}
