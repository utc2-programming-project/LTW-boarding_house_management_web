using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.Entities;

namespace Web.Features.Reports;

/// <summary>
/// Nguồn khách thuê P8 (VHT-46):
/// - Dùng đúng phạm vi P3 (<see cref="TenantVisibility"/>) và cách lọc của P3.
/// - KPI và danh sách chạy trên cùng query nên <c>Total</c> luôn khớp <c>Tenants.TotalCount</c>.
/// - Đếm khách đang thuê theo hồ sơ khách (không join nhân dòng): nhiều hợp đồng cùng hiệu lực
///   hoặc nhiều thành viên trong hợp đồng vẫn chỉ tính một khách.
/// - Không trả CCCD/CMND; Quản lý không thấy trạng thái liên kết tài khoản (D9).
/// - Quản lý phải qua SW-15 (xem báo cáo) và SW-15b (mục Khách thuê); Chủ trọ xem ngay (D4).
/// </summary>
public sealed class TenantReportService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    IManagerTogglePolicy togglePolicy) : ITenantReportService
{
    private const string ViewReportsCode = "SW-15";
    private const string TenantSectionCode = "SW-15b";
    private const string ForbiddenMessage = "Khong duoc xem bao cao khach thue.";

    public async Task<TenantReportKpis> GetAsync(TenantCaller caller, CancellationToken ct = default) =>
        (await GetAsync(new TenantReportFilter { PageSize = 1 }, caller, ct)).Kpis;

    public async Task<TenantReportResult> GetAsync(
        TenantReportFilter filter, TenantCaller caller, CancellationToken ct = default)
    {
        EnsureCanRead(caller);
        await EnsureSectionAllowedAsync(caller, ct);
        filter.Normalize();

        if (filter.HasAccount is not null && !caller.CanRevealIdNumber)
        {
            // D9: Quản lý không được suy ra trạng thái liên kết tài khoản qua số dòng trả về.
            throw new UnauthorizedAccessException(ForbiddenMessage);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var today = clock.GetUtcNow().UtcDateTime.Date;

        // Một predicate duy nhất cho KPI và danh sách.
        var query = ApplyFilter(db, ScopeTenants(db, caller), filter, caller, today);

        // Đếm theo trạng thái hồ sơ + tài khoản trong một truy vấn, không nhân dòng.
        var groups = await query
            .AsNoTracking()
            .GroupBy(tenant => tenant.IsActive)
            .Select(group => new
            {
                IsActive = group.Key,
                Count = group.Count(),
                Linked = group.Count(tenant => tenant.UserId != null),
            })
            .ToListAsync(ct);

        var total = groups.Sum(group => group.Count);
        var activeContracts = ActiveTenantIds(db, caller, filter.PropertyId, today);

        // query là tập hồ sơ khách (mỗi hồ sơ một dòng), nên Count + Contains không đếm trùng.
        var renting = await query
            .AsNoTracking()
            .CountAsync(tenant => activeContracts.Contains(tenant.Id), ct);

        var kpis = new TenantReportKpis
        {
            Total = total,
            Active = groups.Where(group => group.IsActive).Sum(group => group.Count),
            Renting = renting,
            // D9: chỉ Chủ trọ được xem số hồ sơ đã gắn tài khoản.
            LinkedAccountCount = caller.CanRevealIdNumber
                ? groups.Sum(group => group.Linked)
                : null,
        };

        var skip = (long)(filter.Page - 1) * filter.PageSize;
        var rows = await query
            .AsNoTracking()
            .OrderBy(tenant => tenant.FullName)
            .ThenBy(tenant => tenant.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .ToListAsync(ct);

        // Nạp hợp đồng hiện tại cho đúng trang để hiện phòng; chọn xác định khi có nhiều hợp đồng.
        var tenantIds = rows.Select(tenant => tenant.Id).ToArray();
        var activeRows = await ActiveContractRows(db, caller, filter.PropertyId, tenantIds, today)
            .ToListAsync(ct);
        var activeByTenant = activeRows
            .GroupBy(row => row.TenantId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.StartDate)
                    .ThenByDescending(row => row.ContractId)
                    .First());

        var items = rows
            .Select(tenant =>
            {
                var active = activeByTenant.GetValueOrDefault(tenant.Id);
                return new TenantReportItem
                {
                    Id = tenant.Id,
                    FullName = tenant.FullName,
                    PhoneNumber = tenant.Phone,
                    IsActive = tenant.IsActive,
                    HasAccount = caller.CanRevealIdNumber ? tenant.UserId is not null : null,
                    HasActiveContract = active is not null,
                    RoomCode = active?.RoomCode,
                    PropertyName = active?.PropertyName,
                };
            })
            .ToList();

        return new TenantReportResult
        {
            Kpis = kpis,
            Tenants = new PagedResult<TenantReportItem>
            {
                Items = items,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = total,
            },
        };
    }

    // Phạm vi P3 (VHT-119): Chủ trọ/Quản lý theo hợp đồng tại tòa thuộc quyền + kho hồ sơ chưa gắn hợp đồng.
    private static IQueryable<Tenant> ScopeTenants(ApplicationDbContext db, TenantCaller caller) =>
        TenantVisibility.Apply(db, caller.UserId, caller.Role);

    private static IQueryable<Tenant> ApplyFilter(
        ApplicationDbContext db,
        IQueryable<Tenant> query,
        TenantReportFilter filter,
        TenantCaller caller,
        DateTime today)
    {
        if (filter.PropertyId is { } propertyId)
        {
            // Lọc nhà ngay trong SQL; nhà ngoài phạm vi trả rỗng thay vì lộ liên kết khách - nhà khác.
            var tenantIds = TenantIdsAtProperty(db, caller, propertyId);
            query = query.Where(tenant => tenantIds.Contains(tenant.Id));
        }

        query = TenantService.ApplyKeyword(query, filter.Keyword);

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(tenant => tenant.IsActive == isActive);
        }

        if (filter.HasAccount is { } hasAccount)
        {
            query = hasAccount
                ? query.Where(tenant => tenant.UserId != null)
                : query.Where(tenant => tenant.UserId == null);
        }

        if (filter.HasActiveContract is { } hasActiveContract)
        {
            var activeTenantIds = ActiveTenantIds(db, caller, filter.PropertyId, today);
            query = hasActiveContract
                ? query.Where(tenant => activeTenantIds.Contains(tenant.Id))
                : query.Where(tenant => !activeTenantIds.Contains(tenant.Id));
        }

        return query;
    }

