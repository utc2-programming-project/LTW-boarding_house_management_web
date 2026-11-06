using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Web.Features.Contracts;
using Web.Features.Contracts.Components;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Pages;
using Web.Features.Contracts.Services;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Tenants;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Features.Accounts;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

// Trang duyệt yêu cầu gia hạn (VHT-125/2E): hàng đợi chỉ thấy yêu cầu trong phạm vi,
// từ chối phải có lý do và chấp thuận gửi đúng số tháng khách xin xuống service.
public sealed class ContractRenewalQueueComponentTests : BunitContext
{
    public ContractRenewalQueueComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<IStringLocalizer<ContractResources>>(new KeyContractLocalizer());
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeySharedLocalizer());
        Services.AddSingleton(new Web.Components.Shared.Feedback.ToastService());
    }

    [Fact]
    public void Hang_doi_hien_dung_yeu_cau_trong_pham_vi()
    {
        var fake = new FakeRenewalService();
        var cut = RenderQueue(fake, RenewalRequestListResult.Success([PendingRequest()]));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".renewal-table tbody tr")));
        var row = cut.Find(".renewal-table tbody tr");
        Assert.Contains("HD-TEST-1", row.TextContent);
        Assert.Contains("P101", row.TextContent);
        Assert.Contains("RenewalMonthsValue", row.TextContent);
        Assert.Contains("Nguyễn Văn A", row.TextContent);
        Assert.NotNull(cut.Find(".renewal-row-actions button"));
    }

    [Fact]
    public void Khong_co_yeu_cau_thi_hien_trang_thai_trong()
    {
        var cut = RenderQueue(new FakeRenewalService(), RenewalRequestListResult.Success([]));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".renewal-table tbody tr")));
        Assert.Contains("RenewalQueueEmptyTitle", cut.Markup);
    }

    [Fact]
    public void Mo_hop_thoai_va_chap_thuan_gui_dung_yeu_cau()
    {
        var fake = new FakeRenewalService();
        var cut = RenderQueue(fake, RenewalRequestListResult.Success([PendingRequest()]));

        cut.WaitForAssertion(() => cut.Find(".renewal-row-actions button").Click());

        var accept = cut.Find("#renewal-decision-title");
        Assert.Equal("RenewalDecisionTitle", accept.TextContent);

        cut.Find("footer .btn-primary").Click();

        cut.WaitForAssertion(() =>
        {
            var decision = Assert.Single(fake.Decisions);
            Assert.True(decision.IsApproved);
            Assert.Null(decision.Reason);
        });
    }

    [Fact]
    public void Tu_choi_khong_ly_do_bi_chan_truoc_khi_goi_service()
    {
        var fake = new FakeRenewalService();
        var cut = RenderQueue(fake, RenewalRequestListResult.Success([PendingRequest()]));

        cut.WaitForAssertion(() => cut.Find(".renewal-row-actions button").Click());
        cut.Find("footer .btn-danger").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("RenewalErrorRejectReasonRequired", cut.Markup);
            Assert.Empty(fake.Decisions);
        });
    }

    [Fact]
    public void Tu_choi_co_ly_do_gui_ly_do_da_nhap()
    {
        var fake = new FakeRenewalService();
        var cut = RenderQueue(fake, RenewalRequestListResult.Success([PendingRequest()]));

        cut.WaitForAssertion(() => cut.Find(".renewal-row-actions button").Click());
        cut.Find("#renewal-reject-reason").Change("  Phòng này sẽ sửa  ");
        cut.Find("footer .btn-danger").Click();

        cut.WaitForAssertion(() =>
        {
            var decision = Assert.Single(fake.Decisions);
            Assert.False(decision.IsApproved);
            Assert.Equal("  Phòng này sẽ sửa  ", decision.Reason);
        });
    }

    [Fact]
    public void Hop_thoai_duyet_co_nut_dong_goc_phai()
    {
        var closeCalls = 0;
        var cut = Render<ContractRenewalDecisionModal>(parameters => parameters
            .Add(p => p.Visible, true)
            .Add(p => p.Request, PendingRequest())
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closeCalls++)));

        var close = cut.Find(".contract-modal-header .icon-button");

        Assert.Equal("ContractCloseAction", close.GetAttribute("aria-label"));
        close.Click();

        cut.WaitForAssertion(() => Assert.Equal(1, closeCalls));
    }

    private IRenderedComponent<ContractRenewalQueueView> RenderQueue(
        FakeRenewalService fake, RenewalRequestListResult listResult)
    {
        fake.ListResult = listResult;

        var auth = new FixedAuthenticationProvider("chu-tro-1", Roles.ChuTro);
        Services.AddSingleton(new ContractRenewalPageService(new P2CallerProvider(auth), fake));

        return Render<ContractRenewalQueueView>();
    }

    private static RenewalRequestItemDto PendingRequest() => new()
    {
        Id = Guid.NewGuid(),
        ContractId = Guid.NewGuid(),
        ContractNumber = "HD-TEST-1",
        RoomCode = "P101",
        PropertyName = "Trọ A",
        RequesterName = "Nguyễn Văn A",
        RequestedMonths = 6,
        Note = "Cho em gia hạn thêm",
        RequestedAt = new DateTime(2026, 11, 4, 3, 30, 0),
        CurrentEndDate = new DateTime(2027, 10, 21),
    };

    private sealed class FakeRenewalService : IContractRenewalService
    {
        public RenewalRequestListResult ListResult = RenewalRequestListResult.Success([]);

        public List<RenewalDecisionRequest> Decisions { get; } = [];

        public Task<RenewalRequestListResult> ListPendingAsync(
            PropertyCaller caller, CancellationToken cancellationToken = default) =>
            Task.FromResult(ListResult);

        public Task<ContractRenewalResult> DecideAsync(
            Guid requestId,
            RenewalDecisionRequest decision,
            PropertyCaller caller,
            CancellationToken cancellationToken = default)
        {
            Decisions.Add(decision);
            return Task.FromResult(ContractRenewalResult.Success(new ContractRenewalRequestDto
            {
                Id = requestId,
                ContractId = Guid.Empty,
                RequestedMonths = decision.IsApproved ? 6 : 1,
                Status = decision.IsApproved ? RenewalRequestStatus.Approved : RenewalRequestStatus.Rejected,
                RequestedAt = new DateTime(2026, 11, 4, 3, 30, 0),
            }));
        }

        public Task<ContractRenewalResult> SubmitAsync(
            TenantCaller caller,
            Guid contractId,
            int requestedMonths,
            string? note,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RenewalHistoryResult> ListForContractAsync(
            TenantCaller caller,
            Guid contractId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedAuthenticationProvider(string userId, string role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    /// <summary>Trả lại chính key làm văn bản, đủ để kiểm tra cấu trúc UI.</summary>
    private sealed class KeyContractLocalizer : IStringLocalizer<ContractResources>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class KeySharedLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
