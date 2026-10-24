using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Features.Accounts;
using Web.Features.Maintenance;
using Web.Features.Maintenance.Models;

namespace NhaTro.UnitTests.Features.Maintenance;

// Kiểm các ranh giới quyền và tác dụng phụ của UI mẫu; không dùng để nghiệm thu backend P7.
public sealed class IncidentDemoSessionTests
{
    [Theory]
    [InlineData(Roles.ChuTro, 4)]
    [InlineData(Roles.QuanLy, 3)]
    [InlineData(Roles.KhachThue, 2)]
    public async Task List_and_detail_enforce_the_same_room_scope(string role, int expectedCount)
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(role)), TimeProvider.System);
        await session.InitializeAsync();

        Assert.Equal(expectedCount, (await session.ListAsync()).Count);
        if (role != Roles.ChuTro)
            Assert.Equal("NotFound", (await Assert.ThrowsAsync<IncidentDemoException>(() => session.DetailAsync(103))).Message);
        if (role == Roles.KhachThue)
            Assert.Equal("NotFound", (await Assert.ThrowsAsync<IncidentDemoException>(() => session.DetailAsync(102))).Message);
    }

    [Fact]
    public async Task Admin_is_denied_even_with_an_additional_business_role()
    {
        var principal = User(Roles.Admin, Roles.ChuTro);
        Assert.False(IncidentDemoSession.IsAllowed(principal));
        var session = new IncidentDemoSession(new DemoAuthentication(principal), TimeProvider.System);

        Assert.Equal("AccessDenied", (await Assert.ThrowsAsync<IncidentDemoException>(session.InitializeAsync)).Message);
    }

    [Fact]
    public async Task Approval_keeps_current_status_and_notes_and_prevents_duplicate_update_requests()
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.QuanLy)), TimeProvider.System);
        await session.InitializeAsync();
        var before = await session.DetailAsync(101);

        Assert.True(await session.UpdateAsync(101, IncidentDemoStatus.InProgress, "Đề xuất kiểm tra"));

        var after = await session.DetailAsync(101);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.TechnicalNotes, after.TechnicalNotes);
        var request = Assert.Single(after.Pending);
        Assert.Equal(IncidentDemoStatus.InProgress, request.ProposedStatus);
        Assert.Equal("Đề xuất kiểm tra", request.Notes);
        Assert.Equal("AlreadyPending", (await Assert.ThrowsAsync<IncidentDemoException>(
            () => session.UpdateAsync(101, IncidentDemoStatus.InProgress, "Gửi lại"))).Message);
        Assert.Single((await session.DetailAsync(101)).Pending);
    }

    [Fact]
    public async Task Denied_and_invalid_transitions_do_not_change_the_report()
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.QuanLy)), TimeProvider.System);
        await session.InitializeAsync();
        var before = await session.DetailAsync(101);
        await session.SetModeAsync(IncidentDemoAction.Update, IncidentDemoMode.Deny);
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.UpdateAsync(101, IncidentDemoStatus.InProgress, "Không được ghi"));
        Assert.Equal(before, await session.DetailAsync(101));

        await session.SetModeAsync(IncidentDemoAction.Update, IncidentDemoMode.Allow);
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.UpdateAsync(101, IncidentDemoStatus.Completed, "Bỏ bước"));
        Assert.Equal(before, await session.DetailAsync(101));
        Assert.False(await session.UpdateAsync(101, IncidentDemoStatus.InProgress, "Đã tiếp nhận"));
        Assert.False(await session.UpdateAsync(101, IncidentDemoStatus.Completed, "Đã xử lý"));
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.UpdateAsync(101, IncidentDemoStatus.InProgress, "Mở lại"));
        Assert.Equal(IncidentDemoStatus.Completed, (await session.DetailAsync(101)).Status);
    }

    [Fact]
    public async Task Tenant_cannot_create_for_another_room_or_update_a_report()
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.KhachThue)), TimeProvider.System);
        await session.InitializeAsync();
        var form = Form(102);
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.CreateAsync(form));
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.UpdateAsync(101, IncidentDemoStatus.InProgress, ""));
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.AddResultPhotosAsync(101, [Photo()]));
        Assert.Equal(2, (await session.ListAsync()).Count);

        form.RoomId = 101;
        Assert.False(await session.CreateAsync(form));
        Assert.Equal(3, (await session.ListAsync()).Count);
    }

    [Fact]
    public async Task Pending_creation_is_not_an_accepted_report_and_cannot_be_processed()
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.QuanLy)), TimeProvider.System);
        await session.InitializeAsync();
        Assert.True(await session.CreateAsync(Form(101)));
        var pending = Assert.Single((await session.ListAsync()).Where(i => i.AwaitingCreation));
        Assert.Equal(IncidentDemoAction.Create, Assert.Single(pending.Pending).Action);
        await session.SetModeAsync(IncidentDemoAction.Update, IncidentDemoMode.Allow);
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.UpdateAsync(pending.Id, IncidentDemoStatus.InProgress, ""));
        Assert.True((await session.DetailAsync(pending.Id)).AwaitingCreation);
    }

    [Fact]
    public async Task Photo_limit_includes_pending_photos_without_applying_them_to_the_report()
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.QuanLy)), TimeProvider.System);
        await session.InitializeAsync();
        Assert.True(await session.AddResultPhotosAsync(101, [Photo(), Photo()]));
        Assert.True(await session.AddResultPhotosAsync(101, [Photo()]));
        var item = await session.DetailAsync(101);
        Assert.Single(item.Photos);
        Assert.Equal(3, item.Pending.Sum(p => p.Photos.Count));
        Assert.Equal("PhotoLimit", (await Assert.ThrowsAsync<IncidentDemoException>(() => session.AddResultPhotosAsync(101, [Photo()]))).Message);
        Assert.Single((await session.DetailAsync(101)).Photos);
    }

    [Fact]
    public async Task Identity_is_checked_again_for_each_action_and_new_sessions_do_not_share_changes()
    {
        var authentication = new DemoAuthentication(User(Roles.ChuTro));
        var session = new IncidentDemoSession(authentication, TimeProvider.System);
        await session.InitializeAsync();
        await session.CreateAsync(Form(101));

        var other = new IncidentDemoSession(new DemoAuthentication(User(Roles.ChuTro)), TimeProvider.System);
        await other.InitializeAsync();
        Assert.Equal(4, (await other.ListAsync()).Count);
        Assert.Equal(5, (await session.ListAsync()).Count);

        authentication.Principal = User(Roles.Admin, Roles.ChuTro);
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.ListAsync());
        authentication.Principal = UserWithId("another-user", Roles.ChuTro);
        Assert.Equal("SessionChanged", (await Assert.ThrowsAsync<IncidentDemoException>(() => session.ListAsync())).Message);
    }

    [Fact]
    public async Task Tenant_has_a_completed_sample_with_history_but_no_internal_data()
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.KhachThue)), TimeProvider.System);
        await session.InitializeAsync();

        var item = Assert.Single(await session.ListAsync(), i => i.Status == IncidentDemoStatus.Completed);
        Assert.Equal(101, item.RoomId);
        Assert.Equal("EventCompleted", item.History[^1].EventKey);
        var detail = await session.DetailAsync(item.Id);
        Assert.Equal("", detail.TechnicalNotes);
        Assert.Empty(detail.Pending);
    }

    [Fact]
    public async Task Three_report_photos_and_three_result_photos_have_independent_limits()
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.ChuTro)), TimeProvider.System);
        await session.InitializeAsync();
        var form = Form(101);
        form.Photos = [Photo(), Photo(), Photo(), Photo()];
        Assert.Equal("PhotoLimit", (await Assert.ThrowsAsync<IncidentDemoException>(() => session.CreateAsync(form))).Message);
        Assert.Equal(4, (await session.ListAsync()).Count);

        form.Photos = [Photo(), Photo(), Photo()];
        Assert.False(await session.CreateAsync(form));
        var item = Assert.Single(await session.ListAsync(), i => !i.IsSeeded);
        Assert.False(await session.AddResultPhotosAsync(item.Id, [Photo(), Photo()]));
        Assert.False(await session.AddResultPhotosAsync(item.Id, [Photo()]));
        var full = await session.DetailAsync(item.Id);
        Assert.Equal(3, full.Photos.Count(p => !p.IsResult));
        Assert.Equal(3, full.Photos.Count(p => p.IsResult));
        Assert.Equal("PhotoLimit", (await Assert.ThrowsAsync<IncidentDemoException>(
            () => session.AddResultPhotosAsync(item.Id, [Photo()]))).Message);
        Assert.Equal(full, await session.DetailAsync(item.Id));
    }

    [Theory]
    [InlineData(IncidentDemoAction.Create, true)]
    [InlineData(IncidentDemoAction.Create, false)]
    [InlineData(IncidentDemoAction.Update, true)]
    [InlineData(IncidentDemoAction.Update, false)]
    [InlineData(IncidentDemoAction.ResultPhoto, true)]
    [InlineData(IncidentDemoAction.ResultPhoto, false)]
    public async Task Landlord_decides_each_sample_request_once(IncidentDemoAction action, bool approve)
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.ChuTro)), TimeProvider.System);
        await session.InitializeAsync();
        await session.AddApprovalSamplesAsync();
        Assert.Equal(7, (await session.ListAsync()).Count);
        var item = Assert.Single(await session.ListAsync(), i => i.Pending.Any(p => p.Action == action));
        var request = Assert.Single(item.Pending);

        await session.DecideAsync(item.Id, request.Id, approve);

        if (action == IncidentDemoAction.Create && !approve)
        {
            Assert.Equal("NotFound", (await Assert.ThrowsAsync<IncidentDemoException>(() => session.DetailAsync(item.Id))).Message);
        }
        else
        {
            var after = await session.DetailAsync(item.Id);
            Assert.Empty(after.Pending);
            Assert.False(after.AwaitingCreation);
            Assert.Equal(approve && action == IncidentDemoAction.Update ? IncidentDemoStatus.InProgress : IncidentDemoStatus.New, after.Status);
            Assert.Equal(approve && action == IncidentDemoAction.ResultPhoto ? 1 : 0, after.Photos.Count);
            Assert.Contains(after.History, h => h.EventKey == (approve ? "EventApproved" : "EventRejected"));
            Assert.Equal("RequestNotFound", (await Assert.ThrowsAsync<IncidentDemoException>(
                () => session.DecideAsync(item.Id, request.Id, !approve))).Message);
        }
        Assert.Equal("ApprovalSamplesAdded", (await Assert.ThrowsAsync<IncidentDemoException>(session.AddApprovalSamplesAsync)).Message);
    }

    [Theory]
    [InlineData(Roles.QuanLy)]
    [InlineData(Roles.KhachThue)]
    public async Task Non_landlords_cannot_seed_or_decide_requests(string role)
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(role)), TimeProvider.System);
        await session.InitializeAsync();
        Assert.Equal("AccessDenied", (await Assert.ThrowsAsync<IncidentDemoException>(session.AddApprovalSamplesAsync)).Message);
        Assert.Equal("AccessDenied", (await Assert.ThrowsAsync<IncidentDemoException>(
            () => session.DecideAsync(101, Guid.NewGuid(), true))).Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Proposed_assignment_and_cost_only_apply_after_approval_and_remain_private(bool approve)
    {
        var authentication = new DemoAuthentication(User(Roles.QuanLy));
        var session = new IncidentDemoSession(authentication, TimeProvider.System);
        await session.InitializeAsync();
        Assert.True(await session.UpdateAsync(101, IncidentDemoStatus.InProgress, "Internal note", "  Demo technician  ", 150000));
        var before = await session.DetailAsync(101);
        Assert.Equal("", before.Assignee);
        Assert.Null(before.RepairCost);
        var request = Assert.Single(before.Pending);
        Assert.Equal("Demo technician", request.Assignee);
        Assert.Equal(150000m, request.RepairCost);

        authentication.Principal = User(Roles.ChuTro);
        await session.DecideAsync(101, request.Id, approve);
        var after = await session.DetailAsync(101);
        Assert.Equal(approve ? "Demo technician" : "", after.Assignee);
        Assert.Equal(approve ? (decimal?)150000m : null, after.RepairCost);
        Assert.Equal(approve ? "Internal note" : "", after.TechnicalNotes);

        authentication.Principal = User(Roles.KhachThue);
        var tenant = await session.DetailAsync(101);
        Assert.Equal("", tenant.Assignee);
        Assert.Null(tenant.RepairCost);
        Assert.Equal("", tenant.TechnicalNotes);
        Assert.Empty(tenant.Pending);
    }

    [Fact]
    public async Task Allow_applies_assignment_and_cost_and_invalid_values_leave_report_unchanged()
    {
        var session = new IncidentDemoSession(new DemoAuthentication(User(Roles.QuanLy)), TimeProvider.System);
        await session.InitializeAsync();
        await session.SetModeAsync(IncidentDemoAction.Update, IncidentDemoMode.Allow);
        var before = await session.DetailAsync(101);
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.UpdateAsync(101, IncidentDemoStatus.InProgress, "", new string('x', 101), 0));
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.UpdateAsync(101, IncidentDemoStatus.InProgress, "", "", -1));
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.UpdateAsync(101, IncidentDemoStatus.InProgress, "", "", 1.5m));
        Assert.Equal(before, await session.DetailAsync(101));
        Assert.False(await session.UpdateAsync(101, IncidentDemoStatus.InProgress, "", " Tech ", 0));
        var after = await session.DetailAsync(101);
        Assert.Equal("Tech", after.Assignee);
        Assert.Equal(0m, after.RepairCost);
    }

    [Fact]
    public async Task Approving_creation_preserves_report_photos_and_allows_processing()
    {
        var authentication = new DemoAuthentication(User(Roles.QuanLy));
        var session = new IncidentDemoSession(authentication, TimeProvider.System);
        await session.InitializeAsync();
        var form = Form(101);
        form.Photos = [Photo(), Photo(), Photo()];
        Assert.True(await session.CreateAsync(form));
        var item = Assert.Single(await session.ListAsync(), i => i.AwaitingCreation);
        Assert.Empty(item.Photos);
        authentication.Principal = User(Roles.ChuTro);
        await session.DecideAsync(item.Id, Assert.Single(item.Pending).Id, true);
        var accepted = await session.DetailAsync(item.Id);
        Assert.False(accepted.AwaitingCreation);
        Assert.Equal(3, accepted.Photos.Count(p => !p.IsResult));
        Assert.Empty(accepted.Pending);
        Assert.False(await session.UpdateAsync(item.Id, IncidentDemoStatus.InProgress, "", "Tech", 200000));
        Assert.False(await session.AddResultPhotosAsync(item.Id, [Photo(), Photo(), Photo()]));
        Assert.Equal(6, (await session.DetailAsync(item.Id)).Photos.Count);
    }

    [Fact]
    public async Task Rejecting_pending_photos_releases_slots_without_changing_existing_photos()
    {
        var authentication = new DemoAuthentication(User(Roles.QuanLy));
        var session = new IncidentDemoSession(authentication, TimeProvider.System);
        await session.InitializeAsync();
        await session.AddResultPhotosAsync(101, [Photo(), Photo(), Photo()]);
        var before = await session.DetailAsync(101);
        authentication.Principal = User(Roles.ChuTro);
        await session.DecideAsync(101, Assert.Single(before.Pending).Id, false);
        Assert.Single((await session.DetailAsync(101)).Photos);
        authentication.Principal = User(Roles.QuanLy);
        Assert.True(await session.AddResultPhotosAsync(101, [Photo(), Photo(), Photo()]));
    }

    private static IncidentDemoForm Form(int roomId) => new()
    {
        PropertyId = 1, RoomId = roomId, Title = "Phiếu kiểm tra", Description = "Mô tả sự cố đủ độ dài để gửi."
    };

    private static IncidentDemoPhoto Photo() => new("result.png", Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a5QAAAABJRU5ErkJggg=="));

    private static ClaimsPrincipal User(params string[] roles) => UserWithId("demo-user", roles);
    private static ClaimsPrincipal UserWithId(string id, params string[] roles) => new(new ClaimsIdentity(
        roles.Select(role => new Claim(ClaimTypes.Role, role)).Prepend(new(ClaimTypes.NameIdentifier, id)), "DemoTest"));

    private sealed class DemoAuthentication(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public ClaimsPrincipal Principal { get; set; } = principal;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(Principal));
    }
}
