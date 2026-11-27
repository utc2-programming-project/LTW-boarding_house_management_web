using System.Globalization;
using System.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Search;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Providers;
using Web.Features.Search.Services;

namespace NhaTro.UnitTests.Features.Search;

public sealed class SearchRankingTests
{
    [Fact]
    public void Phong_so_khop_khi_go_khong_dau_va_co_tien_to()
    {
        Assert.Equal(0, SearchRanking.Score("Phòng 101", "phong 101"));
        Assert.Equal(0, SearchRanking.Score("A101", "a101"));
        Assert.True(SearchRanking.Best("phong 101", "101", "Nhà Ánh Dương", "Phòng 101") == 0);
    }

    [Theory]
    [InlineData("A101", "a101", 0)]                 // khớp chính xác
    [InlineData("A101 B", "a101", 1)]               // đầu chuỗi
    [InlineData("Phòng A101", "a101", 2)]           // chứa
    [InlineData("Phòng A101 ban công mới", "a101 moi", 3)] // khớp đủ các từ rời nhau
    public void Bac_khop_dung_thu_tu_chinh_xac_dau_chuoi_chua_du_tu(
        string text, string query, int expected)
    {
        Assert.Equal(expected, SearchRanking.Score(text, query));
    }

    [Fact]
    public void Tu_khoa_nhieu_tu_khop_khi_moi_tu_nam_o_truong_khac_nhau()
    {
        Assert.True(SearchRanking.Best("nguyen 101", "Nguyễn Văn An", "A101") < int.MaxValue);
        Assert.Equal(int.MaxValue, SearchRanking.Best("nguyen 999", "Nguyễn Văn An", "A101"));
    }
}

public sealed class TextSearchNormalizerTests
{
    [Theory]
    [InlineData("Phòng 101", "phong 101")]
    [InlineData("Đức Mặt Trước", "duc mat truoc")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Bo_dau_va_thuong_hoa(string? input, string expected)
    {
        Assert.Equal(expected, TextSearchNormalizer.Normalize(input));
    }
}

public sealed class SearchDbFilterTests
{
    [Fact]
    public void Tach_token_theo_khoang_trang()
    {
        Assert.Equal(["phong", "101"], SearchDbFilter.Tokens("phong 101"));
        Assert.Equal(["a101"], SearchDbFilter.Tokens("  a101  "));
        Assert.Empty(SearchDbFilter.Tokens("   "));
    }

    [Fact]
    public void Pattern_bo_ky_tu_dai_dien_like()
    {
        Assert.Equal("%abcd%", SearchDbFilter.Pattern("a%b_c[d]"));
        Assert.Equal("%100%", SearchDbFilter.Pattern("100%"));
    }
}

public sealed class InvoiceCodesTests
{
    [Fact]
    public void Ma_hien_thi_va_kieu_go_cu_cung_mot_cho()
    {
        Assert.Equal("HD-12", InvoiceCodes.Display(12));
        Assert.Equal("HD12", InvoiceCodes.Legacy(12));
    }
}

public sealed class GlobalSearchServiceTests
{
    private sealed class FakeCallers(string? role, bool unauthorized = false) : ISearchCallerProvider
    {
        public Task<PropertyCaller> GetCallerAsync()
        {
            if (unauthorized) throw new UnauthorizedAccessException("no session");
            return Task.FromResult(new PropertyCaller("user-1", role!));
        }
    }

    private sealed class FixedProvider(string groupKey, bool throws) : ISearchProvider
    {
        public string GroupKey => groupKey;
        public string Icon => "door";
        public int Order => 1;

        public Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct)
        {
            if (throws) throw new InvalidOperationException("boom");
            return Task.FromResult(new SearchGroupResult(
                GroupKey,
                Icon,
                [new SearchHitDto(GroupKey, Icon, "Phòng 101", "Nhà A", "/rooms")],
                false));
        }
    }

    private sealed class SlowProvider(string groupKey) : ISearchProvider
    {
        public string GroupKey => groupKey;
        public string Icon => "receipt";
        public int Order => 2;
        public TimeSpan Timeout => TimeSpan.FromMilliseconds(50);

