using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.TestReadiness;

/// <summary>
/// VHT-47 đợt 3A — rà menu/route với service: policy trang phải khớp kỳ vọng 4 vai trò.
/// Kiểm bằng HTTP + cookie đăng nhập thật, không chỉ đọc attribute của trang.
/// </summary>
public sealed class Vht47UiAccessTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private static readonly string[] BusinessPaths =
    [
        "/contracts",
        "/contracts/create",
        "/invoices",
        "/incidents",
    ];

    [WebAppFact]
    public async Task Admin_bi_chan_ca_bon_route_P4_P6_P7()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);
        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.AdminEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        foreach (var path in BusinessPaths)
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
    public async Task ChuTro_vao_duoc_bon_route_P4_P6_P7()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);
        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.ChuTroAEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        foreach (var path in BusinessPaths)
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [WebAppFact]
    public async Task QuanLy_vao_duoc_bon_route_P4_P6_P7()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);
        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.QuanLyEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        foreach (var path in BusinessPaths)
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [WebAppFact]
    public async Task KhachThue_bi_chan_route_tao_hop_dong_nhung_vao_duoc_route_xem()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);
        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.KhachThueEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        foreach (var path in new[] { "/contracts", "/invoices", "/incidents" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var create = await client.GetAsync("/contracts/create");
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        Assert.Contains(
            "/Account/AccessDenied",
            create.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }
}
