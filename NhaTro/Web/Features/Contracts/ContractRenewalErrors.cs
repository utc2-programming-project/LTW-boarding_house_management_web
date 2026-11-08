namespace Web.Features.Contracts;

/// <summary>Mã lỗi riêng của luồng yêu cầu gia hạn (VHT-125/2D), UI đối chiếu để lấy chuỗi đã địa phương hóa.</summary>
public static class ContractRenewalErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string ContractNotFound = nameof(ContractNotFound);
    public const string InvalidStatus = nameof(InvalidStatus);
    public const string InvalidMonths = nameof(InvalidMonths);
    public const string NoteTooLong = nameof(NoteTooLong);
    public const string DuplicatePending = nameof(DuplicatePending);

    // Luồng BQL duyệt yêu cầu (VHT-125/2E).
    public const string RequestNotFound = nameof(RequestNotFound);
    public const string AlreadyDecided = nameof(AlreadyDecided);
    public const string RejectReasonRequired = nameof(RejectReasonRequired);
    public const string ReasonTooLong = nameof(ReasonTooLong);
    public const string ApprovalDenied = nameof(ApprovalDenied);
    public const string RenewApplyFailed = nameof(RenewApplyFailed);

    // Ba mã P4 hay gặp khi chấp thuận, tách riêng để UI nói đúng nguyên nhân thay vì báo chung.
    public const string AlreadyRenewed = nameof(AlreadyRenewed);
    public const string DateOverlap = nameof(DateOverlap);
    public const string EndDateInvalid = nameof(EndDateInvalid);
}

public static class ContractRenewalErrorMessages
{
    public const string Forbidden = "Hồ sơ khách thuê của bạn không còn hiệu lực hoặc không thuộc hợp đồng này.";
    public const string ContractNotFound = "Không tìm thấy hợp đồng thuê.";
    public const string InvalidStatus = "Chỉ hợp đồng đang hiệu lực mới được gửi yêu cầu gia hạn.";
    public const string InvalidMonths = "Số tháng gia hạn phải lớn hơn 0 và không vượt quá giới hạn cho phép.";
    public const string NoteTooLong = "Lời nhắn gia hạn vượt quá độ dài cho phép.";
    public const string DuplicatePending = "Hợp đồng này đã có yêu cầu gia hạn đang chờ xử lý.";

    public const string RequestNotFound = "Không tìm thấy yêu cầu gia hạn.";
    public const string AlreadyDecided = "Yêu cầu gia hạn này đã được xử lý.";
    public const string RejectReasonRequired = "Vui lòng nhập lý do từ chối yêu cầu gia hạn.";
    public const string ReasonTooLong = "Lý do từ chối không được vượt quá 500 ký tự.";
    public const string ApprovalDenied = "Công tắc SW-07 đang chặn thao tác gia hạn hợp đồng.";
    public const string RenewApplyFailed = "Không gia hạn được hợp đồng. Vui lòng kiểm tra lại hợp đồng và thử lại.";
    public const string AlreadyRenewed = "Hợp đồng này đã được gia hạn rồi, không duyệt yêu cầu cũ nữa.";
    public const string DateOverlap = "Phòng đã có hợp đồng hiệu lực trong khoảng thời gian gia hạn.";
    public const string EndDateInvalid = "Ngày kết thúc mới phải sau ngày kết thúc hiện tại.";
}
