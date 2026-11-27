using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class InvoiceSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "invoices";
    public string Icon => "receipt";
    public int Order => 5;

    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        IQueryable<Contract> contracts;

        if (c.Caller.IsAdmin)
        {
            contracts = db.Contracts.AsNoTracking();
        }
        else if (c.Caller.IsKhachThue)
        {
            contracts = from member in db.ContractMembers
                        join tenant in db.Tenants on member.TenantId equals tenant.Id
                        join contract in db.Contracts on member.ContractId equals contract.ContractId
                        where tenant.UserId == c.Caller.UserId
                        select contract;
        }
        else if (c.Caller.IsChuTro)
        {
            contracts = from contract in db.Contracts.AsNoTracking()
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
            contracts = from contract in db.Contracts.AsNoTracking()
                        join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                        where propertyIds.Contains(room.PropertyId)
                        select contract;
        }
        else
        {
            return new(GroupKey, Icon, [], false);
        }

        // D01: lọc phía DB. Ngoài các trường văn bản còn nhận mã hóa đơn dạng số (HD-12/HD12/12)
        // và kỳ (MM/yyyy, yyyy-MM). Mỗi nguồn là tập cha; ranking trong bộ nhớ quyết định cuối cùng.
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
            from invoice in db.Invoices.AsNoTracking()
            join contract in contracts on invoice.ContractId equals contract.ContractId
            join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
            join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
            where !invoice.IsDeleted && (!c.Caller.IsKhachThue || invoice.Status != InvoiceStatus.Draft)
            select new
            {
                invoice.Id,
                invoice.PeriodStart,
                invoice.ContractId,
                contract.ContractNumber,
                RoomCode = room.Code,
                PropertyName = property.Name,
            };

        var invoiceIds = tokens
            .Select(TryParseInvoiceId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var period = tokens
            .Select(TryParsePeriod)
            .FirstOrDefault(value => value.HasValue);

        var candidates = projected
            .WhereMatches(tokens, x => x.ContractNumber, x => x.RoomCode, x => x.PropertyName)
            .Union(projected.Where(x => nameContractIds.Contains(x.ContractId)))
            .Union(projected.Where(x => invoiceIds.Contains(x.Id)));

        if (period.HasValue)
        {
            var month = period!.Value.Month;
            var year = period.Value.Year;
            candidates = candidates.Union(projected.Where(
                x => x.PeriodStart.Month == month && x.PeriodStart.Year == year));
        }

        var rows = await candidates.ToListAsync(ct);

        var contractIds = rows.Select(x => x.ContractId).Distinct().ToArray();
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

        var hits = rows
            .Select(x =>
            {
                var period = x.PeriodStart.ToString("MM/yyyy", CultureInfo.InvariantCulture);
                var periodIso = x.PeriodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                // A06/C07: cùng một chỗ tạo mã với UI (HD-{id}); vẫn nhận kiểu gõ cũ HD{id}.
                var invoiceCode = InvoiceCodes.Display(x.Id);
                var names = namesByContract.GetValueOrDefault(x.ContractId, []);
                var fields = new List<string?>(8 + names.Length)
                {
                    invoiceCode,
                    InvoiceCodes.Legacy(x.Id),
                    period,
                    periodIso,
                    x.ContractNumber,
                    x.RoomCode,
                    x.PropertyName,
                };
                fields.AddRange(names);
                return new
                {
                    Item = x,
                    InvoiceCode = invoiceCode,
                    Period = period,
                    Score = SearchRanking.Best(c.NormalizedQuery, fields.ToArray()),
                };
            })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenByDescending(x => x.Item.PeriodStart)
            .ThenBy(x => x.Item.Id)
            .Skip(c.Offset)
            .Take(c.Limit + 1)
            .ToArray();

        var basePath = c.Caller.IsKhachThue ? "/my/invoices" : "/invoices";
        return new(
            GroupKey,
            Icon,
            hits.Take(c.Limit).Select(x => new SearchHitDto(
                GroupKey,
                Icon,
                x.InvoiceCode,
                $"{x.Item.RoomCode} · {x.Item.PropertyName} · {x.Period}",
                AppLinks.Invoice(x.Item.Id, basePath))).ToArray(),
            hits.Length > c.Limit);
    }

    /// <summary>Nhận "HD-12", "HD12" hoặc "12" (không phân biệt hoa thường) thành id hóa đơn.</summary>
    private static int? TryParseInvoiceId(string token)
    {
        var value = token.Replace("-", string.Empty, StringComparison.Ordinal);
        if (value.StartsWith(InvoiceCodes.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[InvoiceCodes.Prefix.Length..];
        }

        if (value.Length == 0 || !value.All(char.IsAsciiDigit)) return null;
        return int.TryParse(value, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    /// <summary>Nhận kỳ dạng "MM/yyyy" hoặc "yyyy-MM" (O-TK-07).</summary>
    private static (int Month, int Year)? TryParsePeriod(string token)
    {
        var parts = token.Split(['/', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return null;
        if (!parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit))) return null;

        var first = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var second = int.Parse(parts[1], CultureInfo.InvariantCulture);

        var (month, year) = first > 12 ? (second, first) : (first, second);
        if (month is < 1 or > 12 || year is < 1900 or > 2200) return null;
        return (month, year);
    }
}
