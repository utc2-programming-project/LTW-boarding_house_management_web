using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Properties;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Rooms.Mapping;
using Web.Features.Rooms.Services;
using Web.Features.Settings;
using Web.Features.Settings.Enums;

namespace Web.Features.Rooms;

/// <summary>
/// Service SQL cho phòng P2: kiểm mã phòng theo nhà, giá/trạng thái, phân trang
/// và dữ liệu chọn phòng. Quyền theo docs/task/thao_tac_va_quyen.xlsx (P2).
/// </summary>
public sealed class RoomService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    IRoomChangePolicy changePolicy,
    IRoomUsageChecker usageChecker,
    IManagerApprovalService? approvals = null) : IRoomService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public Task<RoomSummary> GetSummaryAsync(PropertyCaller caller, CancellationToken ct = default) =>
        GetSummaryCoreAsync(caller, null, ct);

    public Task<RoomSummary> GetSummaryForPropertyAsync(PropertyCaller caller, int? propertyId, CancellationToken ct = default) =>
        GetSummaryCoreAsync(caller, propertyId, ct);

    private async Task<RoomSummary> GetSummaryCoreAsync(PropertyCaller caller, int? propertyId, CancellationToken ct)
    {
        EnsureCanRead(caller);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var rooms = ScopeRooms(db, caller);
        if (propertyId is int selectedPropertyId)
        {
            rooms = rooms.Where(room => room.PropertyId == selectedPropertyId);
        }
        var counts = await rooms
            .GroupBy(room => new { room.Status, room.IsActive })
            .Select(group => new { group.Key.Status, group.Key.IsActive, Count = group.Count() })
            .ToListAsync(ct);

        int CountFor(RoomStatus status) =>
            counts.FirstOrDefault(item => item.Status == status && item.IsActive)?.Count ?? 0;

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
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = ApplyFilter(ScopeRooms(db, caller), filter);

        var total = await query.CountAsync(ct);
        var skip = (long)(filter.Page - 1) * filter.PageSize;
        var rows = await query
            .OrderBy(r => r.Code)
            .ThenBy(r => r.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .Select(r => new RoomRow(r, db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.Name).First()))
            .ToListAsync(ct);

        var pendingIds = await PendingApprovalRoomIdsAsync(db, rows.Select(row => row.Room.Id), ct);

        return new PagedResult<RoomListItem>
        {
            Items = rows.Select(x => RoomMapping.ToListItem(
                x.Room, x.PropertyName, pendingIds.Contains(x.Room.Id))).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = total,
        };
    }

    public async Task<RoomResult> GetAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var row = await ScopeRooms(db, caller)
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

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await PropertyInScopeAsync(db, request.PropertyId, caller, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.PropertyNotFound, RoomErrorMessages.PropertyNotFound);
        }

        if (await RoomCodeExistsAsync(db, request.PropertyId, request.Code, excludeRoomId: null, ct))
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

        var propertyName = await PropertyNameAsync(db, entity.PropertyId, ct);
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

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await ScopeRooms(db, caller).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Đổi nhà trọ: nhà đích cũng phải nằm trong phạm vi người gọi.
        if (entity.PropertyId != request.PropertyId
            && !await PropertyInScopeAsync(db, request.PropertyId, caller, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.PropertyNotFound, RoomErrorMessages.PropertyNotFound);
        }

        if (await RoomCodeExistsAsync(db, request.PropertyId, request.Code, id, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        // Điểm gọi policy công tắc (VHT-94): Quản lý sửa thông tin/giá/trạng thái phải qua công tắc.
        // Chủ trọ luôn có hiệu lực ngay (D4).
        RoomChangeKind? approvalKind = null;
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
                    // Một lần sửa có thể chạm nhiều công tắc; gửi một yêu cầu duy nhất mang
                    // toàn bộ đề xuất để Chủ trọ quyết định trọn vẹn, không áp dụng nửa vời.
                    approvalKind ??= kind;
                }
            }
        }

        if (approvalKind is { } kindToApprove)
        {
            return await CreateUpdateApprovalAsync(db, entity, request, kindToApprove, caller, ct);
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

        var propertyName = await PropertyNameAsync(db, entity.PropertyId, ct);
        return RoomResult.Success(RoomMapping.ToDetail(entity, propertyName));
    }

    // SW-01/02/03 ở chế độ "Duyệt": tạo yêu cầu Chờ duyệt, không ghi dữ liệu phòng.
    private async Task<RoomResult> CreateUpdateApprovalAsync(
        ApplicationDbContext db,
        Room entity,
        RoomSave request,
        RoomChangeKind kind,
        PropertyCaller caller,
        CancellationToken ct)
    {
        if (approvals is null)
        {
            return RoomResult.Failure(RoomErrorCodes.ApprovalRequired, RoomErrorMessages.ApprovalRequired);
        }

        var action = kind switch
        {
            RoomChangeKind.ChangePrice => ManagerAction.SW02,
            RoomChangeKind.ChangeStatus => ManagerAction.SW03,
            _ => ManagerAction.SW01,
        };
        var payload = JsonSerializer.Serialize(
            new RoomUpdateApprovalPayload(request, entity.UpdatedAt?.Ticks ?? 0), PayloadJsonOptions);

        var result = await approvals.CreateAsync(new CreateApprovalRequest
        {
            OperationCode = ManagerToggleCatalog.ToCode(action),
            TargetType = ApprovalTargetType.Room,
            TargetKey = entity.Id.ToString(CultureInfo.InvariantCulture),
            PropertyId = entity.PropertyId,
            PayloadJson = payload,
        }, new ApprovalCaller(caller.UserId, caller.Role), ct);

        return result.Succeeded
            ? RoomResult.Failure(RoomErrorCodes.ApprovalRequired, RoomErrorMessages.ApprovalRequired)
            : RoomResult.Failure(
                result.ErrorCode ?? RoomErrorCodes.InvalidRequest,
                result.ErrorMessage ?? RoomErrorMessages.InvalidRequest);
    }

    public async Task<RoomResult> DeleteAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        // Ma trận #15: chỉ Chủ trọ xóa phòng (Quản lý ❌).
        EnsureCanManage(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await ScopeRooms(db, caller).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Không xóa phòng còn hợp đồng hiệu lực (VHT-98).
        if (await usageChecker.HasActiveContractAsync(id, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.RoomInUse, RoomErrorMessages.RoomInUse);
        }

        entity.IsDeleted = true;
        entity.DeletedAt = clock.GetUtcNow().UtcDateTime;
        entity.DeletedBy = caller.UserId;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        return RoomResult.Success();
    }

    public async Task<PagedResult<RoomTrashItem>> ListDeletedAsync(
        RoomFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        // Ma trận #16: thùng rác phòng chỉ Chủ trọ.
        EnsureCanManage(caller);
        filter.Normalize();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = ApplyFilter(
            ScopeRooms(db, caller, includeDeleted: true).Where(r => r.IsDeleted), filter);

        var total = await query.CountAsync(ct);
        var skip = (long)(filter.Page - 1) * filter.PageSize;
        var rows = await query
            .OrderByDescending(r => r.DeletedAt)
            .ThenByDescending(r => r.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .Select(r => new RoomRow(r, db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.Name).First()))
            .ToListAsync(ct);

        return new PagedResult<RoomTrashItem>
        {
            Items = rows.Select(x => RoomMapping.ToTrashItem(x.Room, x.PropertyName)).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = total,
        };
    }

    public async Task<RoomResult> RestoreAsync(
        int id, PropertyCaller caller, CancellationToken ct = default)
    {
        // Ma trận #16: chỉ Chủ trọ khôi phục.
        EnsureCanManage(caller);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await ScopeRooms(db, caller, includeDeleted: true)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted, ct);
        if (entity is null)
        {
            return NotFound();
        }

        // Danh sách thùng rác dùng IgnoreQueryFilters nên phòng của nhà trọ đã xóa mềm vẫn hiện;
        // cho khôi phục sẽ tạo phòng mà không trang nào hiển thị được (phạm vi phòng suy qua nhà trọ).
        if (!await db.Properties.AnyAsync(p => p.Id == entity.PropertyId, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.PropertyDeleted, RoomErrorMessages.PropertyDeleted);
        }

        // Khôi phục khi mã phòng đã bị dùng lại trong cùng nhà => chặn (ma trận #16).
        if (await RoomCodeExistsAsync(db, entity.PropertyId, entity.Code, id, ct))
        {
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.DeletedBy = null;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return RoomResult.Failure(RoomErrorCodes.DuplicateRoomCode, RoomErrorMessages.DuplicateRoomCode);
        }

        var propertyName = await PropertyNameAsync(db, entity.PropertyId, ct);
        return RoomResult.Success(RoomMapping.ToDetail(entity, propertyName));
    }

    public async Task<List<RoomOption>> SearchForPickerAsync(
        RoomOptionRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        request.Normalize();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query = ScopeRooms(db, caller).Where(r => r.IsActive);

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

    // Phạm vi P2 (VHT-6/VHT-24): Chủ trọ chỉ thấy phòng thuộc nhà mình;
    // Quản lý chỉ thấy phòng thuộc nhà được phân công đang hiệu lực.
    private static IQueryable<Room> ScopeRooms(
        ApplicationDbContext db,
        PropertyCaller caller,
        bool includeDeleted = false)
    {
        var rooms = includeDeleted ? db.Rooms.IgnoreQueryFilters() : db.Rooms;
        // Thùng rác phòng phải thấy cả phòng thuộc nhà trọ đã xóa mềm (khớp TrashedRoomCount ở thùng rác nhà trọ).
        var properties = includeDeleted ? db.Properties.IgnoreQueryFilters() : db.Properties;
        return RoomScope.Apply(rooms, properties, db.PropertyManagers, caller);
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
            query = query.Where(r => r.IsActive && r.Status == RoomStatus.Available);
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

        if (filter.Floor is { } floor)
        {
            query = query.Where(r => r.Floor == floor);
        }

        if (filter.MinArea is { } minArea)
        {
            query = query.Where(r => r.AreaM2 != null && r.AreaM2 >= minArea);
        }

        if (filter.MaxArea is { } maxArea)
        {
            query = query.Where(r => r.AreaM2 != null && r.AreaM2 <= maxArea);
        }

        return query;
    }

    private static async Task<bool> PropertyInScopeAsync(
        ApplicationDbContext db,
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct)
    {
        var query = PropertyScope.Apply(db.Properties, db.PropertyManagers, caller).Where(p => p.Id == propertyId);
        return await query.AnyAsync(ct);
    }

    // Trùng mã chỉ xét trong cùng một nhà trọ, chỉ tính phòng chưa xóa mềm
    // (khớp unique filtered index IX_Rooms_PropertyId_Code của ERD VHT-5).
    internal static Task<bool> RoomCodeExistsAsync(
        ApplicationDbContext db,
        int propertyId,
        string code,
        int? excludeRoomId,
        CancellationToken ct) =>
        db.Rooms.AnyAsync(
            r => r.PropertyId == propertyId && r.Code == code && (excludeRoomId == null || r.Id != excludeRoomId),
            ct);

    // Hiển thị badge "Chờ duyệt" trên danh sách: chỉ xét yêu cầu còn hiệu lực.
    private static async Task<HashSet<int>> PendingApprovalRoomIdsAsync(
        ApplicationDbContext db, IEnumerable<int> roomIds, CancellationToken ct)
    {
        var keys = roomIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
        if (keys.Count == 0)
        {
            return [];
        }

        var pending = await db.ManagerApprovalRequests.AsNoTracking()
            .Where(request => request.Status == ApprovalRequestStatus.Pending
                && request.TargetType == ApprovalTargetType.Room
                && keys.Contains(request.TargetKey))
            .Select(request => request.TargetKey)
            .Distinct()
            .ToListAsync(ct);

        return pending
            .Select(key => int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();
    }

    private static Task<string> PropertyNameAsync(ApplicationDbContext db, int propertyId, CancellationToken ct) =>
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
    internal static RoomResult? Validate(RoomSave request)
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

        if (!request.IsActive && request.Status != RoomStatus.Available)
        {
            return RoomResult.Failure(
                RoomErrorCodes.InactiveRoomStatus,
                RoomErrorMessages.InactiveRoomStatus);
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
