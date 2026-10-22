using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;

namespace NhaTro.UnitTests.Features.Rooms;

// VHT-7 (dot 1C): test phan thuan (khong DB) cua diem goi policy cong tac va DTO chon phong.
// Hanh vi SQL that nam o NhaTro.IntegrationTests/Features/Properties.
public class RoomServiceRulesTests
{
    [Theory]
    [InlineData(Roles.ChuTro, RoomChangeDecision.Allowed)]
    [InlineData(Roles.QuanLy, RoomChangeDecision.RequiresApproval)]
    public async Task Policy_stub_theo_mac_dinh_duyet(
        string role, RoomChangeDecision expected)
    {
        var decision = await new RoomChangePolicyStub().EvaluateAsync(
            new RoomChangeRequest("u-1", role, 1, RoomChangeKind.ChangePrice));

        Assert.Equal(expected, decision);
    }

    [Fact]
    public void RoomOptionRequest_normalize_gia_tri_mac_dinh()
    {
        var request = new RoomOptionRequest
        {
            Keyword = "  P1  ",
            Limit = 0,
            ExcludeRoomIds = Enumerable.Range(1, 120).ToList(),
        };

        request.Normalize();

        Assert.Equal("P1", request.Keyword);
        Assert.Equal(20, request.Limit);
        Assert.Equal(100, request.ExcludeRoomIds!.Count);
    }

    [Fact]
    public void RoomOptionRequest_giu_limit_hop_le()
    {
        var request = new RoomOptionRequest { Limit = 50 };
        request.Normalize();
        Assert.Equal(50, request.Limit);
    }

    [Fact]
    public void PropertyCaller_phan_loai_vai_tro()
    {
        Assert.True(new PropertyCaller("u", Roles.ChuTro).IsManager);
        Assert.True(new PropertyCaller("u", Roles.QuanLy).IsManager);
        Assert.False(new PropertyCaller("u", Roles.KhachThue).IsManager);
        Assert.False(new PropertyCaller("u", Roles.Admin).IsManager);
        Assert.True(new PropertyCaller("u", Roles.Admin).IsAdmin);
    }
}
