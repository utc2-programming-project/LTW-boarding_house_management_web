namespace Web.Features.Contracts;

public static class ContractErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string ContractNotFound = nameof(ContractNotFound);
    public const string RoomNotFound = nameof(RoomNotFound);
    public const string PropertyNotFound = nameof(PropertyNotFound);
    public const string InvalidStatus = nameof(InvalidStatus);
    public const string RejectReasonRequired = nameof(RejectReasonRequired);
    public const string RejectReasonTooLong = nameof(RejectReasonTooLong);
    public const string RoomUnavailable = nameof(RoomUnavailable);
    public const string PropertyUnavailable = nameof(PropertyUnavailable);
    public const string RoomUnderMaintenance = nameof(RoomUnderMaintenance);
    public const string RoomNotVacant = nameof(RoomNotVacant);
    public const string ContractOverlap = nameof(ContractOverlap);
    public const string PendingContractExists = nameof(PendingContractExists);
    public const string InvalidDateRange = nameof(InvalidDateRange);
    public const string TenantRequired = nameof(TenantRequired);
    public const string TenantUnavailable = nameof(TenantUnavailable);
    public const string RoomNotAvailable = nameof(RoomNotAvailable);
    public const string FutureStartNotAllowed = nameof(FutureStartNotAllowed);
    public const string ContractNotActive = nameof(ContractNotActive);
    public const string InvalidRenewalEndDate = nameof(InvalidRenewalEndDate);
    public const string ContractAlreadyRenewed = nameof(ContractAlreadyRenewed);
    public const string ApprovalDenied = nameof(ApprovalDenied);
    public const string ApprovalFailed = nameof(ApprovalFailed);
    public const string DuplicatePendingRequest = nameof(DuplicatePendingRequest);
    public const string InvalidDepositAmount = nameof(InvalidDepositAmount);
}

public static class ContractErrorMessages
{
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác hợp đồng này.";
    public const string ContractNotFound = "Không tìm thấy hợp đồng.";
    public const string RoomNotFound = "Không tìm thấy phòng của hợp đồng.";
    public const string PropertyNotFound = "Không tìm thấy nhà trọ của phòng.";
    public const string OutsideOwnerScope = "Hợp đồng không thuộc nhà trọ của Chủ trọ đang thao tác.";
    public const string InvalidStatus = "Chỉ hợp đồng đang chờ duyệt mới được xử lý.";
    public const string RejectReasonRequired = "Vui lòng nhập lý do từ chối hợp đồng.";
    public const string RejectReasonTooLong = "Lý do từ chối không được vượt quá 500 ký tự.";
    public const string RoomUnavailable = "Phòng đã ngừng hoạt động, không thể tạo hoặc duyệt hợp đồng.";
    public const string RoomUnderMaintenance = "Không thể duyệt hợp đồng cho phòng đang bảo trì.";
    public const string PropertyUnavailable = "Nhà trọ đã bị xóa, không thể duyệt hợp đồng.";
    public const string ContractOverlap = "Phòng đã có hợp đồng hiệu lực trong thời gian này.";
    public const string RoomNotVacant = "Chỉ có thể duyệt hợp đồng khi phòng đang trống.";
    public const string PendingContractExists = "Phòng đã có hợp đồng đang chờ duyệt hoặc hiệu lực trong thời gian này.";
    public const string InvalidDateRange = "Ngày bắt đầu phải trước ngày kết thúc.";
    public const string TenantRequired = "Hợp đồng phải có ít nhất một khách thuê.";
    public const string TenantUnavailable = "Một hoặc nhiều khách thuê không tồn tại hoặc không còn hoạt động.";
    public const string RoomNotAvailable = "Phòng không tồn tại hoặc nằm ngoài phạm vi được phân công.";
    public const string FutureStartNotAllowed = "Không thể tạo hoặc duyệt hợp đồng có ngày bắt đầu trong tương lai.";
    public const string ContractNotActive = "Chỉ hợp đồng đang hiệu lực mới được gia hạn hoặc thanh lý.";
    public const string InvalidRenewalEndDate = "Ngày kết thúc mới phải sau ngày kết thúc hiện tại.";
    public const string ContractAlreadyRenewed = "Hợp đồng đã được gia hạn.";
    public const string ApprovalDenied = "Công tắc SW-07 đang chặn thao tác gia hạn / thanh lý hợp đồng.";
    public const string ApprovalFailed = "Không thể gửi yêu cầu duyệt. Vui lòng thử lại.";
    public const string DuplicatePendingRequest = "Đã có yêu cầu chờ duyệt cho hợp đồng này.";
}

public interface IContractServiceError
{
    string ErrorCode { get; }
}

public sealed class ContractAuthorizationException(string message, string errorCode)
    : UnauthorizedAccessException(message), IContractServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ContractValidationException(string message, string errorCode, string? paramName = null)
    : ArgumentException(message, paramName), IContractServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ContractNotFoundException(string message, string errorCode)
    : KeyNotFoundException(message), IContractServiceError
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ContractOperationException(string message, string errorCode, Exception? innerException = null)
    : InvalidOperationException(message, innerException), IContractServiceError
{
    public string ErrorCode { get; } = errorCode;
}
