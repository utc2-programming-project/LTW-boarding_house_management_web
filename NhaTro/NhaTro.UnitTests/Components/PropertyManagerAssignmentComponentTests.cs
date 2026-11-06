using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Web.Common.Models;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.Components;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

/// <summary>
/// VHT-24 — panel "Phân công Quản lý" trong hộp thoại chi tiết nhà trọ (bUnit, không cần DB):
/// cấu trúc danh sách, luồng gán/bỏ gán và ánh xạ thông báo lỗi.
/// </summary>
public sealed class PropertyManagerAssignmentComponentTests : BunitContext
{
    private const int PropertyId = 1;
    private const string ManagerA = "manager-a";
    private const string ManagerB = "manager-b";

    private readonly FakePropertyService _properties = new();
    private readonly FakeManagerService _managers = new();

    public PropertyManagerAssignmentComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton(new PropertyPageService(
            new P2CallerProvider(new OwnerAuthenticationProvider()), _properties, _managers));
    }

    private IRenderedComponent<PropertyDetailsDialog> RenderDialog(bool isOwner = true) =>
        Render<PropertyDetailsDialog>(p => p
            .Add(c => c.Property, new PropertyListItem { Id = PropertyId, Name = "Trọ A", IsActive = true })
            .Add(c => c.IsOwner, isOwner));

    [Fact]
    public void Chu_tro_thay_danh_sach_phan_cong_va_chi_ung_vien_chua_phan_cong()
    {
        _managers.Assigned.Add(ManagerA);

        var cut = RenderDialog();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".p2-manager-row")));

        Assert.Contains(ManagerA, cut.Find(".p2-manager-email").TextContent);
        Assert.Equal(
            new[] { string.Empty, ManagerB },
            cut.FindAll("#p2-manager-candidate option").Select(option => option.GetAttribute("value")).ToArray());
    }

    [Fact]
    public void Nut_phan_cong_bi_khi_chua_chon_tai_khoan()
    {
        var cut = RenderDialog();
        cut.WaitForAssertion(() => cut.Find(".p2-manager-panel"));

        Assert.True(cut.Find(".p2-manager-assign button").HasAttribute("disabled"));
    }

    [Fact]
    public void Bam_phan_cong_goi_service_va_nap_lai_danh_sach()
    {
        var cut = RenderDialog();
        cut.WaitForAssertion(() => cut.Find("#p2-manager-candidate"));

        cut.Find("#p2-manager-candidate").Change(ManagerB);
        cut.Find(".p2-manager-assign button").Click();

        cut.WaitForAssertion(() => Assert.Equal(new[] { ManagerB }, _managers.AssignCalls.ToArray()));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".p2-manager-row")));
        // Quản lý vừa gán bị loại khỏi danh sách ứng viên; người chưa gán vẫn còn.
        Assert.Equal(
            new[] { string.Empty, ManagerA },
            cut.FindAll("#p2-manager-candidate option").Select(option => option.GetAttribute("value")).ToArray());
    }

    [Fact]
    public void Bam_bo_phan_cong_goi_revoke_va_an_dong_thong_bao_trong()
    {
        _managers.Assigned.Add(ManagerA);

        var cut = RenderDialog();
        cut.WaitForAssertion(() => cut.Find(".p2-manager-row button"));

        cut.Find(".p2-manager-row button").Click();

        cut.WaitForAssertion(() => Assert.Equal(new[] { ManagerA }, _managers.RevokeCalls.ToArray()));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".p2-manager-empty")));
    }

    [Fact]
    public void Loi_trung_phan_cong_hien_dung_thong_bao()
    {
        _managers.AssignFailure = PropertyManagerErrorCodes.AlreadyAssigned;

        var cut = RenderDialog();
        cut.WaitForAssertion(() => cut.Find("#p2-manager-candidate"));

        cut.Find("#p2-manager-candidate").Change(ManagerB);
        cut.Find(".p2-manager-assign button").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "P2ManagerAlreadyAssigned",
            cut.Find(".p2-manager-panel .p2-alert.error span").TextContent));
    }

    [Fact]
    public void Khong_phai_chu_tro_thi_khong_hien_panel_phan_cong()
    {
        var cut = RenderDialog(isOwner: false);
        cut.WaitForAssertion(() => cut.Find(".p2-details-actions"));

        Assert.Empty(cut.FindAll(".p2-manager-panel"));
    }

    private sealed class FakeManagerService : IPropertyManagerService
    {
        public HashSet<string> Assigned { get; } = [];

        public List<string> AssignCalls { get; } = [];

        public List<string> RevokeCalls { get; } = [];

        public string? AssignFailure { get; set; }

        public Task<PropertyManagerListResult> ListAssignmentsAsync(
            int propertyId, PropertyCaller caller, bool includeHistory = false, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerListResult.Success(
                Assigned.Select(id => Item(id)).ToList()));

        public Task<PropertyManagerResult> AssignAsync(
            int propertyId, string managerUserId, PropertyCaller caller, CancellationToken ct = default)
        {
            AssignCalls.Add(managerUserId);
            if (AssignFailure is { } failure)
            {
                return Task.FromResult(PropertyManagerResult.Failure(failure, failure));
            }

            Assigned.Add(managerUserId);
            return Task.FromResult(PropertyManagerResult.Success(Item(managerUserId)));
        }

        public Task<PropertyManagerResult> RevokeAsync(
            int propertyId, string managerUserId, PropertyCaller caller, CancellationToken ct = default)
        {
            RevokeCalls.Add(managerUserId);
            Assigned.Remove(managerUserId);
            return Task.FromResult(PropertyManagerResult.Success(Item(managerUserId)));
        }

        public Task<PropertyManagerCandidateResult> ListAssignableManagersAsync(
            int propertyId, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerCandidateResult.Success(
                new[] { ManagerA, ManagerB }
                    .Select(id => new ManagerCandidateDto(id, $"{id}@test.local", null, Assigned.Contains(id)))
                    .ToList()));

        public Task<PropertyManagerCandidateListResult> ListCandidatesAsync(
            int propertyId, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerCandidateListResult.Success(
                new[] { ManagerA, ManagerB }
                    .Where(id => !Assigned.Contains(id))
                    .Select(id => new PropertyManagerCandidate(id, $"{id}@test.local", null))
                    .ToList()));

        public Task<PropertyManagerListResult> ListAssignmentsForManagerAsync(
            string managerUserId, PropertyCaller caller, bool includeHistory = false, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerListResult.Success([]));

        public Task<PropertyManagerPropertyListResult> ListAssignablePropertiesAsync(
            string managerUserId, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyManagerPropertyListResult.Success([]));

        public Task<ManagerOwnerResult> GetOwnerForManagerAsync(
            PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(ManagerOwnerResult.Success(null));

        private static PropertyManagerItem Item(string managerUserId) =>
            new(PropertyId, managerUserId, $"{managerUserId}@test.local",
                new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc), "chu-tro-1", null, null);
    }

    private sealed class FakePropertyService : IPropertyService
    {
        public Task<PagedResult<PropertyListItem>> ListAsync(
            PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<PropertyListItem>());

        public Task<PropertyResult> GetAsync(int id, PropertyCaller caller, CancellationToken ct = default) =>
            Task.FromResult(PropertyResult.Success(new PropertyDetail
            {
                Id = id,
                Name = "Trọ A",
                Address = "1 Đường Test",
                OwnerId = caller.UserId,
                IsActive = true,
                CreatedAt = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            }));

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
