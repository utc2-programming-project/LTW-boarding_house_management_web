using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Web.Common.Models;
using Web.Components.Shared.Feedback;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Pages;
using Web.Features.Properties.Services;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

// Trang /properties/trash: liệt kê nhà trọ đã xóa mềm và cho Chủ trọ khôi phục lại.
public sealed class PropertyTrashComponentTests : BunitContext
{
    private const string DialogModule = "./Components/Shared/Dialogs/ConfirmDialog.razor.js";

    private readonly FakePropertyService _properties = new();
    private readonly ToastService _toasts = new();

    public PropertyTrashComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(DialogModule).Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton(_toasts);
        var callerProvider = new P2CallerProvider(new OwnerAuthenticationProvider());
        Services.AddSingleton(callerProvider);
        Services.AddSingleton(new PropertyPageService(callerProvider, _properties, new FakeManagerService()));
    }

    [Fact]
    public void Trang_thung_rac_hien_nha_da_xoa_va_nut_khoi_phuc()
    {
        _properties.Trash.Add(new PropertyTrashItem
        {
            Id = 5,
            Name = "Trọ bị xóa",
            TrashedRoomCount = 2,
            DeletedAt = new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc),
        });

        var cut = Render<PropertyTrash>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody button")));
        Assert.Contains("Trọ bị xóa", cut.Find("tbody").TextContent);
        Assert.Contains("P2PropertyTrashTitle", cut.Find("h1").TextContent);
    }

    [Fact]
    public void Bam_khoi_phuc_roi_xac_nhan_goi_dung_id_va_xoa_khoi_danh_sach()
    {
        _properties.Trash.Add(new PropertyTrashItem { Id = 5, Name = "Trọ bị xóa" });
        _properties.Trash.Add(new PropertyTrashItem { Id = 6, Name = "Trọ bị xóa 2" });

        var cut = Render<PropertyTrash>();
        cut.WaitForAssertion(() => Assert.Equal(2, RestoreButtonCount(cut)));

        ClickFirstRestoreButton(cut);
        cut.WaitForAssertion(() =>
            Assert.Equal("P2PropertyTrashRestoreTitle", cut.Find(".confirm-dialog .title").TextContent.Trim()));

        cut.Find(".confirm-dialog .footer button:nth-child(2)").Click();

        cut.WaitForAssertion(() => Assert.Equal([5], _properties.RestoredIds));
        cut.WaitForAssertion(() =>
            Assert.Contains("P2PropertyTrashRestored", _toasts.Toasts.Select(toast => toast.Text)));
        cut.WaitForAssertion(() => Assert.Equal(1, RestoreButtonCount(cut)));
    }

    [Fact]
    public void Khoi_phuc_bi_loi_thi_toast_hien_dung_thong_bao()
    {
        _properties.Trash.Add(new PropertyTrashItem { Id = 7, Name = "Trọ lỗi" });
        _properties.RestoreFailure = PropertyErrorCodes.PropertyNotFound;

        var cut = Render<PropertyTrash>();
        cut.WaitForAssertion(() => Assert.Equal(1, RestoreButtonCount(cut)));

        ClickFirstRestoreButton(cut);
        cut.Find(".confirm-dialog .footer button:nth-child(2)").Click();

        cut.WaitForAssertion(() => Assert.Equal("P2PropertyNotFound", _toasts.Toasts[0].Text));
        Assert.Empty(_properties.RestoredIds);
    }

    [Fact]
    public void Danh_sach_nha_tro_co_link_mo_thung_rac()
    {
        var cut = Render<PropertyList>();

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("a[href='/properties/trash']")));
    }

    private static int RestoreButtonCount(IRenderedComponent<PropertyTrash> cut) =>
        cut.FindAll("tbody button").Count;

    private static void ClickFirstRestoreButton(IRenderedComponent<PropertyTrash> cut) =>
        cut.FindAll("tbody button")[0].Click();

    private sealed class FakePropertyService : IPropertyService
    {
        public List<PropertyTrashItem> Trash { get; } = [];

        public List<int> RestoredIds { get; } = [];

        public string? RestoreFailure { get; set; }

        public Task<PagedResult<PropertyListItem>> ListAsync(
            PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<PropertyListItem>
            {
                Items = Trash.Select(item => new PropertyListItem { Id = item.Id, Name = item.Name }).ToList(),
                Page = 1,
                PageSize = 20,
                TotalCount = Trash.Count,
            });

        public Task<PagedResult<PropertyTrashItem>> ListDeletedAsync(
            PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<PropertyTrashItem>
            {
                Items = Trash.ToList(),
                Page = 1,
                PageSize = 20,
                TotalCount = Trash.Count,
            });

        public Task<PropertyResult> GetAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyResult.Success(new PropertyDetail { Id = id, OwnerId = caller.UserId }));

        public Task<PropertyResult> CreateAsync(
            PropertySave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> UpdateAsync(
            int id, PropertySave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> DeleteAsync(
            int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> RestoreAsync(
            int id, PropertyCaller caller, CancellationToken ct = default)
        {
            if (RestoreFailure is not null)
            {
                return Task.FromResult(PropertyResult.Failure(RestoreFailure, RestoreFailure));
            }

            RestoredIds.Add(id);
            Trash.RemoveAll(item => item.Id == id);
            return Task.FromResult(PropertyResult.Success());
        }
    }

    private sealed class FakeManagerService : IPropertyManagerService
    {
        public Task<PropertyManagerListResult> ListAssignmentsAsync(
            int propertyId, PropertyCaller caller, bool includeHistory = false,
            CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerListResult.Success([]));

        public Task<PropertyManagerCandidateResult> ListAssignableManagersAsync(
            int propertyId, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerCandidateResult.Success([]));

        public Task<PropertyManagerResult> AssignAsync(
            int propertyId, string managerUserId, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyManagerResult> RevokeAsync(
            int propertyId, string managerUserId, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyManagerCandidateListResult> ListCandidatesAsync(
            int propertyId, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerCandidateListResult.Success([]));

        public Task<PropertyManagerListResult> ListAssignmentsForManagerAsync(
            string managerUserId, PropertyCaller caller, bool includeHistory = false, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerListResult.Success([]));

        public Task<PropertyManagerPropertyListResult> ListAssignablePropertiesAsync(
            string managerUserId, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerPropertyListResult.Success([]));

        public Task<ManagerOwnerResult> GetOwnerForManagerAsync(
            PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(ManagerOwnerResult.Success(null));
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

    /// <summary>Trả lại chính key làm văn bản, đủ để kiểm tra cấu trúc UI và thông báo lỗi.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
