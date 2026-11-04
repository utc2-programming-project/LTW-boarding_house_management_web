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
