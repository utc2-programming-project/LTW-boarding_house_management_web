using System.ComponentModel.DataAnnotations;
using Web.Features.Rooms.Enums;

namespace Web.Features.Reports.DTOs;

// Bo loc nguon phong P8 (VHT-41). IsActive mac dinh true de 4 so KPI luon cong khop voi
// danh sach cung bo loc; phong tam ngung chi vao bao cao khi loc IsActive = false.
public sealed class RoomReportFilter
{
    public int? PropertyId { get; set; }

    public RoomStatus? Status { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string? Keyword { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 20;
        else if (PageSize > 100) PageSize = 100;
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
    }
}
