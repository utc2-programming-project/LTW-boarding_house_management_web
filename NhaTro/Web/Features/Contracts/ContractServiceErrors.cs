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
    public const string ContractOverlap = nameof(ContractOverlap);
}

public static class ContractErrorMessages
{
    public const string Forbidden = "Chỉ Chủ trọ được duyệt hoặc từ chối hợp đồng.";
    public const string ContractNotFound = "Không tìm thấy hợp đồng.";
    public const string RoomNotFound = "Không tìm thấy phòng của hợp đồng.";
    public const string PropertyNotFound = "Không tìm thấy nhà trọ của phòng.";
    public const string OutsideOwnerScope = "Hợp đồng không thuộc nhà trọ của Chủ trọ đang thao tác.";
    public const string InvalidStatus = "Chỉ hợp đồng đang chờ duyệt mới được xử lý.";
    public const string RejectReasonRequired = "Vui lòng nhập lý do từ chối hợp đồng.";
    public const string RejectReasonTooLong = "Lý do từ chối không được vượt quá 500 ký tự.";
    public const string RoomUnavailable = "Không thể duyệt hợp đồng cho phòng đã ngừng hoạt động.";
    public const string RoomUnderMaintenance = "Không thể duyệt hợp đồng cho phòng đang bảo trì.";
    public const string PropertyUnavailable = "Nhà trọ đã bị xóa, không thể duyệt hợp đồng.";
    public const string ContractOverlap = "Phòng đã có hợp đồng hiệu lực trong thời gian này.";
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
