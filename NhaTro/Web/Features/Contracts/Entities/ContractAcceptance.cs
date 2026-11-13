namespace Web.Features.Contracts.Entities;

/// <summary>
/// Bằng chứng khách thuê đã đọc và đồng ý một phiên bản hợp đồng cụ thể.
/// Không lưu mã OTP; chỉ lưu tham chiếu challenge và dấu vết kỹ thuật.
/// </summary>
public sealed class ContractAcceptance
{
    public int Id { get; set; }

    public Guid ContractId { get; set; }

    public int ContractVersionId { get; set; }

    public int TenantId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset AcceptedAt { get; set; }

    /// <summary>Hash của nội dung tại thời điểm khách xác nhận (phải khớp ContractVersion).</summary>
    public string ContentHash { get; set; } = string.Empty;

    public int? OtpChallengeId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Phương thức xác nhận, hiện tại chỉ hỗ trợ "Otp".</summary>
    public string SignatureMethod { get; set; } = "Otp";
}
