using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Entities;

namespace Web.Features.Tenants.Mapping;

// Mapping thu cong entity <-> DTO (khong dung AutoMapper theo tech-stack).
// Ten cot DB Phone/CitizenId doi chieu sang DTO PhoneNumber/IdNumber.
// ApplyX gia dinh DTO da duoc Normalize() o service (mot noi chuan hoa duy nhat).
public static class TenantMapping
{
    public static TenantListItemDto ToListItem(
        Tenant e, bool hasActiveContract = false, bool includeAccountStatus = true, string? roomName = null) => new()
    {
        Id = e.Id,
        FullName = e.FullName,
        PhoneNumber = e.Phone,
        IdNumberMasked = TenantNormalizer.MaskIdNumber(e.CitizenId),
        IsActive = e.IsActive,
        HasAccount = includeAccountStatus ? e.UserId is not null : null,
        HasActiveContract = hasActiveContract,
        RoomName = roomName,
    };

    public static TenantPickerItemDto ToPickerItem(Tenant e, bool hasActiveContract = false) => new()
    {
        Id = e.Id,
        FullName = e.FullName,
        PhoneNumber = e.Phone,
        IdNumberMasked = TenantNormalizer.MaskIdNumber(e.CitizenId),
        HasActiveContract = hasActiveContract,
    };

    public static TenantDetailDto ToDetail(
        Tenant e, bool includeNote, bool revealIdNumber, bool includeAccountStatus = true) => new()
    {
        Id = e.Id,
        FullName = e.FullName,
        PhoneNumber = e.Phone,
        Email = e.Email,
        // D9: chỉ Chủ trọ (và chính khách) thấy số đầy đủ; Quản lý nhận chuỗi che.
        IdNumber = revealIdNumber ? e.CitizenId : TenantNormalizer.MaskIdNumber(e.CitizenId),
        DateOfBirth = e.DateOfBirth,
        Gender = e.Gender,
        PermanentAddress = e.PermanentAddress,
        IdIssuedDate = e.IdIssuedDate,
        IdIssuedPlace = e.IdIssuedPlace,
        Note = includeNote ? e.Note : null,
        IsActive = e.IsActive,
        HasAccount = includeAccountStatus ? e.UserId is not null : null,
        CreatedAt = ToOffset(e.CreatedAt),
        UpdatedAt = e.UpdatedAt is { } updatedAt ? ToOffset(updatedAt) : null,
        RowVersion = Convert.ToBase64String(e.RowVersion),
    };

    public static void ApplyCreate(CreateTenantRequest dto, Tenant e)
    {
        e.FullName = dto.FullName;
        e.Phone = dto.PhoneNumber;
        e.Email = dto.Email;
        e.CitizenId = dto.IdNumber;
        e.DateOfBirth = dto.DateOfBirth;
        e.Gender = dto.Gender;
        e.PermanentAddress = dto.PermanentAddress;
        e.IdIssuedDate = dto.IdIssuedDate;
        e.IdIssuedPlace = dto.IdIssuedPlace;
        e.Note = dto.Note;
        e.IsActive = true;
    }

    public static void ApplyUpdate(UpdateTenantRequest dto, Tenant e)
    {
        e.FullName = dto.FullName;
        e.Phone = dto.PhoneNumber;
        e.Email = dto.Email;
        e.CitizenId = dto.IdNumber;
        e.DateOfBirth = dto.DateOfBirth;
        e.Gender = dto.Gender;
        e.PermanentAddress = dto.PermanentAddress;
        e.IdIssuedDate = dto.IdIssuedDate;
        e.IdIssuedPlace = dto.IdIssuedPlace;
        e.Note = dto.Note;
    }

    public static void ApplyOwnUpdate(UpdateOwnProfileRequest dto, Tenant e)
    {
        e.Phone = dto.PhoneNumber;
        e.Email = dto.Email;
        e.PermanentAddress = dto.PermanentAddress;
    }

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
