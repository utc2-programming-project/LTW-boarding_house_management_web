using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;
using Web.Features.Storage.Enums;

namespace Web.Features.Search.Providers;

/// <summary>Nhóm "Tài liệu" cho Khách thuê (O-TK-03, spec mục 8).</summary>
public sealed class DocumentSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "documents";
    public string Icon => "file-text";
    public int Order => 9;

    // O-TK-03: nhóm Tài liệu chỉ dành cho Khách thuê.
    public bool CanRun(PropertyCaller caller) => caller.IsKhachThue;

    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        if (!c.Caller.IsKhachThue) return new(GroupKey, Icon, [], false);

        await using var db = await factory.CreateDbContextAsync(ct);
        var tenantId = await db.Tenants.AsNoTracking()
            .Where(tenant => tenant.UserId == c.Caller.UserId && !tenant.IsDeleted)
            .Select(tenant => tenant.Id)
            .FirstOrDefaultAsync(ct);
        if (tenantId == 0) return new(GroupKey, Icon, [], false);

        var relatedId = tenantId.ToString(CultureInfo.InvariantCulture);
        var tokens = SearchDbFilter.Tokens(c.NormalizedQuery);
        var rows = await db.StoredFiles.AsNoTracking()
            .Where(file => !file.IsDeleted
                && file.RelatedEntityType == RelatedEntityType.Tenant
                && file.RelatedEntityId == relatedId
                && file.FileCategory == FileCategory.IdentityDocument)
            .WhereMatches(tokens, file => file.FileName, file => file.Description)
            .Select(file => new { file.Id, file.FileName, file.Description, file.UploadedAt })
            .ToListAsync(ct);

        var hits = rows
            .Select(x => new { Item = x, Score = SearchRanking.Best(c.NormalizedQuery, x.FileName, x.Description) })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenByDescending(x => x.Item.UploadedAt)
            .Skip(c.Offset)
            .Take(c.Limit + 1)
            .ToArray();

        return new(
            GroupKey,
            Icon,
            hits.Take(c.Limit)
                .Select(x => new SearchHitDto(GroupKey, Icon, x.Item.FileName, null, AppLinks.TenantDocuments()))
                .ToArray(),
            hits.Length > c.Limit);
    }
}
