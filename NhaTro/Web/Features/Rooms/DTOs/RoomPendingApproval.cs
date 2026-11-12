namespace Web.Features.Rooms.DTOs;

/// <summary>
/// Yêu cầu Chờ duyệt (SW-01/02/03) đang gắn với một phòng. Giữ đủ dữ liệu để badge
/// trên danh sách phòng giải thích được "ai đề xuất, đề xuất gì, lúc nào" thay vì
/// chỉ hiện một nhãn "Chờ duyệt" vô nghĩa.
/// </summary>
public sealed record RoomPendingApproval(
    Guid Id,
    string OperationCode,
    DateTime RequestedAt,
    string? RequesterName);
