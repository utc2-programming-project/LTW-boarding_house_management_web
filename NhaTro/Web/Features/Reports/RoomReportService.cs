using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Rooms.Mapping;
using Web.Features.Settings.Services;

namespace Web.Features.Reports;

/// <summary>
/// Nguon phong cho bao cao P8 (VHT-41): dùng lại phạm vi P2 và đếm/list trên cùng một query.
/// Không đọc bảng hợp đồng — trạng thái phòng là nguồn chuẩn cho "đang thuê", tránh đếm trùng.
/// Quản lý phải qua công tắc SW-15 (xem báo cáo) và SW-15a (mục Phòng); Chủ trọ luôn xem (D4).
/// </summary>
public sealed class RoomReportService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IManagerTogglePolicy togglePolicy) : IRoomReportService
{
    private const string ViewReportsCode = "SW-15";
    private const string RoomSectionCode = "SW-15a";
    private const string ForbiddenMessage = "Khong duoc xem bao cao phong.";

    public async Task<RoomReportResult> GetAsync(
        RoomReportFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        await EnsureSectionAllowedAsync(caller, ct);
        filter.Normalize();

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Một predicate duy nhất: KPI và danh sách không thể lệch nhau.
        var query = ApplyFilter(ScopeRooms(db, caller), filter);

        var counts = await query
            .AsNoTracking()
            .GroupBy(room => room.Status)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(ct);

        int CountFor(RoomStatus status) =>
            counts.FirstOrDefault(item => item.Key == status)?.Count ?? 0;

        var total = counts.Sum(item => item.Count);

        var skip = (long)(filter.Page - 1) * filter.PageSize;
        var rows = await query
            .AsNoTracking()
            .OrderBy(r => r.Code)
            .ThenBy(r => r.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .Select(r => new RoomRow(r, db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.Name).First()))
            .ToListAsync(ct);

        return new RoomReportResult
        {
            Kpis = new RoomReportKpis
            {
                Total = total,
                Available = CountFor(RoomStatus.Available),
                Rented = CountFor(RoomStatus.Rented),
                Maintenance = CountFor(RoomStatus.Maintenance),
            },
            Rooms = new PagedResult<RoomListItem>
            {
                Items = rows.Select(x => RoomMapping.ToListItem(x.Room, x.PropertyName)).ToList(),
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = total,
            },
        };
    }

    // Phạm vi P2 (VHT-6/VHT-24): Chủ trọ theo OwnerId; Quản lý theo phân công nhà trọ đang hiệu lực.
    private static IQueryable<Room> ScopeRooms(ApplicationDbContext db, PropertyCaller caller) =>
        RoomScope.Apply(db.Rooms, db.Properties, db.PropertyManagers, caller);

    private static IQueryable<Room> ApplyFilter(IQueryable<Room> query, RoomReportFilter filter)
    {
        query = query.Where(room => room.IsActive == filter.IsActive);

        if (filter.PropertyId is { } propertyId)
        {
            query = query.Where(room => room.PropertyId == propertyId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(room => room.Status == status);
        }

        if (filter.Keyword is { Length: > 0 } keyword)
        {
            query = query.Where(room => room.Code.Contains(keyword));
        }

        return query;
    }

    private async Task EnsureSectionAllowedAsync(PropertyCaller caller, CancellationToken ct)
    {
        // D4: Chủ trọ luôn xem ngay; Admin/Khách thuê đã bị chặn ở EnsureCanRead.
        if (!caller.IsQuanLy)
        {
            return;
        }

        // D12: SW-15* chỉ Cho phép/Không thể. Duyệt hoặc lạ đều đóng cửa (fail closed).
        foreach (var code in new[] { ViewReportsCode, RoomSectionCode })
        {
            var decision = await togglePolicy.EvaluateAsync(code, caller.Role, ct);
            if (decision != RoomChangeDecision.Allowed)
            {
                throw new UnauthorizedAccessException(ForbiddenMessage);
            }
        }
    }

    private static void EnsureCanRead(PropertyCaller caller)
    {
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException(ForbiddenMessage);
        }
    }

    /// <summary>Phòng kèm tên nhà trọ (Rooms không có navigation property).</summary>
    private sealed record RoomRow(Room Room, string PropertyName);
}
