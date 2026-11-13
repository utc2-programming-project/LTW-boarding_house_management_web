using Web.Features.Properties;
using Web.Features.Reports.DTOs;

namespace Web.Features.Reports;

/// <summary>Nguon phong cho bao cao P8 (VHT-41). Chi doc, khong ghi du lieu.</summary>
public interface IRoomReportService
{
    Task<RoomReportResult> GetAsync(
        RoomReportFilter filter, PropertyCaller caller, CancellationToken ct = default);

    /// <summary>VHT-141 — lấp đầy theo tháng, cùng phạm vi quyền với báo cáo phòng.</summary>
    Task<OccupancyReportResult> GetOccupancyAsync(
        OccupancyFilter filter, PropertyCaller caller, CancellationToken ct = default);
}
