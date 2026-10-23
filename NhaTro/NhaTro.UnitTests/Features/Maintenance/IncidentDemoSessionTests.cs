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
    [InlineData(Roles.ChuTro, 3)]
    [InlineData(Roles.QuanLy, 2)]
    [InlineData(Roles.KhachThue, 1)]
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
        Assert.Single(await session.ListAsync());

        form.RoomId = 101;
        Assert.False(await session.CreateAsync(form));
        Assert.Equal(2, (await session.ListAsync()).Count);
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
        var item = await session.DetailAsync(101);
        Assert.Single(item.Photos);
        Assert.Equal(2, Assert.Single(item.Pending).Photos.Count);
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
        Assert.Equal(3, (await other.ListAsync()).Count);
        Assert.Equal(4, (await session.ListAsync()).Count);

        authentication.Principal = User(Roles.Admin, Roles.ChuTro);
        await Assert.ThrowsAsync<IncidentDemoException>(() => session.ListAsync());
        authentication.Principal = UserWithId("another-user", Roles.ChuTro);
        Assert.Equal("SessionChanged", (await Assert.ThrowsAsync<IncidentDemoException>(() => session.ListAsync())).Message);
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