        public async Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new SearchGroupResult(GroupKey, Icon, [], false);
        }
    }

    private sealed class CapturingProvider : ISearchProvider
    {
        public string GroupKey => "rooms";
        public string Icon => "door";
        public int Order => 1;
        public int? SeenLimit { get; private set; }

        public Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct)
        {
            SeenLimit = context.Limit;
            return Task.FromResult(new SearchGroupResult(GroupKey, Icon, [], false));
        }
    }

    private sealed class HiddenProvider(string groupKey) : ISearchProvider
    {
        public string GroupKey => groupKey;
        public string Icon => "qr-code";
        public int Order => 2;

        public bool CanRun(PropertyCaller caller) => false;

        public Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct) =>
            throw new InvalidOperationException("Không được chạy nhóm này.");
    }

    [Fact]
    public void SearchRequest_tinh_offset_tu_so_trang()
    {
        Assert.Equal(0, new SearchRequest("x").Offset);
        Assert.Equal(10, new SearchRequest("x", Limit: 5, Page: 2).Offset);
        Assert.Equal(0, new SearchRequest("x", Limit: 5, Page: -1).Offset);
    }

    [Fact]
    public void SearchRequest_kep_limit_trong_khoang_cho_phep()
    {
        // D03: chỉ chặn dưới là chưa đủ, phải chặn cả trần để caller không kéo cả bảng.
        Assert.Equal(SearchLimits.MaxPerGroup, new SearchRequest("x", Limit: 1000).EffectiveLimit);
        Assert.Equal(1, new SearchRequest("x", Limit: 0).EffectiveLimit);
        Assert.Equal(SearchLimits.MaxPerGroup, new SearchRequest("x", Limit: 999, Page: 1).Offset);
    }

    [Fact]
    public async Task Nhom_khong_duoc_phep_thi_khong_chay_va_nhan_nhom_duoc_dien()
    {
        var result = await Service(
                Roles.ChuTro,
                new FixedProvider("rooms", throws: false),
                new HiddenProvider("gate"))
            .SearchAsync(new SearchRequest("phong"), CancellationToken.None);

        Assert.Single(result.Groups);
        Assert.Equal("SearchGroupRooms", result.Groups[0].GroupLabelKey);
        Assert.Equal(1, result.ProviderCount);
    }

    [Fact]
    public async Task GetGroups_chi_tra_nhom_vai_tro_duoc_phep()
    {
        var groups = await Service(
                Roles.ChuTro,
                new FixedProvider("rooms", throws: false),
                new HiddenProvider("gate"))
            .GetGroupsAsync(CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.Equal("rooms", group.GroupKey);
        Assert.Equal("SearchGroupRooms", group.GroupLabelKey);
    }

    private static GlobalSearchService Service(string role, params ISearchProvider[] providers) =>
        new(providers, new FakeCallers(role), NullLogger<GlobalSearchService>.Instance);

    [Fact]
    public async Task Admin_khong_bi_chan_khoi_tim_kiem()
    {
        var result = await Service(Roles.Admin, new FixedProvider("rooms", throws: false))
            .SearchAsync("phong", null, 5, 0, CancellationToken.None);

        Assert.Single(result.Groups);
        Assert.Empty(result.FailedGroups ?? Array.Empty<string>());
    }

    [Fact]
    public async Task Mot_provider_loi_van_tra_nhom_con_lai()
    {
        var result = await Service(
                Roles.ChuTro,
                new FixedProvider("rooms", throws: false),
                new FixedProvider("contracts", throws: true))
            .SearchAsync("phong", null, 5, 0, CancellationToken.None);

        Assert.Single(result.Groups);
        Assert.Equal(new[] { "contracts" }, result.FailedGroups);
        Assert.Equal(2, result.ProviderCount);
    }

    [Fact]
    public async Task Tat_ca_provider_loi_duoc_bao_la_loi_khong_phai_rong()
    {
        var result = await Service(
                Roles.ChuTro,
                new FixedProvider("rooms", throws: true),
                new FixedProvider("contracts", throws: true))
            .SearchAsync("phong", null, 5, 0, CancellationToken.None);

        Assert.Empty(result.Groups);
        Assert.Equal(2, result.FailedGroups?.Count);
        Assert.Equal(2, result.ProviderCount);
    }

    [Fact]
    public async Task Provider_qua_timeout_bi_bo_nhung_nhom_khac_van_tra()
    {
        var result = await Service(
                Roles.ChuTro,
                new FixedProvider("rooms", throws: false),
                new SlowProvider("invoices"))
            .SearchAsync("phong", null, 5, 0, CancellationToken.None);

        Assert.Single(result.Groups);
        Assert.Equal("rooms", result.Groups[0].GroupKey);
        Assert.Equal(new[] { "invoices" }, result.FailedGroups);
    }

    [Fact]
    public async Task Provider_nhan_limit_da_kep_thay_vi_limit_tho()
    {
        var provider = new CapturingProvider();
        await Service(Roles.ChuTro, provider)
            .SearchAsync("phong", null, limit: 1000, offset: 0, CancellationToken.None);

        Assert.Equal(SearchLimits.MaxPerGroup, provider.SeenLimit);
    }

    [Fact]
    public async Task Phien_khong_hop_le_tra_rong_thay_vi_no()
    {
        var service = new GlobalSearchService(
            [new FixedProvider("rooms", throws: false)],
            new FakeCallers(Roles.ChuTro, unauthorized: true),
            NullLogger<GlobalSearchService>.Instance);

        var result = await service.SearchAsync(new SearchRequest("phong"), CancellationToken.None);
        Assert.Empty(result.Groups);

        var groups = await service.GetGroupsAsync(CancellationToken.None);
        Assert.Empty(groups);
    }
}

