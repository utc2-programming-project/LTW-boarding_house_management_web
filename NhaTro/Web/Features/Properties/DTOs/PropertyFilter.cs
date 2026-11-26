using System.ComponentModel.DataAnnotations;
using Web.Common.Queries;

namespace Web.Features.Properties.DTOs;

// Dieu kien loc nha tro cho list (Phuc: VHT-16).
// ChuTro: service tu gioi han theo OwnerId (PropertyScope), khong nhan OwnerId tu client.
// Page/PageSize/Keyword/SortKey lấy từ ListQueryBase dùng chung.
public sealed class PropertyFilter : ListQueryBase
{
    public bool? IsActive { get; set; }

    // Loc theo khoang so phong (tinh ca phong chua xoa mem).
    [Range(0, int.MaxValue)]
    public int? MinRoomCount { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxRoomCount { get; set; }

    // Alias cũ của SortKey: name (mac dinh), newest, oldest, roomCountAsc, roomCountDesc.
    [MaxLength(20)]
    public string? SortBy
    {
        get => SortKey;
        set => SortKey = value;
    }

    public override void Normalize()
    {
        base.Normalize();
        if (MinRoomCount is < 0) MinRoomCount = null;
        if (MaxRoomCount is < 0) MaxRoomCount = null;
        if (MinRoomCount is { } min && MaxRoomCount is { } max && min > max)
        {
            (MinRoomCount, MaxRoomCount) = (max, min);
        }
    }
}
