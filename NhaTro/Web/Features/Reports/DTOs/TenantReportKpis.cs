namespace Web.Features.Reports.DTOs;

/// <summary>
/// Số liệu khách thuê cho P8. Tất cả tính trên cùng query/danh sách:
/// - <see cref="Total"/>: tổng hồ sơ trong phạm vi (không tính hồ sơ đã xóa mềm).
/// - <see cref="Active"/>: hồ sơ đang hoạt động.
/// - <see cref="Renting"/>: khách đang thuê = có hợp đồng đã duyệt, hôm nay nằm trong thời hạn.
///   Đếm theo hồ sơ khách (DISTINCT), không nhân theo số hợp đồng hoặc số tài khoản.
/// - <see cref="LinkedAccountCount"/>: hồ sơ đã gắn tài khoản; null với Quản lý (D9).
/// </summary>
public sealed class TenantReportKpis
{
    public int Total { get; init; }

    public int Active { get; init; }

    public int Renting { get; init; }

    public int? LinkedAccountCount { get; init; }
}
