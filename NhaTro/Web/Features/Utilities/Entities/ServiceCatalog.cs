using Web.Common.SoftDelete;

namespace Web.Features.Utilities.Entities;

/// <summary>A service identity and its scope. Price details are stored as immutable versions.</summary>
public sealed class ServiceCatalog : ISoftDeletable
{
    public int Id { get; set; }

    /// <summary>Null means this catalog applies to every property.</summary>
    public int? PropertyId { get; set; }

    /// <summary>Used for system supplied electricity/water tariffs which users may only view.</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Indicates whether this service catalog is active for usage and new pricing.</summary>
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Xóa mềm: ẩn khỏi danh mục nhưng giữ lịch sử giá để đối soát hóa đơn cũ.</summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public ICollection<ServicePriceHistory> PriceHistory { get; set; } = new List<ServicePriceHistory>();
}
