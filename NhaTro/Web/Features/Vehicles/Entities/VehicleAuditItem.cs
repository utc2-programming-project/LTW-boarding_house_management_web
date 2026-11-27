using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.Entities;

/// <summary>
/// Chi tiết kiểm kê từng xe trong một phiên kiểm kê bãi xe (VehicleAuditSession).
/// </summary>
public sealed class VehicleAuditItem
{
    public int Id { get; set; }
    public int AuditSessionId { get; set; }

    /// <summary>Xe gửi tháng (Active). Null khi dòng kiểm kê là phiếu xe bạn gửi nhờ.</summary>
    public int? VehicleId { get; set; }

    /// <summary>Phiếu xe bạn gửi nhờ (Active) tương ứng. Null với dòng xe gửi tháng.</summary>
    public int? GuestNoticeId { get; set; }
    public string PlateNumberSnapshot { get; set; } = string.Empty;
    public int RoomIdSnapshot { get; set; }
    public string RoomCodeSnapshot { get; set; } = string.Empty;
    public VehicleType VehicleTypeSnapshot { get; set; } = VehicleType.ManualMotorbike;
    public VehicleAuditItemResult Result { get; set; } = VehicleAuditItemResult.Pending;
    public DateTimeOffset? CheckedAt { get; set; }
    public string? Note { get; set; }
}
