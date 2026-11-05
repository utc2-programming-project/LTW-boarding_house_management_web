namespace Web.Features.Utilities.Entities;

/// <summary>
/// P5 — chỉ số điện/nước bền vững của một phòng trong một kỳ. Kỳ sau lấy chỉ số đầu
/// từ chỉ số cuối của kỳ trước cùng phòng.
/// </summary>
public sealed class UtilityReading
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public decimal ElectricStart { get; set; }

    public decimal ElectricEnd { get; set; }

    public decimal WaterStart { get; set; }

    public decimal WaterEnd { get; set; }

    public string RecordedByUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<UtilityReadingAudit> Audits { get; set; } = new List<UtilityReadingAudit>();
}

/// <summary>D10 — lịch sử sửa chỉ số: ai, khi nào, cũ → mới, lý do.</summary>
public sealed class UtilityReadingAudit
{
    public int Id { get; set; }

    public int UtilityReadingId { get; set; }

    public decimal OldElectricEnd { get; set; }

    public decimal NewElectricEnd { get; set; }

    public decimal OldWaterEnd { get; set; }

    public decimal NewWaterEnd { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string ChangedByUserId { get; set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; set; }
}
