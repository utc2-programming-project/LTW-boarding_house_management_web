using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Security.Claims;
using Web.Common.Models;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Enums;
using Web.Features.Rooms.Services;
using Web.Features.Properties.Components;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

/// <summary>PLAN-011: bước thêm phòng trong wizard thiết lập nhà trọ.</summary>
public sealed class PropertySetupRoomsStepTests : BunitContext
{
    private const string DialogModule = "./Components/Shared/Dialogs/ConfirmDialog.razor.js";

    private readonly FakeRoomService _rooms = new();

    public PropertySetupRoomsStepTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(DialogModule).Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton(new RoomPageService(
            new P2CallerProvider(new OwnerAuthenticationProvider()), _rooms, null!));
    }

    private static RoomListItem Room(int id, string code, int? floor, decimal price = 1_000_000m) => new()
    {
        Id = id,
        PropertyId = 1,
        Code = code,
        Floor = floor,
        StandardPrice = price,
        Status = RoomStatus.Available,
        IsActive = true,
    };

    private IRenderedComponent<PropertySetupRoomsStep> RenderStep(
        Action<ComponentParameterCollectionBuilder<PropertySetupRoomsStep>>? configure = null) =>
        Render<PropertySetupRoomsStep>(parameters =>
        {
            parameters
                .Add(component => component.PropertyId, 1)
                .Add(component => component.PropertyName, "Trọ A")
                .Add(component => component.IsOwner, true);
            configure?.Invoke(parameters);
        });

    [Fact]
    public void Chua_co_phong_thi_hien_goi_y_va_nut_bo_qua_van_tiep_tuc_duoc()
    {
        _rooms.OnList = _ => Page();

        var continued = false;
        var cut = RenderStep(p => p.Add(component => component.OnContinue, EventCallback.Factory.Create(this, () => continued = true)));

        cut.WaitForAssertion(() => cut.Find(".setup-empty"));
        Assert.Contains("P2SetupRoomsEmpty", cut.Find(".setup-empty").TextContent);

        cut.Find("footer.setup-step-actions button.btn-primary").Click();
        Assert.True(continued);
    }

    [Fact]
    public void Danh_sach_phong_duoc_sap_xep_theo_tang_roi_ma_phong()
    {
        _rooms.OnList = _ => Page(
            Room(3, "P201", 2),
            Room(1, "P102", 1),
            Room(2, "P101", 1));

        var cut = RenderStep();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".setup-room-item").Count));
        Assert.Contains("P2SetupRoomsCount", cut.Find(".setup-room-count").TextContent);

        var codes = cut.FindAll(".setup-room-code").Select(node => node.TextContent).ToArray();
        Assert.Equal(new[] { "P101", "P102", "P201" }, codes);
    }

    [Fact]
    public void Bam_them_phong_mo_form_nhap_phong()
    {
        _rooms.OnList = _ => Page();
        var cut = RenderStep();

        cut.WaitForAssertion(() => cut.Find(".setup-empty"));
        cut.Find(".setup-step-head button.btn-primary").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#room-code")));
    }

    [Fact]
    public void Dong_form_khi_chua_luu_thi_giu_lai_nhap_do()
    {
        _rooms.OnList = _ => Page();
        RoomSave? captured = null;
        var cut = RenderStep(p => p.Add(
            component => component.OnPendingRoomChanged,
            EventCallback.Factory.Create<RoomSave?>(this, draft => captured = draft)));

        cut.WaitForAssertion(() => cut.Find(".setup-empty"));
        cut.Find(".setup-step-head button.btn-primary").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#room-code")));

        cut.Find("#room-code").Change("P101");
        cut.Find(".room-form-dialog .feature-dialog-header button.icon-button").Click();

        cut.WaitForAssertion(() => Assert.NotNull(captured));
        Assert.Equal("P101", captured!.Code);
        Assert.Equal(1, captured.PropertyId);
    }

    private static PagedResult<RoomListItem> Page(params RoomListItem[] items) => new()
    {
        Items = items,
        TotalCount = items.Length,
        Page = 1,
        PageSize = 100,
    };

    private sealed class FakeRoomService : IRoomService
    {
        public Func<RoomFilter, PagedResult<RoomListItem>> OnList { get; set; } =
            _ => new PagedResult<RoomListItem> { Items = [], TotalCount = 0 };

        public Task<PagedResult<RoomListItem>> ListAsync(
            RoomFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(OnList(filter));

        public Task<RoomResult> GetAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomResult> CreateAsync(RoomSave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomSummary> GetSummaryAsync(PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomResult> UpdateAsync(
            int id, RoomSave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomResult> DeleteAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomResult> RestoreAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PagedResult<RoomTrashItem>> ListDeletedAsync(
            RoomFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
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

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
