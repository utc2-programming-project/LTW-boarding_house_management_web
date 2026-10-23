using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>Antiforgery và health endpoint chạy thật qua HTTP trên DB test.</summary>
public sealed class SecurityEndpointTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Health_live_tra_200_va_chi_tra_trang_thai()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [WebAppFact]
    public async Task Health_ready_tra_200_khi_db_san_sang()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [WebAppFact]
    public async Task Post_dang_nhap_thieu_antiforgery_tra_400()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = TestDataSeeder.AdminEmail,
                ["Input.Password"] = TestDataSeeder.Password,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [WebAppFact]
    public async Task Culture_set_thieu_antiforgery_tra_400()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsync(
            "/culture/set?culture=en-US",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [WebAppFact]
    public async Task Upload_thieu_antiforgery_tra_400_sau_khi_dang_nhap()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.ChuTroAEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var content = new MultipartFormDataContent
        {
            { new StringContent("RoomPhoto"), "fileCategory" },
            { new StringContent("Room"), "relatedEntityType" },
            { new StringContent(fixture.Ids.RoomAId.ToString()), "relatedEntityId" },
        };

        var response = await client.PostAsync("/api/files/", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
