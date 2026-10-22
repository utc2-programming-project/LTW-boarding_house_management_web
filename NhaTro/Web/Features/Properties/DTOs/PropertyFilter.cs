using System.ComponentModel.DataAnnotations;

namespace Web.Features.Properties.DTOs;

// Dieu kien loc nha tro cho list (Phuc: VHT-16).
// ChuTro: service tu gioi han theo OwnerId (PropertyScope), khong nhan OwnerId tu client.
public sealed class PropertyFilter
{
    [MaxLength(200)]
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1 || PageSize > 100) PageSize = 20;
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
    }
}
