using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Entities;
using Web.Features.Properties.Mapping;

namespace Web.Features.Properties;

/// <summary>
/// Service SQL cho nhà trọ P2: kiểm quyền/phạm vi và chịu trách nhiệm lưu dữ liệu.
/// Nguồn: docs/task/de_xuat_hop_dong_nha_tro_phong.md; quyền: docs/task/thao_tac_va_quyen.xlsx (P2).
/// </summary>
public sealed class PropertyService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock) : IPropertyService
{
    public async Task<PagedResult<PropertyListItem>> ListAsync(
        PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        filter.Normalize();

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = ScopeQuery(db, caller).AsNoTracking();
        if (filter.IsActive is { } isActive)
        {
            query = query.Where(p => p.IsActive == isActive);
        }

        if (filter.Keyword is { Length: > 0 } keyword)
        {
            query = query.Where(p => p.Name.Contains(keyword) || (p.Address != null && p.Address.Contains(keyword)));
        }

        var total = await query.CountAsync(ct);
        var skip = (long)(filter.Page - 1) * filter.PageSize;
        var rows = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .Select(p => new
            {
                Property = p,
                RoomCount = db.Rooms.Count(r => r.PropertyId == p.Id),
            })
            .ToListAsync(ct);

        return new PagedResult<PropertyListItem>
        {
            Items = rows.Select(r => PropertyMapping.ToListItem(r.Property, r.RoomCount)).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = total,
        };
    }

    public async Task<PropertyResult> GetAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await ScopeQuery(db, caller).AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return entity is null
            ? NotFound()
            : PropertyResult.Success(PropertyMapping.ToDetail(entity));
    }

    public async Task<PropertyResult> CreateAsync(
        PropertySave request, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanWrite(caller);
        request.Normalize();

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = new Property
        {
            OwnerId = caller.UserId,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        PropertyMapping.ApplySave(request, entity);

        db.Properties.Add(entity);
        await db.SaveChangesAsync(ct);

        return PropertyResult.Success(PropertyMapping.ToDetail(entity));
    }

    public async Task<PropertyResult> UpdateAsync(
        int id, PropertySave request, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanWrite(caller);
        request.Normalize();

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await ScopeQuery(db, caller).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        PropertyMapping.ApplySave(request, entity);
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);

        return PropertyResult.Success(PropertyMapping.ToDetail(entity));
    }

    // Phạm vi (VHT-6/VHT-24): Chủ trọ chỉ thấy nhà có OwnerId của mình;
    // Quản lý chỉ thấy nhà được phân công đang hiệu lực.
    private static IQueryable<Property> ScopeQuery(ApplicationDbContext db, PropertyCaller caller) =>
        PropertyScope.Apply(db.Properties, db.PropertyManagers, caller);

    // Quyền P2: Chủ trọ/Quản lý xem; Admin và Khách thuê không vào nghiệp vụ quản lý.
    // Khách thuê chỉ xem phòng đang thuê qua hợp đồng (VHT-36), không qua service này.
    private static void EnsureCanRead(PropertyCaller caller)
    {
        if (!caller.IsManager)
        {
            throw Forbidden(caller);
        }
    }

    // Tạo/sửa nhà trọ: chỉ Chủ trọ (ma trận không có thao tác này cho Quản lý).
    private static void EnsureCanWrite(PropertyCaller caller)
    {
        if (!caller.IsChuTro)
        {
            throw Forbidden(caller);
        }
    }

    private static PropertyResult? Validate(PropertySave request)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            return PropertyResult.Failure(PropertyErrorCodes.InvalidRequest, PropertyErrorMessages.InvalidName);
        }

        if (request.Name.Length > 200 || (request.Address?.Length ?? 0) > 500)
        {
            return PropertyResult.Failure(PropertyErrorCodes.InvalidRequest, PropertyErrorMessages.InvalidLength);
        }

        return null;
    }

    private static UnauthorizedAccessException Forbidden(PropertyCaller caller) =>
        new(caller.IsAdmin
            ? "Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu nha tro."
            : PropertyErrorMessages.Forbidden);

    private static PropertyResult NotFound() =>
        PropertyResult.Failure(PropertyErrorCodes.PropertyNotFound, PropertyErrorMessages.PropertyNotFound);
}
