namespace Web.Features.Reports.DTOs;

/// <summary>
/// VHT-141 — bộ lọc báo cáo lấp đầy lịch sử. Không chọn ngày thì nguồn trả 6 tháng
/// gần nhất tính đến tháng hiện tại.
/// </summary>
public sealed record OccupancyFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    int? PropertyId = null);

/// <summary>
/// Một tháng lấp đầy. Đang thuê = số phòng khác nhau có hợp đồng Approved/Liquidated
/// giao với tháng; tổng = phòng active đã tạo trước cuối tháng.
/// <see cref="OccupancyRate"/> là phần trăm (null khi tổng phòng bằng 0).
/// </summary>
public sealed record OccupancyMonthDto(
    string MonthKey,
    DateOnly Start,
    DateOnly End,
    int TotalRooms,
    int RentedRooms,
    int VacantRooms,
    decimal? OccupancyRate);

public sealed record OccupancyReportResult(IReadOnlyList<OccupancyMonthDto> Months);
