namespace Web.Features.Invoices;

public static class InvoiceErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string ContractNotFound = nameof(ContractNotFound);
    public const string ContractNotApproved = nameof(ContractNotApproved);
    public const string InvalidPeriod = nameof(InvalidPeriod);
    public const string ItemsRequired = nameof(ItemsRequired);
    public const string InvalidItem = nameof(InvalidItem);
    public const string DuplicateInvoice = nameof(DuplicateInvoice);
    public const string InvoiceNotFound = nameof(InvoiceNotFound);
    public const string InvalidStatus = nameof(InvalidStatus);
    public const string TotalMismatch = nameof(TotalMismatch);
    public const string UtilityPeriodMismatch = nameof(UtilityPeriodMismatch);
    public const string TariffNotConfigured = nameof(TariffNotConfigured);
    public const string ApprovalRequired = nameof(ApprovalRequired);
    public const string ApprovalDenied = nameof(ApprovalDenied);
    public const string ApprovalFailed = nameof(ApprovalFailed);
    public const string NotDeletable = nameof(NotDeletable);
    public const string NotInTrash = nameof(NotInTrash);
    public const string HasDependencies = nameof(HasDependencies);
    public const string ContractDeleted = nameof(ContractDeleted);
    public const string DuplicatePeriod = nameof(DuplicatePeriod);
}

public static class InvoiceErrorMessages
{
    public const string Forbidden = "Bạn không có quyền lập hoặc phát hành hóa đơn cho hợp đồng này.";
    public const string ContractNotFound = "Không tìm thấy hợp đồng hoặc hợp đồng nằm ngoài phạm vi được giao.";
    public const string ContractNotApproved = "Chỉ được lập hóa đơn từ hợp đồng đã duyệt.";
    public const string InvalidPeriod = "Ngày cuối kỳ phải lớn hơn hoặc bằng ngày đầu kỳ.";
    public const string PeriodOutsideContract = "Kỳ hóa đơn phải nằm trong thời hạn hợp đồng.";
    public const string ItemsRequired = "Hóa đơn phải có ít nhất một dòng tiền thuê hoặc dịch vụ.";
    public const string InvalidItem = "Dòng hóa đơn không hợp lệ.";
    public const string DuplicateInvoice = "Hợp đồng đã có hóa đơn cho kỳ này với dữ liệu khác.";
    public const string InvoiceNotFound = "Không tìm thấy hóa đơn hoặc bạn không có quyền thao tác.";
    public const string InvalidStatus = "Chỉ hóa đơn nháp mới được phát hành.";
    public const string TotalMismatch = "Tổng hóa đơn không khớp với các dòng đã lưu.";
    public const string UtilityPeriodMismatch = "Kỳ chỉ số điện/nước đã chốt không khớp kỳ hóa đơn.";
    public const string TariffNotConfigured = "Chưa có biểu giá điện/nước hiệu lực cho kỳ này nên không thể lập hóa đơn thật.";
    public const string ApprovalRequired = "Thao tác đang chờ Chủ trọ duyệt; hóa đơn chưa được ghi.";
    public const string ApprovalDenied = "Công tắc Quản lý đang chặn thao tác với hóa đơn.";
    public const string ApprovalFailed = "Không thể gửi yêu cầu duyệt. Vui lòng thử lại.";
    public const string ApprovalComposite =
        "Lập và phát hành cùng lúc không đi qua hàng đợi duyệt; hãy gửi từng bước để Chủ trọ duyệt.";
    public const string NotDeletable = "Chỉ hóa đơn nháp mới được xóa.";
    public const string NotInTrash = "Hóa đơn không còn trong thùng rác.";
    public const string HasDependencies = "Không thể xóa vì hóa đơn còn thanh toán, kỳ chỉ số hoặc yêu cầu đăng ký.";
    public const string ContractDeleted = "Hợp đồng của hóa đơn đã bị xóa; hãy khôi phục hợp đồng trước.";
    public const string DuplicatePeriod = "Hợp đồng đã có hóa đơn sống trong cùng kỳ.";
}

public interface IInvoiceServiceError
{
    string ErrorCode { get; }
}

public sealed class InvoiceAuthorizationException(string message, string errorCode)
    : UnauthorizedAccessException(message), IInvoiceServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class InvoiceValidationException(string message, string errorCode, string? paramName = null)
    : ArgumentException(message, paramName), IInvoiceServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class InvoiceNotFoundException(string message, string errorCode)
    : KeyNotFoundException(message), IInvoiceServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class InvoiceOperationException(string message, string errorCode, Exception? innerException = null)
    : InvalidOperationException(message, innerException), IInvoiceServiceError
{
    public string ErrorCode { get; } = errorCode;
}
