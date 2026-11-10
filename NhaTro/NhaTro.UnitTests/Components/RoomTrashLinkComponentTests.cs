using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Web.Common.Models;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Pages;
using Web.Features.Rooms.Services;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

// Nút mở thùng rác phòng trên trang danh sách phòng: cùng kiểu với trang nhà trọ,
// chỉ Chủ trọ thấy vì trang /rooms/trash chỉ mở cho vai trò này.
public sealed class RoomTrashLinkComponentTests : BunitContext
{
    public RoomTrashLinkComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./Components/Shared/Dialogs/ConfirmDialog.razor.js").Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton(new Web.Components.Shared.Feedback.ToastService());
    }

    [Fact]
    public void Chu_tro_thay_nut_mo_thung_rac_phong()
    {
        var cut = Render(new OwnerAuthenticationProvider());

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("a[href='/rooms/trash']")));
    }

    [Fact]
    public void Quan_ly_khong_thay_nut_mo_thung_rac_phong()
    {
        var cut = Render(new ManagerAuthenticationProvider());

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("h1")));
        Assert.Empty(cut.FindAll("a[href='/rooms/trash']"));
    }

    private IRenderedComponent<RoomList> Render(AuthenticationStateProvider auth)
    {
        Services.AddSingleton(new RoomPageService(
            new P2CallerProvider(auth), new FakeRoomService(), new FakePropertyService()));

        return Render<RoomList>();
    }

    private sealed class FakePropertyService : IPropertyService
    {
        public Task<PagedResult<PropertyListItem>> ListAsync(
            PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<PropertyListItem>
            {
                Items = [new PropertyListItem { Id = 1, Name = "Trọ A", IsActive = true }],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
            });

        public Task<PropertyResult> GetAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> CreateAsync(
            PropertySave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> UpdateAsync(
            int id, PropertySave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> DeleteAsync(
            int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PagedResult<PropertyTrashItem>> ListDeletedAsync(
            PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> RestoreAsync(
            int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRoomService : IRoomService
    {
        public Task<RoomSummary> GetSummaryAsync(PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(new RoomSummary());

        public Task<PagedResult<RoomListItem>> ListAsync(
            RoomFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<RoomListItem>
            {
                Items = [new RoomListItem { Id = 7, Code = "P101", PropertyId = 1, PropertyName = "Trọ A" }],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
            });

        public Task<RoomResult> GetAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RoomResult> CreateAsync(RoomSave request, PropertyCaller caller, CancellationToken ct = default) =>
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
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(State("chu-tro-1", Roles.ChuTro));
    }

    private sealed class ManagerAuthenticationProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(State("quan-ly-1", Roles.QuanLy));
    }

    private static AuthenticationState State(string userId, string role)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "test");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Trả lại chính key làm văn bản, đủ để kiểm tra cấu trúc UI.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
