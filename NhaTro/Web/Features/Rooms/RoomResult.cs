using Web.Features.Rooms.DTOs;

namespace Web.Features.Rooms;

/// <summary>Kết quả thao tác phòng, cùng khuôn Succeeded/ErrorCode/ErrorMessage như P3.</summary>
public sealed class RoomResult
{
    public bool Succeeded { get; init; }
    public RoomDetail? Room { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static RoomResult Success(RoomDetail room) => new() { Succeeded = true, Room = room };

    public static RoomResult Success() => new() { Succeeded = true };

    public static RoomResult Failure(string code, string message) =>
        new() { Succeeded = false, ErrorCode = code, ErrorMessage = message };
}
