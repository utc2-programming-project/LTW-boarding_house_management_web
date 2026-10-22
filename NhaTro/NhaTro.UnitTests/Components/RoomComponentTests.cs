using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Web.Common.Models;
using Web.Components.Shared.Tables;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.Components;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Services;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

// Hồi quy nhánh 16 ở mức component (bUnit), không cần DB hay trình duyệt:
// B16-01 (form giá), B16-02 (empty state khi lọc), B16-05 (load lỗi thì khóa form).
public class RoomComponentTests : BunitContext
{
    private const string DialogModule = "./Components/Shared/Dialogs/ConfirmDialog.razor.js";

    private readonly FakeRoomService _rooms = new();

    public RoomComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(DialogModule).Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton(new RoomPageService(
            new P2CallerProvider(new OwnerAuthenticationProvider()), _rooms, null!));
    }

    private static RoomResult Found(string code = "P101") => RoomResult.Success(new RoomDetail
    {
        Id = 7,
        PropertyId = 1,
        Code = code,
        StandardPrice = 1_000_000m,
        IsActive = true,
    });

    private static IReadOnlyList<PropertyListItem> OneProperty() =>
        [new PropertyListItem { Id = 1, Name = "Trọ A", IsActive = true }];

    private IRenderedComponent<RoomFormDialog> RenderForm(int? roomId) =>
        Render<RoomFormDialog>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.RoomId, roomId)
            .Add(c => c.IsOwner, true)
            .Add(c => c.Properties, OneProperty()));

    // ---------- B16-05 ----------

    [Fact]
    public void B16_05_load_that_bai_thi_an_form_va_nut_luu()
    {
        _rooms.OnGet = _ => RoomResult.Failure(RoomErrorCodes.RoomNotFound, "not found");

        var cut = RenderForm(roomId: 7);

        cut.WaitForAssertion(() => cut.Find(".p2-alert.error"));
        Assert.Empty(cut.FindAll("#room-code"));
        Assert.Empty(cut.FindAll("#room-standard-price"));
        Assert.Empty(cut.FindAll("button[type=submit]"));
        Assert.Single(cut.FindAll(".p2-alert.error button")); // chỉ còn nút Thử lại
    }

    [Fact]
    public void B16_05_load_thanh_cong_thi_form_va_nut_luu_dung_duoc()
    {
        _rooms.OnGet = _ => Found("P101");

        var cut = RenderForm(roomId: 7);

        cut.WaitForAssertion(() => cut.Find("#room-code"));
        Assert.Equal("P101", cut.Find("#room-code").GetAttribute("value"));
        Assert.False(cut.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(".p2-alert.error"));
    }

    [Fact]
    public void B16_05_thu_lai_thanh_cong_thi_form_xuat_hien_lai()
    {
        var calls = 0;
        _rooms.OnGet = _ => ++calls == 1
            ? RoomResult.Failure(RoomErrorCodes.RoomNotFound, "not found")
            : Found("P101");

        var cut = RenderForm(roomId: 7);
        cut.WaitForAssertion(() => cut.Find(".p2-alert.error"));

        cut.Find(".p2-alert.error button").Click();

        cut.WaitForAssertion(() => cut.Find("#room-code"));
        Assert.Empty(cut.FindAll(".p2-alert.error"));
        Assert.False(cut.Find("button[type=submit]").HasAttribute("disabled"));
    }

    // ---------- B16-01 ----------

    [Fact]
    public void B16_01_form_phong_tat_validation_goc_va_khong_gioi_han_buoc_gia()
    {
        var cut = RenderForm(roomId: null);

        Assert.True(cut.Find("form").HasAttribute("novalidate"));
        Assert.Equal("any", cut.Find("#room-standard-price").GetAttribute("step"));
    }

    [Fact]
    public void B16_01_luu_duoc_gia_khong_chia_het_1000()
    {
        RoomSave? saved = null;
        _rooms.OnCreate = request =>
        {
            saved = request;
            return Found(request.Code);
        };

        var cut = RenderForm(roomId: null);
        cut.Find("#room-property-select").Change("1");
        cut.Find("#room-code").Change("P101");
        cut.Find("#room-standard-price").Change("2500500");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(saved));
        Assert.Equal(2_500_500m, saved!.StandardPrice);
    }

    // ---------- B16-02 ----------

    private sealed record Row(int Id);

    private IRenderedComponent<DataTable<Row>> RenderEmptyTable(
        bool hasActiveFilters, Action? onClear = null, bool withAddAction = false) =>
        Render<DataTable<Row>>(p =>
        {
            p.Add(c => c.Columns, new List<DataTableColumn<Row>>
            {
                new() { Key = "id", Title = "Id", Value = row => row.Id.ToString() },
            });
            p.Add(c => c.ItemsProvider, _ => Task.FromResult(new PagedResult<Row> { Items = [], TotalCount = 0 }));
            p.Add(c => c.HasActiveFilters, hasActiveFilters);
            if (onClear is not null)
            {
                p.Add(c => c.OnClearFilters, EventCallback.Factory.Create(this, onClear));
            }

            if (withAddAction)
            {
                p.Add(c => c.EmptyActions, (RenderFragment)(b =>
                    b.AddMarkupContent(0, "<button id=\"add-action\" type=\"button\">Add</button>")));
            }
        });

    [Fact]
    public void B16_02_loc_khong_co_ket_qua_hien_nut_xoa_bo_loc_khong_hien_nut_them()
    {
        var cut = RenderEmptyTable(hasActiveFilters: true, withAddAction: true);

        cut.WaitForAssertion(() => cut.Find(".empty-state"));
        Assert.Empty(cut.FindAll("#add-action"));
        Assert.Equal(new DataTableLabels().ClearFilters, cut.Find(".empty-state button").TextContent.Trim());
    }

    [Fact]
    public void B16_02_bam_xoa_bo_loc_goi_callback_cua_trang()
    {
        var cleared = 0;
        var cut = RenderEmptyTable(hasActiveFilters: true, onClear: () => cleared++);
        cut.WaitForAssertion(() => cut.Find(".empty-state button"));

        cut.Find(".empty-state button").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, cleared));
    }

    [Fact]
    public void B16_02_chua_co_du_lieu_va_khong_loc_thi_hien_nut_them()
    {
        var cut = RenderEmptyTable(hasActiveFilters: false, withAddAction: true);

        cut.WaitForAssertion(() => cut.Find("#add-action"));
    }

    // ---------- Đồ giả ----------

    private sealed class FakeRoomService : IRoomService
    {
        public Func<int, RoomResult> OnGet { get; set; } = _ => throw new NotSupportedException();
        public Func<RoomSave, RoomResult> OnCreate { get; set; } = _ => throw new NotSupportedException();

        public Task<RoomResult> GetAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(OnGet(id));

        public Task<RoomResult> CreateAsync(RoomSave request, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(OnCreate(request));

        public Task<RoomSummary> GetSummaryAsync(PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PagedResult<RoomListItem>> ListAsync(
            RoomFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomResult> UpdateAsync(
            int id, RoomSave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomResult> DeleteAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomResult> RestoreAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<List<RoomOption>> SearchForPickerAsync(
            RoomOptionRequest request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class OwnerAuthenticationProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "chu-tro-1"), new Claim(ClaimTypes.Role, Roles.ChuTro)],
                "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    /// <summary>Trả lại chính key làm văn bản, đủ để test cấu trúc UI mà không cần file .resx.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
