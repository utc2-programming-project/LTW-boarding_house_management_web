namespace Web.Features.Approvals;

/// <summary>Mã lỗi trả về của service hàng đợi duyệt (VHT-96).</summary>
public static class ApprovalErrorCodes
{
    public const string InvalidRequest = nameof(InvalidRequest);
    public const string Forbidden = nameof(Forbidden);
    public const string NotFound = nameof(NotFound);
    public const string AlreadyDecided = nameof(AlreadyDecided);
    public const string Conflict = nameof(Conflict);
    public const string RejectReasonRequired = nameof(RejectReasonRequired);
    public const string RejectReasonTooLong = nameof(RejectReasonTooLong);
    public const string UnknownOperationCode = nameof(UnknownOperationCode);
    public const string ApplierNotRegistered = nameof(ApplierNotRegistered);
    public const string DuplicatePendingRequest = nameof(DuplicatePendingRequest);
    public const string OperationFailed = nameof(OperationFailed);
}

/// <summary>Thông báo lỗi tiếng Việt hiển thị cho người dùng.</summary>
public static class ApprovalErrorMessages
{
    public const string InvalidRequest = "Dữ liệu gửi lên không hợp lệ.";
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này.";
    public const string NotFound = "Không tìm thấy yêu cầu duyệt.";
    public const string AlreadyDecided = "Yêu cầu đã được xử lý, không thể xử lý lại.";
    public const string Conflict = "Dữ liệu đã thay đổi sau khi gửi yêu cầu, không thể áp dụng. Vui lòng gửi lại.";
    public const string RejectReasonRequired = "Vui lòng nhập lý do từ chối.";
    public const string RejectReasonTooLong = "Lý do từ chối không được vượt quá 500 ký tự.";
    public const string UnknownOperationCode = "Mã thao tác không tồn tại trong danh mục công tắc Quản lý.";
    public const string ApplierNotRegistered = "Mã thao tác chưa có bộ xử lý áp dụng, không thể duyệt.";
    public const string DuplicatePendingRequest = "Đã có yêu cầu chờ duyệt cho thao tác này.";
    public const string OperationFailed = "Không thể thực hiện, vui lòng thử lại.";
}

/// <summary>
/// Lỗi nghiệp vụ do applier ném ra khi không áp dụng được thao tác
/// (ví dụ đối tượng đã bị xóa). Service đổi thành kết quả thất bại và rollback.
/// </summary>
public sealed class ApprovalApplyException(string errorCode, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public string ErrorCode { get; } = errorCode;
}
