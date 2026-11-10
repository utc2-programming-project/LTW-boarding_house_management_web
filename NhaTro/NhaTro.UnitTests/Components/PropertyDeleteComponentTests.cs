using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Security.Claims;
using Web.Common.Models;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Pages;
using Web.Features.Properties.Services;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

// Nút xóa nhà trọ ở mức component: chỉ Chủ trọ thấy nút, hộp xác nhận gọi đúng
// PropertyPageService.DeleteAsync, và lỗi nghiệp vụ (còn phòng) hiện bằng đúng key bản dịch.
public sealed class PropertyDeleteComponentTests : BunitContext
{
    private const string DialogModule = "./Components/Shared/Dialogs/ConfirmDialog.razor.js";

    private readonly FakePropertyService _properties = new();
    private readonly Web.Components.Shared.Feedback.ToastService _toasts = new();

    public PropertyDeleteComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule(DialogModule).Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton(_toasts);
    }

    [Fact]
    public void Chu_tro_thay_nut_xa_o_dong_danh_sach()
    {
        _properties.Add(1, "Trọ A");

        var cut = RenderOwner();

        cut.WaitForAssertion(() => Assert.Equal(1, RowCount(cut)));
    }

    [Fact]
    public void Quan_ly_khong_thay_nut_xa_nha_tro()
    {
        _properties.Add(1, "Trọ A", assignedToManager: true);

        var cut = Render(new ManagerAuthenticationProvider());

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("tbody")));
        Assert.Equal(0, RowCount(cut));
    }

    [Fact]
    public void Bam_xa_roi_xac_nhan_goi_dung_id_va_bao_thanh_cong()
    {
        _properties.Add(1, "Trọ A");
        _properties.Add(2, "Trọ B");
        var cut = RenderOwner();
        cut.WaitForAssertion(() => Assert.Equal(2, RowCount(cut)));

        cut.FindAll("button[aria-label='P2DeleteProperty']")[0].Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("P2DeletePropertyTitle", cut.Find(".confirm-dialog .title").TextContent.Trim()));

        Confirm(cut);

        cut.WaitForAssertion(() => Assert.Single(_properties.DeletedIds));
        cut.WaitForAssertion(() =>
            Assert.Contains("P2PropertyDeleted", _toasts.Toasts.Select(toast => toast.Text)));
        cut.WaitForAssertion(() => Assert.Equal(1, RowCount(cut)));
    }

    [Fact]
    public void Nha_con_phong_bi_chan_xoa_thi_toast_hien_dung_thong_bao()
    {
        _properties.Add(3, "Trọ còn phòng", deleteFailure: PropertyErrorCodes.PropertyHasRooms);
        var cut = RenderOwner();
        cut.WaitForAssertion(() => Assert.Equal(1, RowCount(cut)));

        cut.Find("button[aria-label='P2DeleteProperty']").Click();
        Confirm(cut);

        cut.WaitForAssertion(() => Assert.Equal("P2PropertyHasRooms", _toasts.Toasts[0].Text));
        Assert.Empty(_properties.DeletedIds);
        Assert.Equal(1, RowCount(cut));
    }

    [Fact]
    public void Huy_hop_xac_nhan_thi_khong_goi_service()
    {
        _properties.Add(1, "Trọ A");
        var cut = RenderOwner();
        cut.WaitForAssertion(() => Assert.Equal(1, RowCount(cut)));

        cut.Find("button[aria-label='P2DeleteProperty']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".confirm-dialog")));

        cut.Find(".confirm-dialog .footer button:first-child").Click();

        Assert.Empty(_properties.DeletedIds);
        Assert.Empty(_toasts.Toasts);
    }

    private IRenderedComponent<PropertyList> RenderOwner() =>
        Render(new OwnerAuthenticationProvider());

    private IRenderedComponent<PropertyList> Render(AuthenticationStateProvider auth)
    {
        var callerProvider = new P2CallerProvider(auth);
        Services.AddSingleton(callerProvider);
        Services.AddSingleton(new PropertyPageService(callerProvider, _properties, new FakeManagerService()));

        return Render<PropertyList>();
    }

    private static int RowCount(IRenderedComponent<PropertyList> cut) =>
        cut.FindAll("tbody button[aria-label='P2DeleteProperty']").Count;

    // Nút xác nhận là nút thứ hai trong footer của ConfirmDialog.
    private static void Confirm(IRenderedComponent<PropertyList> cut) =>
        cut.Find(".confirm-dialog .footer button:nth-child(2)").Click();

    private sealed class FakePropertyService : IPropertyService
    {
        private readonly List<PropertyListItem> _items = [];
        private readonly Dictionary<int, string?> _failures = [];

        public List<int> DeletedIds { get; } = [];

        public void Add(int id, string name, bool assignedToManager = false, string? deleteFailure = null)
        {
            _items.Add(new PropertyListItem { Id = id, Name = name, IsActive = true });
            _failures[id] = deleteFailure;
            if (assignedToManager) AssignedToManager = true;
        }

        public bool AssignedToManager { get; private set; }

        public Task<PagedResult<PropertyListItem>> ListAsync(
            PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default)
        {
            List<PropertyListItem> visible = caller.IsChuTro || AssignedToManager ? _items : [];

            return Task.FromResult(new PagedResult<PropertyListItem>
            {
                Items = visible.ToList(),
                Page = 1,
                PageSize = 20,
                TotalCount = visible.Count,
            });
        }

        public Task<PropertyResult> GetAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyResult.Success(new PropertyDetail { Id = id, OwnerId = caller.UserId }));

        public Task<PropertyResult> CreateAsync(
            PropertySave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> UpdateAsync(
            int id, PropertySave request, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> DeleteAsync(
            int id, PropertyCaller caller, CancellationToken ct = default)
        {
            if (!caller.IsChuTro)
            {
                throw new UnauthorizedAccessException("Ngoai pham vi.");
            }

            if (_failures.TryGetValue(id, out var failure) && failure is not null)
            {
                return Task.FromResult(PropertyResult.Failure(failure, failure));
            }

            DeletedIds.Add(id);
            _items.RemoveAll(item => item.Id == id);
            return Task.FromResult(PropertyResult.Success());
        }

        public Task<PagedResult<PropertyTrashItem>> ListDeletedAsync(
            PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PropertyResult> RestoreAsync(
            int id, PropertyCaller caller, CancellationToken ct = default) =>
            throw new NotSupportedException();
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

    /// <summary>Trả lại chính key làm văn bản, đủ để kiểm tra cấu trúc UI và thông báo lỗi.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
