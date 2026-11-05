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
}

public static class ContractRenewalErrorMessages
{
    public const string Forbidden = "Hồ sơ khách thuê của bạn không còn hiệu lực hoặc không thuộc hợp đồng này.";
    public const string ContractNotFound = "Không tìm thấy hợp đồng thuê.";
    public const string InvalidStatus = "Chỉ hợp đồng đang hiệu lực mới được gửi yêu cầu gia hạn.";
    public const string InvalidMonths = "Số tháng gia hạn phải lớn hơn 0 và không vượt quá giới hạn cho phép.";
    public const string NoteTooLong = "Lời nhắn gia hạn vượt quá độ dài cho phép.";
    public const string DuplicatePending = "Hợp đồng này đã có yêu cầu gia hạn đang chờ xử lý.";
}
