namespace Web.Features.Rooms.DTOs;

/// <summary>Scoped room totals used by the P2 overview cards.</summary>
public sealed class RoomSummary
{
    public int Total { get; init; }
    public int Available { get; init; }
    public int Rented { get; init; }
    public int Maintenance { get; init; }
}
