using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Entities;

/// <summary>
/// Thiết bị tại cổng (kiosk, đầu đọc, máy quét, relay).
/// </summary>
public sealed class GateDevice
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public GateDeviceKind Kind { get; set; }

    public GateLevel Level { get; set; }

    public bool IsOnline { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
