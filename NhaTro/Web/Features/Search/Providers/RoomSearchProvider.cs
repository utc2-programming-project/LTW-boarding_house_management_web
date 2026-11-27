using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class RoomSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "rooms";
    public string Icon => "door";
    public int Order => 1;

    private sealed record RoomRow(int Id, string Code, string Property, string Href);

    public async Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct)
    {
        var caller = context.Caller;
        var tokens = SearchDbFilter.Tokens(context.NormalizedQuery);
        await using var db = await factory.CreateDbContextAsync(ct);
        List<RoomRow> rows;

        if (caller.IsKhachThue)
        {
            // Khách thuê: "Phòng của mình" — qua liên kết hợp đồng thật, bỏ hợp đồng nháp.
            var own = await (
                from member in db.ContractMembers.AsNoTracking()
                join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
                join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                where tenant.UserId == caller.UserId && contract.Status != ContractStatus.Draft
                select new
                {
                    room.Id,
                    room.Code,
                    Property = property.Name,
                    contract.ContractId,
                    contract.StartDate,
                })
                .WhereMatches(tokens, x => x.Code, x => x.Property, x => "phong")
                .ToListAsync(ct);

            rows = own
                .GroupBy(x => x.Id)
                .Select(g => g.OrderByDescending(x => x.StartDate).ThenBy(x => x.ContractId).First())
                .Select(x => new RoomRow(x.Id, x.Code, x.Property, AppLinks.TenantRoom(x.ContractId)))
                .ToList();
        }
        else
        {
            // D02: một lần join Property cho cả lọc phạm vi lẫn lấy tên nhà.
            var properties = caller.IsAdmin
                ? db.Properties.AsNoTracking().Where(x => !x.IsDeleted)
                : PropertyScope.Apply(
                    db.Properties.AsNoTracking().Where(x => !x.IsDeleted),
                    db.PropertyManagers.AsNoTracking(),
                    caller);

            var scoped = await (
                from room in db.Rooms.AsNoTracking().Where(x => !x.IsDeleted)
                join property in properties on room.PropertyId equals property.Id
                select new { room.Id, room.Code, Property = property.Name })
                .WhereMatches(tokens, x => x.Code, x => x.Property, x => "phong")
                .ToListAsync(ct);
            rows = scoped.Select(x => new RoomRow(x.Id, x.Code, x.Property, AppLinks.Room(x.Id))).ToList();
        }

        // Hai truy vấn trên đều thêm trường hằng "phong" vì mọi tiêu đề phòng là "Phòng {mã}":
        // token "phong" khớp mọi dòng, điều kiện DB phải luôn thoả để không hẹp hơn ranking bộ nhớ.
        var matches = rows
            .Select(x => new
            {
                Item = x,
                Score = SearchRanking.Best(
                    context.NormalizedQuery,
                    x.Code,
                    x.Property,
                    // Cho phép "phong 101" khớp "Phòng 101" (spec mục 1.4).
                    $"Phòng {x.Code}"),
            })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Item.Code, StringComparer.CurrentCulture)
            .ThenBy(x => x.Item.Id)
            .Skip(context.Offset)
            .Take(context.Limit + 1)
            .ToArray();

        return new(
            GroupKey,
            Icon,
            matches.Take(context.Limit)
                .Select(x => new SearchHitDto(GroupKey, Icon, x.Item.Code, x.Item.Property, x.Item.Href))
                .ToArray(),
            matches.Length > context.Limit);
    }
}
