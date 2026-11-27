using System.ComponentModel.DataAnnotations;
using Web.Common.Queries;
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
        Page = PagingExtensions.ClampPage(Page);
        PageSize = PagingExtensions.ClampPageSize(PageSize);
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
    }
}
