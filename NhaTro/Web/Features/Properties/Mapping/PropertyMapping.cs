using Web.Features.Properties.DTOs;
using Web.Features.Properties.Entities;

namespace Web.Features.Properties.Mapping;

// Mapping thu cong entity <-> DTO (khong dung AutoMapper theo tech-stack).
public static class PropertyMapping
{
    public static PropertyListItem ToListItem(Property e, int roomCount = 0) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Address = e.Address,
        IsActive = e.IsActive,
        RoomCount = roomCount,
    };

    public static PropertyDetail ToDetail(Property e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Address = e.Address,
        OwnerId = e.OwnerId,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };

    public static void ApplySave(PropertySave dto, Property e)
    {
        dto.Normalize();
        e.Name = dto.Name;
        e.Address = dto.Address;
        e.IsActive = dto.IsActive;
    }
}
