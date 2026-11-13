using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Rooms.Mapping;
using Web.Features.Settings.Services;
using Web.Infrastructure.Time;

namespace Web.Features.Reports;

/// <summary>
/// Nguon phong cho bao cao P8 (VHT-41): dùng lại phạm vi P2 và đếm/list trên cùng một query.
/// Không đọc bảng hợp đồng — trạng thái phòng là nguồn chuẩn cho "đang thuê", tránh đếm trùng.
/// Quản lý phải qua công tắc SW-15 (xem báo cáo) và SW-15a (mục Phòng); Chủ trọ luôn xem (D4).
/// </summary>
public sealed class RoomReportService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IManagerTogglePolicy togglePolicy,
    TimeProvider? time = null) : IRoomReportService
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

    /// <summary>VHT-141 — lấp đầy theo tháng, cùng phạm vi quyền với báo cáo phòng.</summary>
    public async Task<OccupancyReportResult> GetOccupancyAsync(
        OccupancyFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        await EnsureSectionAllowedAsync(caller, ct);

        var today = DateOnly.FromDateTime(
            VietnamTime.ToVietnam((time ?? TimeProvider.System).GetUtcNow().UtcDateTime));
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var firstMonth = filter.From is { } from
            ? new DateOnly(from.Year, from.Month, 1)
            : currentMonth.AddMonths(-5);
        var lastMonth = filter.To is { } to
            ? new DateOnly(to.Year, to.Month, 1)
            : currentMonth;
        if (firstMonth > lastMonth)
        {
            return new OccupancyReportResult([]);
        }

        var rangeStart = firstMonth.ToDateTime(TimeOnly.MinValue);
        var rangeEndExclusive = lastMonth.AddMonths(1).ToDateTime(TimeOnly.MinValue);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var roomsQuery = ScopeRooms(db, caller).Where(room => room.IsActive);
        if (filter.PropertyId is { } propertyId)
        {
            roomsQuery = roomsQuery.Where(room => room.PropertyId == propertyId);
        }

        var rooms = await roomsQuery.AsNoTracking()
            .Select(room => new { room.Id, room.CreatedAt })
            .ToListAsync(ct);

        // Hợp đồng Approved/Liquidated giao với khoảng tháng; đếm phòng khác nhau mỗi tháng.
        var contracts = await (
            from contract in db.Contracts.AsNoTracking()
            join room in ScopeRooms(db, caller).AsNoTracking() on contract.RoomId equals room.Id
            where (contract.Status == ContractStatus.Approved
                    || contract.Status == ContractStatus.Liquidated)
                && (filter.PropertyId == null || room.PropertyId == filter.PropertyId)
                && contract.StartDate < rangeEndExclusive
                && contract.EndDate >= rangeStart
            select new { contract.RoomId, contract.StartDate, contract.EndDate })
            .ToListAsync(ct);

        var months = new List<OccupancyMonthDto>();
        for (var monthStart = firstMonth; monthStart <= lastMonth; monthStart = monthStart.AddMonths(1))
        {
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            // Phòng phải tồn tại trước cuối tháng; CreatedAt lưu UTC.
            var createdBeforeUtc = VietnamTime.DayRangeUtc(monthEnd).EndUtc;
            var eligibleRooms = rooms.Where(room => room.CreatedAt < createdBeforeUtc).ToList();
            var eligibleIds = eligibleRooms.Select(room => room.Id).ToHashSet();
            var monthStartDateTime = monthStart.ToDateTime(TimeOnly.MinValue);
            var nextMonthDateTime = monthStart.AddMonths(1).ToDateTime(TimeOnly.MinValue);
            var rented = contracts
                .Where(contract => eligibleIds.Contains(contract.RoomId)
                    && contract.StartDate < nextMonthDateTime
                    && contract.EndDate >= monthStartDateTime)
                .Select(contract => contract.RoomId)
                .Distinct()
                .Count();
            var total = eligibleRooms.Count;
            months.Add(new OccupancyMonthDto(
                monthStart.ToString("yyyy-MM"),
                monthStart,
                monthEnd,
                total,
                rented,
                Math.Max(0, total - rented),
                total == 0
                    ? null
                    : Math.Round(100m * rented / total, 1, MidpointRounding.AwayFromZero)));
        }

        return new OccupancyReportResult(months);
    }

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
