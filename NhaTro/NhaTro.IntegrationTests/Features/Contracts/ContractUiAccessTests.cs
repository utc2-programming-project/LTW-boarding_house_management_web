using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Contracts;

/// <summary>
/// VHT-129 (đợt 2E) — chạy lại trên commit ghép tuần 2:
/// Admin bị chặn ở route P2–P4 (nhà/phòng, khách thuê, hợp đồng) và giấy tờ;
/// Chủ trọ vào được hợp đồng nhưng không vào được trang giấy tờ (chỉ Khách thuê).
/// Kiểm ở tầng HTTP/UI thật bằng cookie đăng nhập, không chỉ unit test policy.
/// </summary>
public sealed class ContractUiAccessTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private static readonly string[] AdminBlockedPaths =
    [
        "/properties",
        "/rooms",
        "/tenants",
        "/contracts",
        "/contracts/create",
        "/my/documents",
    ];

    [WebAppFact]
    public async Task Admin_bi_chan_UI_P2_P4_tren_ban_ghep()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.AdminEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        foreach (var path in AdminBlockedPaths)
        {
            var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(
                "/Account/AccessDenied",
                response.Headers.Location?.OriginalString,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [WebAppFact]
    public async Task ChuTro_vao_hop_dong_nhung_khong_vao_giay_to()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.ChuTroAEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        foreach (var path in new[] { "/contracts", "/contracts/create" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var documents = await client.GetAsync("/my/documents");
        Assert.Equal(HttpStatusCode.Redirect, documents.StatusCode);
        Assert.Contains(
            "/Account/AccessDenied",
            documents.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }
}
