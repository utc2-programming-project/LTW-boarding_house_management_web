using System.ComponentModel.DataAnnotations;
using Web.Features.Rooms.Enums;

namespace Web.Features.Rooms.DTOs;

// Dieu kien loc phong cho list + du lieu chon phong cho hop dong (Dao: VHT-11/VHT-29).
// ChuTro: service tu gioi han qua Property.OwnerId (RoomScope), khong nhan OwnerId tu client.
public sealed class RoomFilter
{
    public int? PropertyId { get; set; }

    public RoomStatus? Status { get; set; }

    [MaxLength(50)]
    public string? Keyword { get; set; }

    // Loc nhanh cho form hop dong: chi lay phong trong.
    public bool? OnlyAvailable { get; set; }

    public bool? IsActive { get; set; }

    [Range(0, (double)decimal.MaxValue)]
    public decimal? MinPrice { get; set; }

    [Range(0, (double)decimal.MaxValue)]
    public decimal? MaxPrice { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1 || PageSize > 100) PageSize = 20;
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
        if (MinPrice is < 0) MinPrice = null;
        if (MaxPrice is < 0) MaxPrice = null;
    }
}