/// <summary>Ma trận nhóm theo vai trò (spec mục 8, O-TK-04) — dựng provider thật, không chạm DB.</summary>
public sealed class SearchProviderScopeTests
{
    private sealed class ThrowingFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => throw new InvalidOperationException("CanRun không được mở DB.");

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("CanRun không được mở DB.");
    }

    internal static ISearchProvider[] AllProviders()
    {
        var factory = new ThrowingFactory();
        return
        [
            new RoomSearchProvider(factory),
            new PropertySearchProvider(factory),
            new TenantSearchProvider(factory),
            new ContractSearchProvider(factory),
            new InvoiceSearchProvider(factory),
            new IncidentSearchProvider(factory, TimeProvider.System),
            new ServiceSearchProvider(factory),
            new GateSearchProvider(factory),
            new DocumentSearchProvider(factory),
        ];
    }

    private sealed class FakeCallers(string role) : ISearchCallerProvider
    {
        public Task<PropertyCaller> GetCallerAsync() => Task.FromResult(new PropertyCaller("user-1", role));
    }

    private static async Task<string> GroupsFor(string role)
    {
        var service = new GlobalSearchService(
            AllProviders(),
            new FakeCallers(role),
            NullLogger<GlobalSearchService>.Instance);
        var groups = await service.GetGroupsAsync(CancellationToken.None);
        return string.Join(",", groups.Select(g => g.GroupKey).OrderBy(k => k, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.ChuTro)]
    [InlineData(Roles.QuanLy)]
    public async Task Chu_tro_quan_ly_admin_thay_du_nhom_tru_tai_lieu(string role)
    {
        Assert.Equal(
            "contracts,gate,incidents,invoices,properties,rooms,services,tenants",
            await GroupsFor(role));
    }

    [Fact]
    public async Task Khach_thue_chi_thay_nhom_cua_minh_va_tai_lieu()
    {
        Assert.Equal(
            "contracts,documents,incidents,invoices,rooms,tenants",
            await GroupsFor(Roles.KhachThue));
    }

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public void Moi_nhom_va_chuoi_trang_co_ban_dich(string culture)
    {
        // AddLocalization(ResourcesPath = "Resources") → tên tài nguyên nhúng theo đường dẫn vật lý.
        var manager = new ResourceManager(
            "Web.Resources.Features.Search.SearchResources", typeof(SearchResources).Assembly);
        var info = CultureInfo.GetCultureInfo(culture);

        var keys = AllProviders()
            .Select(p => p.GroupLabelKey)
            .Concat(["SearchGroupVehicles"])
            .Concat(PageKeys)
            .ToArray();

        foreach (var key in keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(manager.GetString(key, info)), $"{culture}:{key}");
        }
    }

    private static readonly string[] PageKeys =
    [
        "SearchTitle", "SearchPlaceholder", "SearchLoading", "SearchError", "SearchMinChars",
        "SearchNoResults", "SearchViewAll", "SearchAllGroups", "SearchPrevPage", "SearchNextPage",
        "SearchPage", "SearchHasMore", "SearchPartialError",
    ];
}
