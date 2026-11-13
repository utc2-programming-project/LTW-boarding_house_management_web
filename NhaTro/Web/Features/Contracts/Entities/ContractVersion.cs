namespace Web.Features.Contracts.Entities;

/// <summary>
/// Snapshot bất biến của nội dung hợp đồng tại mỗi lần gửi khách.
/// Nội dung được serialize JSON và băm SHA-256 để làm bằng chứng.
/// </summary>
public sealed class ContractVersion
{
    public int Id { get; set; }

    public Guid ContractId { get; set; }

    public int VersionNo { get; set; }

    public string SnapshotJson { get; set; } = string.Empty;

    /// <summary>SHA-256 (Base64) của <see cref="SnapshotJson"/>.</summary>
    public string ContentHash { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Thời điểm phiên bản được gửi cho khách; null nếu chỉ là bản nháp.</summary>
    public DateTimeOffset? SentAt { get; set; }
}
