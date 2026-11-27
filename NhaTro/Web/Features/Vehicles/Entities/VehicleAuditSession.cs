using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.Entities;

/// <summary>
/// Đợt kiểm kê bãi xe định kỳ của một nhà trọ để chống thất thoát phí gửi xe.
/// </summary>
public sealed class VehicleAuditSession
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string AuditedByUserId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public VehicleAuditSessionStatus Status { get; set; } = VehicleAuditSessionStatus.InProgress;
    public int TotalExpected { get; set; }
    public int TotalPresent { get; set; }
    public int TotalMissing { get; set; }
    public string? Note { get; set; }
    public ICollection<VehicleAuditItem> Items { get; set; } = new List<VehicleAuditItem>();
}
