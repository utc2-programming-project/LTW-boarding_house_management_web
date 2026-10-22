using Microsoft.Extensions.Logging.Abstractions;
using Web.Features.Settings;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Rooms;
using Web.Features.Accounts;

namespace NhaTro.UnitTests.Features.Settings;

/// <summary>
/// VHT-94 — tiêu chí "test đạt 3 chế độ × 16 thao tác" và các ràng buộc của ma trận:
/// SW-15 chỉ Cho phép/Không thể, Chủ trọ luôn thực thi ngay, mã lạ bị từ chối.
/// Test thuần, không cần SQL: policy đọc qua <see cref="IManagerToggleReader"/>.
/// </summary>
public class ManagerTogglePolicyTests
{
    private static ManagerTogglePolicy NewPolicy(ManagerToggleMode mode) =>
        new(new StubReader(mode), NullLogger<ManagerTogglePolicy>.Instance);

    private sealed class StubReader(ManagerToggleMode mode) : IManagerToggleReader
    {
        public Task<ManagerToggleMode> GetModeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(mode);
    }

    public static TheoryData<ManagerAction, ManagerToggleMode, RoomChangeDecision> Combos()
    {
        var data = new TheoryData<ManagerAction, ManagerToggleMode, RoomChangeDecision>();
        foreach (var action in Enum.GetValues<ManagerAction>())
        {
            data.Add(action, ManagerToggleMode.Allow, RoomChangeDecision.Allowed);
            data.Add(action, ManagerToggleMode.Approve, RoomChangeDecision.RequiresApproval);
            data.Add(action, ManagerToggleMode.Deny, RoomChangeDecision.Denied);
        }

        return data;
    }

    /// <summary>3 chế độ × 16 thao tác = 48 tổ hợp, đúng bảng quy đổi của VHT-94.</summary>
    [Theory]
    [MemberData(nameof(Combos))]
    public async Task Ba_che_do_nhan_16_thao_tac(
        ManagerAction action, ManagerToggleMode mode, RoomChangeDecision expected)
    {
        var decision = await NewPolicy(mode).EvaluateAsync(action, Roles.QuanLy);

        Assert.Equal(expected, decision);
    }

    [Fact]
    public void Danh_muc_co_16_cong_tac_chinh_va_5_cong_tac_phu()
    {
        Assert.Equal(16, Enum.GetValues<ManagerAction>().Length);

        var main = ManagerToggleCatalog.All.Where(d => !d.IsSubSwitch).ToList();
        var sub = ManagerToggleCatalog.All.Where(d => d.IsSubSwitch).ToList();

        Assert.Equal(16, main.Count);
        Assert.Equal(5, sub.Count);
        Assert.Equal(
            ["SW-01", "SW-02", "SW-03", "SW-04", "SW-05", "SW-06", "SW-07", "SW-08",
             "SW-09", "SW-10", "SW-11", "SW-12", "SW-13", "SW-14", "SW-15", "SW-16"],
            main.Select(d => d.Code));
        Assert.Equal(
            ["SW-15a", "SW-15b", "SW-15c", "SW-15d", "SW-15e"],
            sub.Select(d => d.Code));
    }

    [Theory]
    [InlineData("SW-01", ManagerToggleMode.Approve)]
    [InlineData("SW-02", ManagerToggleMode.Approve)]
    [InlineData("SW-03", ManagerToggleMode.Approve)]
    [InlineData("SW-04", ManagerToggleMode.Approve)]
    [InlineData("SW-05", ManagerToggleMode.Approve)]
    [InlineData("SW-06", ManagerToggleMode.Approve)]
    [InlineData("SW-07", ManagerToggleMode.Approve)]
    [InlineData("SW-08", ManagerToggleMode.Approve)]
    [InlineData("SW-09", ManagerToggleMode.Approve)]
    [InlineData("SW-10", ManagerToggleMode.Approve)]
    [InlineData("SW-11", ManagerToggleMode.Approve)]
    [InlineData("SW-12", ManagerToggleMode.Approve)]
    [InlineData("SW-13", ManagerToggleMode.Approve)]
    [InlineData("SW-14", ManagerToggleMode.Approve)]
    [InlineData("SW-15", ManagerToggleMode.Allow)]
    [InlineData("SW-16", ManagerToggleMode.Approve)]
    [InlineData("SW-15a", ManagerToggleMode.Allow)]
    [InlineData("SW-15b", ManagerToggleMode.Allow)]
    [InlineData("SW-15c", ManagerToggleMode.Allow)]
    [InlineData("SW-15d", ManagerToggleMode.Allow)]
    [InlineData("SW-15e", ManagerToggleMode.Allow)]
    public void Mac_dinh_dung_ma_tran(string code, ManagerToggleMode expected)
    {
        Assert.Equal(expected, ManagerToggleCatalog.DefaultModeOf(code));
    }

