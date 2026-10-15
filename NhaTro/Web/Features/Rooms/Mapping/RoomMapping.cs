using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Entities;

namespace Web.Features.Rooms.Mapping;

// Mapping thu cong entity <-> DTO (khong dung AutoMapper theo tech-stack).
public static class RoomMapping
{
    public static RoomListItem ToListItem(Room e, string propertyName) => new()
    {
        Id = e.Id,
        PropertyId = e.PropertyId,
        PropertyName = propertyName,
        Code = e.Code,
        Floor = e.Floor,
        AreaM2 = e.AreaM2,
        StandardPrice = e.StandardPrice,
        Status = e.Status,
        IsActive = e.IsActive,
    };

    public static RoomDetail ToDetail(Room e, string propertyName) => new()
    {
        Id = e.Id,
        PropertyId = e.PropertyId,
        PropertyName = propertyName,
        Code = e.Code,
        Floor = e.Floor,
        AreaM2 = e.AreaM2,
        StandardPrice = e.StandardPrice,
        Status = e.Status,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };

    public static RoomOption ToOption(Room e, string propertyName) => new()
    {
        Id = e.Id,
        PropertyId = e.PropertyId,
        PropertyName = propertyName,
        Code = e.Code,
        StandardPrice = e.StandardPrice,
        Status = e.Status,
    };

    public static void ApplySave(RoomSave dto, Room e)
    {
        dto.Normalize();
        e.PropertyId = dto.PropertyId;
        e.Code = dto.Code;
        e.Floor = dto.Floor;
        e.AreaM2 = dto.AreaM2;
        e.StandardPrice = dto.StandardPrice;
        e.Status = dto.Status;
        e.IsActive = dto.IsActive;
    }
}
