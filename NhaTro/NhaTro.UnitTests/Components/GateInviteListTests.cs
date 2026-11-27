using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Web.Features.AccessControl.Components;
using Web.Features.AccessControl.DTOs;

namespace NhaTro.UnitTests.Components;

/// <summary>Danh sách liên kết khách tự điền: hiển thị đủ liên kết đang mở, không phân trang.</summary>
public sealed class GateInviteListTests : BunitContext
{
    public GateInviteListTests()
    {
        Services.AddLogging();
        Services.AddLocalization(o => o.ResourcesPath = "Resources");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Hien_thi_toan_bo_lien_ket_va_khong_co_thanh_phan_trang()
    {
        var cut = Render<GateInviteList>(p => p.Add(x => x.Invites, Invites(12)));

        Assert.Equal(12, cut.FindAll(".gate-invite-list li").Count);
        Assert.Empty(cut.FindAll(".table-pager"));
        Assert.Contains("Phòng 12", cut.FindAll(".gate-invite-list li").Last().TextContent);
    }

    [Fact]
    public void Khong_ve_gi_khi_khong_co_lien_ket()
    {
        var cut = Render<GateInviteList>(p => p.Add(x => x.Invites, []));

        Assert.Empty(cut.FindAll(".gate-form-card"));
    }

    private static GateInviteDto[] Invites(int count) =>
        Enumerable.Range(1, count).Select(i => new GateInviteDto(
            Id: i,
            PropertyId: 1,
            PropertyName: "Nhà A",
            RoomId: i,
            RoomCode: $"Phòng {i}",
            CreatedByUserId: "owner-1",
            CreatedByName: "Chủ trọ",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt: DateTimeOffset.UtcNow)).ToArray();
}
