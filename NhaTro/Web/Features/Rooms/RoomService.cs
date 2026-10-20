using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Properties;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Rooms.Mapping;

namespace Web.Features.Rooms;

/// <summary>
/// Phòng P2 đợt 1C: query/save trên SQL, kiểm mã phòng theo nhà, giá/trạng thái, phân trang
/// và dữ liệu chọn phòng. Quyền theo docs/task/thao_tac_va_quyen.xlsx (P2).
/// </summary>
public sealed class RoomService(
    ApplicationDbContext db,
    TimeProvider clock,
    IRoomChangePolicy changePolicy,
    IRoomUsageChecker usageChecker) : IRoomService
{
    public async Task<RoomSummary> GetSummaryAsync(PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);

        var counts = await ScopeRooms(caller)
            .GroupBy(room => room.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        int CountFor(RoomStatus status) => counts.FirstOrDefault(item => item.Status == status)?.Count ?? 0;

        return new RoomSummary
        {
            Total = counts.Sum(item => item.Count),
            Available = CountFor(RoomStatus.Available),
            Rented = CountFor(RoomStatus.Rented),
            Maintenance = CountFor(RoomStatus.Maintenance)
        };
    }

    public async Task<PagedResult<RoomListItem>> ListAsync(
        RoomFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        filter.Normalize();

        var query = ApplyFilter(ScopeRooms(caller), filter);

        var total = await query.CountAsync(ct);
        var skip = (long)(filter.Page - 1) * filter.PageSize;
        var rows = await query
            .OrderBy(r => r.Code)
            .ThenBy(r => r.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .Select(r => new RoomRow(r, db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.Name).First()))
            .ToListAsync(ct);

        return new PagedResult<RoomListItem>
        {
            Items = rows.Select(x => RoomMapping.ToListItem(x.Room, x.PropertyName)).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = total,
        };
    }

    public async Task<RoomResult> GetAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);

        var row = await ScopeRooms(caller)
            .Where(r => r.Id == id)
            .Select(r => new RoomRow(r, db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.Name).First()))
            .FirstOrDefaultAsync(ct);

        return row is null
            ? NotFound()
            : RoomResult.Success(RoomMapping.ToDetail(row.Room, row.PropertyName));
    }

    public async Task<RoomResult> CreateAsync(
        RoomSave request, PropertyCaller caller, CancellationToken ct = default)
    {
        // Ma trận P2 không có dòng "Tạo phòng" cho Quản lý: chỉ Chủ trọ tạo.
        EnsureCanManage(caller);
        request.Normalize();

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!await PropertyInScopeAsync(request.PropertyId, caller, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.PropertyNotFound, RoomErrorMessages.PropertyNotFound);
        }

        if (await RoomCodeExistsAsync(request.PropertyId, request.Code, excludeRoomId: null, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        var entity = new Room { CreatedAt = clock.GetUtcNow().UtcDateTime };
        RoomMapping.ApplySave(request, entity);

        db.Rooms.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Hai request đồng thời cùng tạo một mã phòng trong cùng nhà.
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        var propertyName = await PropertyNameAsync(entity.PropertyId, ct);
        return RoomResult.Success(RoomMapping.ToDetail(entity, propertyName));
    }

    public async Task<RoomResult> UpdateAsync(
        int id, RoomSave request, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        request.Normalize();

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return invalid;
        }

        var entity = await ScopeRooms(caller).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Đổi nhà trọ: nhà đích cũng phải nằm trong phạm vi người gọi.
        if (entity.PropertyId != request.PropertyId
            && !await PropertyInScopeAsync(request.PropertyId, caller, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.PropertyNotFound, RoomErrorMessages.PropertyNotFound);
        }

        if (await RoomCodeExistsAsync(request.PropertyId, request.Code, id, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        // Điểm gọi policy công tắc (VHT-94): Quản lý sửa thông tin/giá/trạng thái phải qua công tắc.
        // Chủ trọ luôn có hiệu lực ngay (D4).
        if (!caller.IsChuTro)
        {
            foreach (var kind in ChangedKinds(entity, request))
            {
                var decision = await changePolicy.EvaluateAsync(
                    new RoomChangeRequest(caller.UserId, caller.Role, id, kind), ct);

                if (decision == RoomChangeDecision.Denied)
                {
                    throw Forbidden(caller);
                }

                if (decision == RoomChangeDecision.RequiresApproval)
                {
                    // VHT-7 chưa làm hàng đợi duyệt (VHT-96): trả trạng thái chờ duyệt, không ghi.
                    return RoomResult.Failure(RoomErrorCodes.ApprovalRequired, RoomErrorMessages.ApprovalRequired);
                }
            }
        }

        RoomMapping.ApplySave(request, entity);
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        var propertyName = await PropertyNameAsync(entity.PropertyId, ct);
        return RoomResult.Success(RoomMapping.ToDetail(entity, propertyName));
    }

    public async Task<RoomResult> DeleteAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        // Ma trận #15: chỉ Chủ trọ xóa phòng (Quản lý ❌).
        EnsureCanManage(caller);

        var entity = await ScopeRooms(caller).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Không xóa phòng còn hợp đồng hiệu lực (VHT-98 nối kiểm tra thật).
        if (await usageChecker.HasActiveContractAsync(id, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.RoomInUse, RoomErrorMessages.RoomInUse);
        }

        entity.IsDeleted = true;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        return RoomResult.Success();
    }

    public async Task<RoomResult> RestoreAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        // Ma trận #16: chỉ Chủ trọ khôi phục.
        EnsureCanManage(caller);

        var entity = await ScopeRooms(caller, includeDeleted: true)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Khôi phục khi mã phòng đã bị dùng lại cho phòng đang hoạt động => chặn (ma trận #16).
        if (await RoomCodeExistsAsync(entity.PropertyId, entity.Code, id, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        entity.IsDeleted = false;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        var propertyName = await PropertyNameAsync(entity.PropertyId, ct);
        return RoomResult.Success(RoomMapping.ToDetail(entity, propertyName));
    }

    public async Task<List<RoomOption>> SearchForPickerAsync(
        RoomOptionRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        request.Normalize();

        var query = ScopeRooms(caller).Where(r => r.IsActive);

        if (!request.IncludeUnavailable)
        {
            query = query.Where(r => r.Status == RoomStatus.Available);
        }

        if (request.PropertyId is { } propertyId)
        {
            query = query.Where(r => r.PropertyId == propertyId);
        }

        if (request.Keyword is { Length: > 0 } keyword)
        {
            query = query.Where(r => r.Code.Contains(keyword));
        }

        if (request.ExcludeRoomIds is { Count: > 0 } exclude)
        {
            query = query.Where(r => !exclude.Contains(r.Id));
        }

        var rows = await query
            .OrderBy(r => r.Code)
            .ThenBy(r => r.Id)
            .Take(request.Limit)
            .Select(r => new RoomRow(r, db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.Name).First()))
            .ToListAsync(ct);

        return rows.Select(x => RoomMapping.ToOption(x.Room, x.PropertyName)).ToList();
    }

    // Phạm vi P2 (VHT-6/VHT-24): Chủ trọ chỉ thấy phòng thuộc nhà mình.
    // Quản lý chưa có bảng phân công tòa nhà nên tạm đọc toàn bộ; VHT-24 chỉ sửa tại đây.
    private IQueryable<Room> ScopeRooms(PropertyCaller caller, bool includeDeleted = false)
    {
        var rooms = includeDeleted ? db.Rooms.IgnoreQueryFilters() : db.Rooms;

        if (caller.IsChuTro)
        {
            rooms = from r in rooms
                    join p in db.Properties on r.PropertyId equals p.Id
                    where p.OwnerId == caller.UserId
                    select r;
        }

        return rooms;
    }

    private static IQueryable<Room> ApplyFilter(IQueryable<Room> query, RoomFilter filter)
    {
        if (filter.PropertyId is { } propertyId)
        {
            query = query.Where(r => r.PropertyId == propertyId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        if (filter.OnlyAvailable == true)
        {
            query = query.Where(r => r.Status == RoomStatus.Available);
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(r => r.IsActive == isActive);
        }

        if (filter.Keyword is { Length: > 0 } keyword)
        {
            query = query.Where(r => r.Code.Contains(keyword));
        }

        if (filter.MinPrice is { } minPrice)
        {
            query = query.Where(r => r.StandardPrice >= minPrice);
        }

        if (filter.MaxPrice is { } maxPrice)
        {
            query = query.Where(r => r.StandardPrice <= maxPrice);
        }

        return query;
    }

    private async Task<bool> PropertyInScopeAsync(int propertyId, PropertyCaller caller, CancellationToken ct)
    {
        var query = db.Properties.Where(p => p.Id == propertyId);
        if (caller.IsChuTro)
        {
            query = query.Where(p => p.OwnerId == caller.UserId);
        }

        return await query.AnyAsync(ct);
    }

    // Trùng mã theo nhà, chỉ xét phòng chưa xóa mềm (khớp unique filtered index IX_Rooms_PropertyId_Code).
    private Task<bool> RoomCodeExistsAsync(int propertyId, string code, int? excludeRoomId, CancellationToken ct) =>
        db.Rooms.AnyAsync(
            r => r.PropertyId == propertyId && r.Code == code && (excludeRoomId == null || r.Id != excludeRoomId),
            ct);

    private Task<string> PropertyNameAsync(int propertyId, CancellationToken ct) =>
        db.Properties.Where(p => p.Id == propertyId).Select(p => p.Name).FirstAsync(ct);

    // Loại thay đổi để tra đúng công tắc SW-01/02/03.
    private static List<RoomChangeKind> ChangedKinds(Room entity, RoomSave request)
    {
        var kinds = new List<RoomChangeKind>();

        if (entity.StandardPrice != request.StandardPrice)
        {
            kinds.Add(RoomChangeKind.ChangePrice);
        }

        if (entity.Status != request.Status)
        {
            kinds.Add(RoomChangeKind.ChangeStatus);
        }

        if (entity.Code != request.Code
            || entity.PropertyId != request.PropertyId
            || entity.Floor != request.Floor
            || entity.AreaM2 != request.AreaM2
            || entity.IsActive != request.IsActive)
        {
            kinds.Add(RoomChangeKind.EditInfo);
        }

        // Không đổi gì: vẫn coi là sửa thông tin để không bỏ qua công tắc.
        if (kinds.Count == 0)
        {
            kinds.Add(RoomChangeKind.EditInfo);
        }

        return kinds;
    }

    // Kiểm SAU Normalize để khớp cột DB (Code 50, AreaM2 decimal(10,2), StandardPrice decimal(18,2)).
    private static RoomResult? Validate(RoomSave request)
    {
        if (string.IsNullOrEmpty(request.Code))
        {
            return RoomResult.Failure(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidRequest);
        }

        if (request.Code.Length > 50)
        {
            return RoomResult.Failure(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidLength);
        }

        if (request.PropertyId < 1)
        {
            return RoomResult.Failure(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidRequest);
        }

        if (request.Floor is < 0 or > 1000)
        {
            return RoomResult.Failure(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidFloor);
        }

        if (request.AreaM2 is { } area && (area < 0 || area > 99_999_999.99m))
        {
            return RoomResult.Failure(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidArea);
        }

        if (request.StandardPrice < 0 || request.StandardPrice > 9_999_999_999_999_999.99m)
        {
            return RoomResult.Failure(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidPrice);
        }

        if (!Enum.IsDefined(request.Status))
        {
            return RoomResult.Failure(RoomErrorCodes.InvalidRequest, RoomErrorMessages.InvalidStatus);
        }

        // Cột decimal(…,2): làm tròn về 2 chữ số thập phân thay vì để SQL cắt ngầm.
        request.StandardPrice = decimal.Round(request.StandardPrice, 2, MidpointRounding.AwayFromZero);
        if (request.AreaM2 is { } a)
        {
            request.AreaM2 = decimal.Round(a, 2, MidpointRounding.AwayFromZero);
        }

        return null;
    }

    private static void EnsureCanRead(PropertyCaller caller)
    {
        if (!caller.IsManager)
        {
            throw Forbidden(caller);
        }
    }

    private static void EnsureCanManage(PropertyCaller caller)
    {
        if (!caller.IsChuTro)
        {
            throw Forbidden(caller);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static UnauthorizedAccessException Forbidden(PropertyCaller caller) =>
        new(caller.IsAdmin
            ? "Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu phong."
            : RoomErrorMessages.Forbidden);

    private static RoomResult NotFound() =>
        RoomResult.Failure(RoomErrorCodes.RoomNotFound, RoomErrorMessages.RoomNotFound);

    /// <summary>Phòng kèm tên nhà trọ (Rooms không có navigation property).</summary>
    private sealed record RoomRow(Room Room, string PropertyName);
}
