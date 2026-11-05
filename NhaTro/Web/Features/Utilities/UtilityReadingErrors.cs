namespace Web.Features.Utilities;

/// <summary>Mã lỗi nghiệp vụ của luồng ghi chỉ số/tính chi phí kỳ thật (VHT-51).</summary>
public enum UtilityReadingErrorCode
{
    ContractNotFound,
    ContractNotApproved,
    InvalidPeriod,
    PeriodOutsideContract,
    DuplicatePeriod,
    OverlappingPeriod,
    PeriodNotFound,
    PeriodLocked,
    PeriodHasLaterPeriod,
    PeriodAlreadyHasReadings,
    PreviousPeriodHasNoReadings,
    PeriodHasNoReadings,
    ReadingCannotBeNegative,
    ReadingLessThanPrevious,
    Forbidden,
    ConcurrencyConflict,
}

/// <summary>Thông điệp dùng chung để service và test không lệch nhau.</summary>
public static class UtilityReadingErrorMessages
{
    public const string ContractNotFound = "Không tìm thấy hợp đồng trong phạm vi được phép.";
    public const string ContractNotApproved = "Chỉ hợp đồng đã duyệt mới được ghi chỉ số.";
    public const string InvalidPeriod = "Kỳ tính tiền không hợp lệ: ngày kết thúc phải sau hoặc bằng ngày bắt đầu.";
    public const string PeriodOutsideContract = "Kỳ tính tiền phải nằm trong thời hạn hợp đồng.";
    public const string DuplicatePeriod = "Hợp đồng đã có kỳ khác trùng ngày bắt đầu.";
    public const string OverlappingPeriod = "Kỳ mới chồng lấn với một kỳ đã có của hợp đồng.";
    public const string PeriodNotFound = "Không tìm thấy kỳ ghi chỉ số trong phạm vi được phép.";
    public const string PeriodLocked = "Kỳ đã phát hành hóa đơn nên không được sửa chỉ số (D10).";
    public const string PeriodHasLaterPeriod = "Kỳ này đã có kỳ sau nên không thể mở kỳ chen trước.";
    public const string PeriodAlreadyHasReadings = "Kỳ đã ghi chỉ số; việc điều chỉnh kỳ liền trước thuộc luồng VHT-102.";
    public const string PreviousPeriodHasNoReadings = "Kỳ liền trước chưa ghi chỉ số nên chưa lấy được chỉ số đầu kỳ.";
    public const string PeriodHasNoReadings = "Kỳ chưa ghi chỉ số nên chưa thể chuyển chi phí cho hóa đơn.";
    public const string ReadingCannotBeNegative = "Chỉ số không được là số âm.";
    public const string ReadingLessThanPrevious = "Chỉ số mới phải lớn hơn hoặc bằng chỉ số đầu kỳ.";
    public const string Forbidden = "Bạn không có quyền ghi chỉ số/tính chi phí kỳ.";
    public const string ConcurrencyConflict = "Dữ liệu kỳ vừa bị người khác thay đổi, vui lòng tải lại.";
}

/// <summary>Lỗi nghiệp vụ có mã ổn định để UI dịch sang VI/EN.</summary>
public class UtilityReadingException(
    string message,
    string code,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public string Code { get; } = code;
}

/// <summary>Không tìm thấy dữ liệu hoặc dữ liệu ngoài phạm vi (không phân biệt để không lộ ID).</summary>
public sealed class UtilityReadingNotFoundException(string message, string code, Exception? innerException = null)
    : KeyNotFoundException(message, innerException)
{
    public string Code { get; } = code;
}

/// <summary>Sai quyền ghi/đọc nghiệp vụ chỉ số.</summary>
public sealed class UtilityReadingAuthorizationException(string message, string code, Exception? innerException = null)
    : UnauthorizedAccessException(message, innerException)
{
    public string Code { get; } = code;
}
