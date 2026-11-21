using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Common.Queries;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Entities;
using Web.Features.Properties.Mapping;
using Web.Features.Properties.Services;

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

        // Loc theo khoang so phong (dem phong chua xoa mem) truoc khi phan trang.
        if (filter.MinRoomCount is { } minRoomCount)
        {
            query = query.Where(p => db.Rooms.Count(r => r.PropertyId == p.Id) >= minRoomCount);
        }

        if (filter.MaxRoomCount is { } maxRoomCount)
        {
            query = query.Where(p => db.Rooms.Count(r => r.PropertyId == p.Id) <= maxRoomCount);
        }

        var sorts = new SortMap<Property>(
                (query, _) => query.OrderBy(p => p.Name),
                ordered => ordered.ThenBy(p => p.Id))
            .Add("newest", (query, _) => query.OrderByDescending(p => p.CreatedAt))
            .Add("oldest", (query, _) => query.OrderBy(p => p.CreatedAt))
            .Add("roomCountAsc", (query, _) =>
                query.OrderBy(p => db.Rooms.Count(r => r.PropertyId == p.Id)).ThenBy(p => p.Name))
            .Add("roomCountDesc", (query, _) =>
                query.OrderByDescending(p => db.Rooms.Count(r => r.PropertyId == p.Id)).ThenBy(p => p.Name));

        var ordered = sorts.Apply(query, filter.SortKey, filter.SortDescending);
        var page = await ordered
            .Select(p => new
            {
                Property = p,
                RoomCount = db.Rooms.Count(r => r.PropertyId == p.Id),
            })
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);

        return new PagedResult<PropertyListItem>
        {
            Items = page.Items.Select(r => PropertyMapping.ToListItem(r.Property, r.RoomCount)).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };
    }

    public async Task<PropertyListSummary> GetSummaryAsync(
        PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = ScopeQuery(db, caller).AsNoTracking();

        var counts = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Active = group.Count(property => property.IsActive),
            })
            .SingleOrDefaultAsync(ct);

        var rooms = await (
                from room in db.Rooms.AsNoTracking()
                join property in query on room.PropertyId equals property.Id
                select room.Id)
            .CountAsync(ct);

        var total = counts?.Total ?? 0;
        var active = counts?.Active ?? 0;
        return new PropertyListSummary(total, active, total - active, rooms);
    }

    public Task<IReadOnlyList<PropertyListItem>> ListOptionsAsync(
        PropertyCaller caller, CancellationToken ct = default) =>
        ListOptionsAsync(caller, null, ct);

    public async Task<IReadOnlyList<PropertyListItem>> ListOptionsAsync(
        PropertyCaller caller, string? keyword, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        var term = SearchTerm.Normalize(keyword);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = ScopeQuery(db, caller).AsNoTracking();
        if (term is { Length: > 0 } value)
        {
            query = query.Where(property => property.Name.Contains(value)
                || (property.Address != null && property.Address.Contains(value)));
        }

        return await LookupQuery.Bounded(query
                .OrderBy(property => property.Name)
                .ThenBy(property => property.Id)
                .Select(property => new PropertyListItem
                {
                    Id = property.Id,
                    Name = property.Name,
                    Address = property.Address,
                    IsActive = property.IsActive,
                }))
            .ToListAsync(ct);
    }

    public async Task<PropertyResult> GetAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await ScopeQuery(db, caller).AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        var roomCount = await db.Rooms.CountAsync(room => room.PropertyId == id, ct);
        return PropertyResult.Success(PropertyMapping.ToDetail(entity, roomCount));
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

    public async Task<PropertyResult> DeleteAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        // Ma trận P2: tạo/sửa/xóa nhà trọ chỉ Chủ trọ (Quản lý ❌).
        EnsureCanWrite(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await ScopeQuery(db, caller).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Room→Property là Restrict, còn phòng trong thùng rác thì không có đường xóa cứng,
        // nên chỉ chặn khi còn phòng nằm ngoài thùng rác (global filter đã loại IsDeleted).
        if (await db.Rooms.AnyAsync(r => r.PropertyId == id, ct))
        {
            return PropertyResult.Failure(
                PropertyErrorCodes.PropertyHasRooms, PropertyErrorMessages.PropertyHasRooms);
        }

        entity.IsDeleted = true;
        entity.DeletedAt = clock.GetUtcNow().UtcDateTime;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        return PropertyResult.Success();
    }

    public async Task<PagedResult<PropertyTrashItem>> ListDeletedAsync(
        PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        // Thùng rác nhà trọ chỉ Chủ trọ (giống thùng rác phòng VHT-98); phạm vi theo OwnerId.
        EnsureCanWrite(caller);
        filter.Normalize();

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = db.Properties.IgnoreQueryFilters()
            .Where(p => p.IsDeleted && p.OwnerId == caller.UserId);

        if (filter.Keyword is { Length: > 0 } keyword)
        {
            query = query.Where(p => p.Name.Contains(keyword) || (p.Address != null && p.Address.Contains(keyword)));
        }

        var total = await query.CountAsync(ct);
        var skip = (long)(filter.Page - 1) * filter.PageSize;
        var rows = await query
            .OrderByDescending(p => p.DeletedAt)
            .ThenByDescending(p => p.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .Select(p => new
            {
                Property = p,
                // Điều kiện ghi thẳng vì IgnoreQueryFilters tắt bộ lọc của mọi bảng trong query.
                TrashedRoomCount = db.Rooms.Count(r => r.PropertyId == p.Id && r.IsDeleted),
            })
            .ToListAsync(ct);

        return new PagedResult<PropertyTrashItem>
        {
            Items = rows.Select(r => new PropertyTrashItem
            {
                Id = r.Property.Id,
                Name = r.Property.Name,
                Address = r.Property.Address,
                TrashedRoomCount = r.TrashedRoomCount,
                DeletedAt = r.Property.DeletedAt,
            }).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = total,
        };
    }

    public async Task<PropertyResult> RestoreAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanWrite(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.Properties.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted && p.OwnerId == caller.UserId, ct);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        return PropertyResult.Success(PropertyMapping.ToDetail(entity));
    }

    public async Task<PropertyResult> PurgeAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanWrite(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.Properties.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted && p.OwnerId == caller.UserId, ct);
        if (entity is null)
        {
            return PropertyResult.Failure(PropertyErrorCodes.NotInTrash, PropertyErrorMessages.NotInTrash);
        }

        var inUse = await db.Rooms.IgnoreQueryFilters().AnyAsync(r => r.PropertyId == id, ct)
            || await db.ServiceCatalogs.IgnoreQueryFilters().AnyAsync(c => c.PropertyId == id, ct)
            || await db.ManagerApprovalRequests.AnyAsync(r => r.PropertyId == id, ct);
        if (inUse)
        {
            return PropertyResult.Failure(
                PropertyErrorCodes.PropertyHasDependencies,
                PropertyErrorMessages.PropertyHasDependencies);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var managers = await db.PropertyManagers.Where(m => m.PropertyId == id).ToListAsync(ct);
            db.PropertyManagers.RemoveRange(managers);
            db.Properties.Remove(entity);
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                Category = AuditLogCategory.Business,
                Action = "property.purge",
                ActorUserId = caller.UserId,
                PropertyId = id,
                Metadata = $"Property {entity.Name}",
                OccurredAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return PropertyResult.Success();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            return PropertyResult.Failure(
                PropertyErrorCodes.PropertyHasDependencies,
                PropertyErrorMessages.PropertyHasDependencies);
        }
    }

    public async Task<TrashPurgeSummary> PurgeAllAsync(
        PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanWrite(caller);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Một lượt đọc cho cả lô thay vì gọi PurgeAsync từng nhà (mỗi nhà mở context + 4-5 truy vấn).
        var properties = await db.Properties.IgnoreQueryFilters()
            .Where(p => p.IsDeleted && p.OwnerId == caller.UserId)
            .ToListAsync(ct);
        if (properties.Count == 0)
        {
            return new TrashPurgeSummary(0, 0);
        }

        var ids = properties.Select(p => p.Id).ToList();
        var blocked = new HashSet<int>();
        void AddBlocked(IEnumerable<int?> propertyIds)
        {
            foreach (var propertyId in propertyIds)
            {
                if (propertyId is { } value)
                {
                    blocked.Add(value);
                }
            }
        }

        AddBlocked(await db.Rooms.IgnoreQueryFilters()
            .Where(r => ids.Contains(r.PropertyId)).Select(r => (int?)r.PropertyId).Distinct().ToListAsync(ct));
        AddBlocked(await db.ServiceCatalogs.IgnoreQueryFilters()
            .Where(c => c.PropertyId != null && ids.Contains(c.PropertyId.Value)).Select(c => c.PropertyId).Distinct().ToListAsync(ct));
        AddBlocked(await db.ManagerApprovalRequests
            .Where(r => r.PropertyId != null && ids.Contains(r.PropertyId.Value)).Select(r => r.PropertyId).Distinct().ToListAsync(ct));

        var purgeProperties = properties.Where(p => !blocked.Contains(p.Id)).ToList();
        if (purgeProperties.Count == 0)
        {
            return new TrashPurgeSummary(0, properties.Count);
        }

        var purgeIds = purgeProperties.Select(p => p.Id).ToList();
        var managers = await db.PropertyManagers
            .Where(m => purgeIds.Contains(m.PropertyId))
            .ToListAsync(ct);
        db.PropertyManagers.RemoveRange(managers);

        db.Properties.RemoveRange(purgeProperties);
        foreach (var property in purgeProperties)
        {
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                Category = AuditLogCategory.Business,
                Action = "property.purge",
                ActorUserId = caller.UserId,
                PropertyId = property.Id,
                Metadata = $"Property {property.Name}",
                OccurredAt = DateTimeOffset.UtcNow,
            });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            return new TrashPurgeSummary(0, properties.Count);
        }

        return new TrashPurgeSummary(purgeProperties.Count, properties.Count - purgeProperties.Count);
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
