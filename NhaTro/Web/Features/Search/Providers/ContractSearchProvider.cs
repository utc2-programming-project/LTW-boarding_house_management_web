using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class ContractSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "contracts";
    public string Icon => "file";
    public int Order => 4;

    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        IQueryable<Contract> query;

        if (c.Caller.IsAdmin)
        {
            query = db.Contracts.AsNoTracking();
        }
        else if (c.Caller.IsKhachThue)
        {
            query = from contract in db.Contracts.AsNoTracking()
                    join member in db.ContractMembers on contract.ContractId equals member.ContractId
                    join tenant in db.Tenants on member.TenantId equals tenant.Id
                    where tenant.UserId == c.Caller.UserId && contract.Status != ContractStatus.Draft
                    select contract;
        }
        else if (c.Caller.IsChuTro)
        {
            query = from contract in db.Contracts.AsNoTracking()
                    join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                    join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                    where property.OwnerId == c.Caller.UserId
                    select contract;
        }
        else if (c.Caller.IsQuanLy)
        {
            // A07: loại cả nhà của chủ trọ đang bị khoá (caller.BlockedOwnerIds).
            var propertyIds = PropertyScope.ManagerPropertyIds(
                db.PropertyManagers, db.Properties.IgnoreQueryFilters(), c.Caller);
            query = from contract in db.Contracts.AsNoTracking()
                    join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                    where propertyIds.Contains(room.PropertyId)
                    select contract;
        }
        else
        {
            return new(GroupKey, Icon, [], false);
        }

        // D01: lọc phía DB trước khi nạp. Token có thể nằm ở số hợp đồng/phòng/nhà hoặc tên khách,
        // nên hợp (UNION) hai nguồn — tập DB luôn là tập cha của khớp C# — rồi ranking lọc lại.
        var tokens = SearchDbFilter.Tokens(c.NormalizedQuery);
        var nameContractIds = await (
            from member in db.ContractMembers.AsNoTracking()
            join tenant in db.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
            select new { member.ContractId, tenant.FullName })
            .WhereMatchesAnyToken(tokens, x => x.FullName)
            .Select(x => x.ContractId)
            .Distinct()
            .ToListAsync(ct);

        var projected =
            from contract in query
            join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
            join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
            select new
            {
                contract.ContractId,
                contract.ContractNumber,
                RoomCode = room.Code,
                PropertyName = property.Name,
            };

        var contracts = await projected
            .WhereMatches(tokens, x => x.ContractNumber, x => x.RoomCode, x => x.PropertyName)
            .Union(projected.Where(x => nameContractIds.Contains(x.ContractId)))
            .ToListAsync(ct);

        var contractIds = contracts.Select(x => x.ContractId).Distinct().ToArray();
        var members = await (
            from member in db.ContractMembers.AsNoTracking()
            join tenant in db.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
            where contractIds.Contains(member.ContractId)
            select new { member.ContractId, tenant.FullName }).ToListAsync(ct);
        var namesByContract = members
            .GroupBy(x => x.ContractId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.FullName).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray());

        var hits = contracts
            .Select(x =>
            {
                var names = namesByContract.GetValueOrDefault(x.ContractId, []);
                var fields = new List<string?>(3 + names.Length) { x.ContractNumber, x.RoomCode, x.PropertyName };
                fields.AddRange(names);
                return new { Item = x, Names = names, Score = SearchRanking.Best(c.NormalizedQuery, fields.ToArray()) };
            })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Item.ContractNumber, StringComparer.CurrentCulture)
            .ThenBy(x => x.Item.ContractId)
            .Skip(c.Offset)
            .Take(c.Limit + 1)
            .ToArray();

        return new(
            GroupKey,
            Icon,
            hits.Take(c.Limit).Select(x => new SearchHitDto(
                GroupKey,
                Icon,
                x.Item.ContractNumber,
                $"{x.Item.RoomCode} · {x.Item.PropertyName}",
                AppLinks.Contract(x.Item.ContractId))).ToArray(),
            hits.Length > c.Limit);
    }
}
