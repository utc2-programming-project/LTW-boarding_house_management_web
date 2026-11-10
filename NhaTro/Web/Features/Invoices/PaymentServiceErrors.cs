namespace Web.Features.Invoices;

public static class PaymentErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string InvoiceNotFound = nameof(InvoiceNotFound);
    public const string InvoiceNotIssued = nameof(InvoiceNotIssued);
    public const string InvoiceAlreadyPaid = nameof(InvoiceAlreadyPaid);
    public const string AmountMismatch = nameof(AmountMismatch);
    public const string InvalidTransactionCode = nameof(InvalidTransactionCode);
    public const string TransferredAtInFuture = nameof(TransferredAtInFuture);
    public const string ProofRequired = nameof(ProofRequired);
    public const string InvalidProof = nameof(InvalidProof);
    public const string PendingPaymentExists = nameof(PendingPaymentExists);
    public const string PaymentNotFound = nameof(PaymentNotFound);
    public const string InvalidPaymentStatus = nameof(InvalidPaymentStatus);
    public const string RejectionReasonRequired = nameof(RejectionReasonRequired);
    public const string RejectionReasonTooLong = nameof(RejectionReasonTooLong);
    public const string ConcurrencyConflict = nameof(ConcurrencyConflict);
}

public static class PaymentErrorMessages
{
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác thanh toán này.";
    public const string InvoiceNotFound = "Không tìm thấy hóa đơn hoặc hóa đơn nằm ngoài phạm vi của bạn.";
    public const string InvoiceNotIssued = "Chỉ hóa đơn đã phát hành mới được gửi yêu cầu thanh toán.";
    public const string InvoiceAlreadyPaid = "Hóa đơn đã được xác nhận thanh toán.";
    public const string AmountMismatch = "Số tiền phải bằng đúng tổng hóa đơn; hệ thống không hỗ trợ trả một phần.";
    public const string InvalidTransactionCode = "Mã giao dịch tối đa 100 ký tự.";
    public const string TransferredAtInFuture = "Thời điểm chuyển tiền không được ở tương lai.";
    public const string ProofRequired = "Vui lòng gửi ít nhất một minh chứng thanh toán.";
    public const string InvalidProof = "Minh chứng thanh toán không hợp lệ hoặc không thuộc hóa đơn của bạn.";
    public const string PendingPaymentExists = "Hóa đơn đã có yêu cầu thanh toán đang chờ xác nhận.";
    public const string PaymentNotFound = "Không tìm thấy yêu cầu thanh toán hoặc bạn không có quyền thao tác.";
    public const string InvalidPaymentStatus = "Yêu cầu thanh toán không còn ở trạng thái có thể xử lý.";
    public const string RejectionReasonRequired = "Vui lòng nhập lý do từ chối minh chứng.";
    public const string RejectionReasonTooLong = "Lý do từ chối tối đa 500 ký tự.";
    public const string ConcurrencyConflict = "Dữ liệu thanh toán vừa thay đổi, vui lòng tra cứu lại kết quả.";
}

/// <summary>Lỗi nghiệp vụ P6 có mã ổn định để endpoint/UI ánh xạ thông báo.</summary>
public interface IPaymentServiceError
{
    string ErrorCode { get; }
}

public sealed class PaymentAuthorizationException(string message, string errorCode)
    : UnauthorizedAccessException(message), IPaymentServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class PaymentValidationException(string message, string errorCode, string? paramName = null)
    : ArgumentException(message, paramName), IPaymentServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class PaymentNotFoundException(string message, string errorCode)
    : KeyNotFoundException(message), IPaymentServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class PaymentOperationException(string message, string errorCode, Exception? innerException = null)
    : InvalidOperationException(message, innerException), IPaymentServiceError
{
    public string ErrorCode { get; } = errorCode;
}
