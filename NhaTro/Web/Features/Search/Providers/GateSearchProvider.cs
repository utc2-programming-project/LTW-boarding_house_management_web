using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.AccessControl.Entities;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

/// <summary>Nhóm "Phiếu ra vào" (spec mục 1.2) — module AccessControl đã có trên nhánh này.</summary>
public sealed class GateSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "gate";
    public string Icon => "qr-code";
    public int Order => 8;

    // O-TK-04: Khách thuê chưa được chốt nhóm Phiếu ra vào.
    public bool CanRun(PropertyCaller caller) => !caller.IsKhachThue;

    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        IQueryable<GatePass> passes;
        if (c.Caller.IsAdmin)
        {
            passes = db.GatePasses.AsNoTracking();
        }
        else
        {
            var properties = PropertyScope.Apply(
                db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), c.Caller);
            passes = db.GatePasses.AsNoTracking()
                .Where(pass => properties.Any(property => property.Id == pass.PropertyId));
        }

        var tokens = SearchDbFilter.Tokens(c.NormalizedQuery);
        var rows = await (
            from pass in passes
            join property in db.Properties.AsNoTracking() on pass.PropertyId equals property.Id
            join room in db.Rooms.AsNoTracking() on pass.RoomId equals room.Id into rooms
            from room in rooms.DefaultIfEmpty()
            select new
            {
                pass.Id,
                pass.VisitorName,
                pass.VisitorPhone,
                RoomCode = room != null ? room.Code : null,
                PropertyName = property.Name,
            })
            .WhereMatches(
                tokens,
                x => x.VisitorName,
                x => x.VisitorPhone,
                x => x.RoomCode,
                x => x.PropertyName,
                // Tiêu đề "Phòng {mã}" khớp token "phong" — điều kiện DB luôn đúng.
                x => "phong")
            .ToListAsync(ct);

        var hits = rows
            .Select(x => new
            {
                Item = x,
                Score = SearchRanking.Best(
                    c.NormalizedQuery,
                    x.VisitorName,
                    x.VisitorPhone,
                    x.RoomCode,
                    x.PropertyName,
                    x.RoomCode is null ? null : $"Phòng {x.RoomCode}"),
            })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenByDescending(x => x.Item.Id)
            .Skip(c.Offset)
            .Take(c.Limit + 1)
            .ToArray();

        return new(
            GroupKey,
            Icon,
            hits.Take(c.Limit).Select(x => new SearchHitDto(
                GroupKey,
                Icon,
                x.Item.VisitorName,
                string.IsNullOrWhiteSpace(x.Item.RoomCode)
                    ? x.Item.PropertyName
                    : $"{x.Item.RoomCode} · {x.Item.PropertyName}",
                AppLinks.GatePasses())).ToArray(),
            hits.Length > c.Limit);
    }
}