    [Theory]
    [InlineData("SW-15")]
    [InlineData("SW-15a")]
    [InlineData("SW-15b")]
    [InlineData("SW-15c")]
    [InlineData("SW-15d")]
    [InlineData("SW-15e")]
    public void Nhom_bao_cao_chi_cho_phep_hoac_khong_the(string code)
    {
        Assert.True(ManagerToggleCatalog.IsAllowed(code, ManagerToggleMode.Allow));
        Assert.False(ManagerToggleCatalog.IsAllowed(code, ManagerToggleMode.Approve));
        Assert.True(ManagerToggleCatalog.IsAllowed(code, ManagerToggleMode.Deny));
    }

    [Theory]
    [InlineData(ManagerToggleMode.Allow)]
    [InlineData(ManagerToggleMode.Approve)]
    [InlineData(ManagerToggleMode.Deny)]
    public async Task Chu_tro_luon_duoc_phep_du_cong_tac_khong_the(ManagerToggleMode mode)
    {
        foreach (var action in Enum.GetValues<ManagerAction>())
        {
            var decision = await NewPolicy(mode).EvaluateAsync(action, Roles.ChuTro);

            Assert.Equal(RoomChangeDecision.Allowed, decision);
        }
    }

    [Fact]
    public async Task Ma_cong_tac_la_thi_tu_choi_chu_khong_cho_phep()
    {
        var decision = await NewPolicy(ManagerToggleMode.Allow).EvaluateAsync("SW-99", Roles.QuanLy);

        Assert.Equal(RoomChangeDecision.Denied, decision);
    }

    [Theory]
    [InlineData(RoomChangeKind.EditInfo, "SW-01")]
    [InlineData(RoomChangeKind.ChangePrice, "SW-02")]
    [InlineData(RoomChangeKind.ChangeStatus, "SW-03")]
    public void Sua_phong_dung_cong_tac_theo_loai_thay_doi(RoomChangeKind kind, string expectedCode)
    {
        Assert.Equal(expectedCode, ManagerToggleCatalog.ToCode(ManagerTogglePolicy.ToAction(kind)));
    }

    [Fact]
    public async Task Diem_goi_cu_cua_P2_van_theo_cong_tac()
    {
        var policy = NewPolicy(ManagerToggleMode.Deny);

        var decision = await policy.EvaluateAsync(
            new RoomChangeRequest("quan-ly-1", Roles.QuanLy, 10, RoomChangeKind.ChangePrice));

        Assert.Equal(RoomChangeDecision.Denied, decision);
    }

    [Theory]
    [InlineData(Roles.ChuTro, true)]
    [InlineData(Roles.QuanLy, false)]
    [InlineData(Roles.Admin, false)]
    [InlineData(Roles.KhachThue, false)]
    public void Chi_chu_tro_duoc_cau_hinh_cong_tac(string role, bool expected)
    {
        var caller = new ManagerToggleCaller("u-1", role);

        Assert.Equal(expected, caller.CanConfigure);
    }

    [Fact]
    public void Ma_cong_tac_duoc_chuan_hoa()
    {
        Assert.True(ManagerToggleCatalog.TryFind("sw 02", out var first));
        Assert.Equal("SW-02", first.Code);
        Assert.True(ManagerToggleCatalog.TryFind("SW_15a", out var second));
        Assert.Equal("SW-15a", second.Code);
        Assert.False(ManagerToggleCatalog.TryFind("SW-17", out _));
    }
}
