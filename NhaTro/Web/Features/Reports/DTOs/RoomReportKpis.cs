namespace Web.Features.Reports.DTOs;

/// <summary>So phong theo trang thai cho muc bao cao Phong (P8). Tinh tren cung query voi danh sach.</summary>
public sealed class RoomReportKpis
{
    public int Total { get; init; }
    public int Available { get; init; }
    public int Rented { get; init; }
    public int Maintenance { get; init; }
}