    /// <summary>
    /// Hồ sơ có ít nhất một hợp đồng (mọi trạng thái) tại nhà được chọn.
    /// Dùng EXISTS qua <see cref="IQueryable{T}"/> nên không nhân dòng khi có nhiều hợp đồng.
    /// </summary>
    private static IQueryable<int> TenantIdsAtProperty(
        ApplicationDbContext db, TenantCaller caller, int propertyId)
    {
        var visiblePropertyIds = VisiblePropertyIds(db, caller, propertyId);

        return
            from member in db.ContractMembers.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            where visiblePropertyIds.Contains(room.PropertyId)
            select member.TenantId;
    }

    /// <summary>
    /// Khách đang thuê trong phạm vi nhà của người gọi: hợp đồng đã duyệt và hôm nay nằm
    /// trong [StartDate, EndDate]. Hợp đồng tại nhà ngoài phạm vi không được tính/không lộ tên nhà.
    /// Nếu có lọc nhà thì chỉ tính hợp đồng tại nhà đó.
    /// </summary>
    private static IQueryable<int> ActiveTenantIds(
        ApplicationDbContext db, TenantCaller caller, int? propertyId, DateTime today)
    {
        var visiblePropertyIds = VisiblePropertyIds(db, caller, propertyId);

        return
            from member in db.ContractMembers.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            where contract.Status == ContractStatus.Approved
                && contract.StartDate <= today
                && contract.EndDate >= today
                && visiblePropertyIds.Contains(room.PropertyId)
            select member.TenantId;
    }

    private static IQueryable<TenantActiveContractRow> ActiveContractRows(
        ApplicationDbContext db,
        TenantCaller caller,
        int? propertyId,
        int[] tenantIds,
        DateTime today)
    {
        var visiblePropertyIds = VisiblePropertyIds(db, caller, propertyId);

        // Lọc nhà TRƯỚC khi chiếu sang record: EF chỉ dịch được Contains trên kiểu vô danh
        // (record private không có mapping nên không dịch được nếu Where đặt sau Select).
        var rows =
            from member in db.ContractMembers.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            where tenantIds.Contains(member.TenantId)
                && contract.Status == ContractStatus.Approved
                && contract.StartDate <= today
                && contract.EndDate >= today
                && visiblePropertyIds.Contains(room.PropertyId)
            select new
            {
                member.TenantId,
                contract.ContractId,
                RoomCode = room.Code,
                PropertyName = property.Name,
                room.PropertyId,
                contract.StartDate,
            };

        return rows.Select(row => new TenantActiveContractRow(
            row.TenantId, row.ContractId, row.RoomCode, row.PropertyName, row.PropertyId, row.StartDate));
    }

    /// <summary>
    /// Nhà thuộc phạm vi P2 của người gọi; nếu truyền <paramref name="propertyId"/> thì chỉ nhà đó.
    /// Nhà ngoài phạm vi không bao giờ xuất hiện trong tập ID.
    /// </summary>
    private static IQueryable<int> VisiblePropertyIds(
        ApplicationDbContext db, TenantCaller caller, int? propertyId = null)
    {
        var ids = PropertyScope
            .Apply(db.Properties, db.PropertyManagers, new PropertyCaller(caller.UserId, caller.Role))
            .Select(property => property.Id);

        return propertyId is { } id ? ids.Where(value => value == id) : ids;
    }

    private async Task EnsureSectionAllowedAsync(TenantCaller caller, CancellationToken ct)
    {
        // D4: Chủ trọ luôn xem ngay; Admin/Khách thuê đã bị chặn ở EnsureCanRead.
        if (!caller.IsQuanLy)
        {
            return;
        }

        // D12: SW-15* chỉ Cho phép/Không thể. Duyệt hoặc lạ đều đóng cửa (fail closed).
        foreach (var code in new[] { ViewReportsCode, TenantSectionCode })
        {
            var decision = await togglePolicy.EvaluateAsync(code, caller.Role, ct);
            if (decision != RoomChangeDecision.Allowed)
            {
                throw new UnauthorizedAccessException(ForbiddenMessage);
            }
        }
    }

    private static void EnsureCanRead(TenantCaller caller)
    {
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException(ForbiddenMessage);
        }
    }

    private sealed record TenantActiveContractRow(
        int TenantId,
        Guid ContractId,
        string RoomCode,
        string PropertyName,
        int PropertyId,
        DateTime StartDate);
}
