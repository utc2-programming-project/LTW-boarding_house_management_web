using System.ComponentModel.DataAnnotations;

namespace Web.Features.Rooms.DTOs;

/// <summary>
/// Yêu cầu lấy dữ liệu chọn phòng (dropdown hợp đồng — Đào dùng ở VHT-11/VHT-29).
/// Chỉ trả phòng đang hoạt động trong phạm vi người gọi; mặc định chỉ phòng trống.
/// </summary>
public sealed class RoomOptionRequest
{
    [MaxLength(50)]
    public string? Keyword { get; set; }

    public int? PropertyId { get; set; }

    /// <summary>Lấy cả phòng đang thuê/bảo trì (mặc định false = chỉ phòng trống).</summary>
    public bool IncludeUnavailable { get; set; }

    /// <summary>Loại trừ phòng đã chọn (tránh trùng trong một hợp đồng).</summary>
    public List<int>? ExcludeRoomIds { get; set; }

    public int Limit { get; set; } = 20;

    public void Normalize()
    {
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
        if (Limit < 1 || Limit > 50) Limit = 20;
        if (ExcludeRoomIds is { Count: > 100 }) ExcludeRoomIds = ExcludeRoomIds.Take(100).ToList();
    }
}
