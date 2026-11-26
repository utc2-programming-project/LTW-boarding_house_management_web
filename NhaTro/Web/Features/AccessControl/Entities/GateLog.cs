using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Entities;

/// <summary>
/// Nhật ký mọi lượt thử vào/ra, kể cả bị từ chối (có lý do).
/// Không lưu token, ảnh hay vector sinh trắc.
/// </summary>
public sealed class GateLog
{
    public long Id { get; set; }

    public int PropertyId { get; set; }

    public int? GateDeviceId { get; set; }

    public int? GatePassId { get; set; }

    public GateSubjectType SubjectType { get; set; } = GateSubjectType.Person;

    public GateDirection Direction { get; set; }

    public GateMethod Method { get; set; }

    public bool IsSuccess { get; set; }

    public string? Reason { get; set; }

    public int? MatchScore { get; set; }

    public string? SubjectName { get; set; }

    public string? UserId { get; set; }

    public int? TenantId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
