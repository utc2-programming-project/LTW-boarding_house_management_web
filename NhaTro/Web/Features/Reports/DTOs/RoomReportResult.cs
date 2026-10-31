using Web.Common.Models;
using Web.Features.Rooms.DTOs;

namespace Web.Features.Reports.DTOs;

/// <summary>Ket qua muc bao cao Phong: KPI + trang danh sach dung lai DTO list cua P2.</summary>
public sealed class RoomReportResult
{
    public RoomReportKpis Kpis { get; init; } = new();

    public PagedResult<RoomListItem> Rooms { get; init; } = new();
}
