using System.Net;
using System.Text.RegularExpressions;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.TestReadiness;

/// <summary>
/// VHT-14 (đợt 1D): chạy thật HTTP/auth/UI trên factory SQL test.
/// Không dùng DB demo; tài khoản do <see cref="TestDataSeeder"/> gieo.
/// </summary>
public sealed class AuthFlowTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Trang_dang_nhap_tra_200_va_co_form()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"Input.Email\"", html);
        Assert.Contains("name=\"Input.Password\"", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    [WebAppFact]
    public async Task Dang_nhap_admin_that_thanh_cong_va_hien_avatar()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var (token, handler) = await GetLoginFormAsync(client);
        var response = await PostLoginAsync(
            client, token, handler, TestDataSeeder.AdminEmail, TestDataSeeder.Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.AbsolutePath);

        var home = await client.GetAsync("/");
        home.EnsureSuccessStatusCode();
        var html = await home.Content.ReadAsStringAsync();

        // AppTopbar chỉ render avatar/sign-out khi cookie đăng nhập hợp lệ.
        Assert.Contains("class=\"avatar\"", html);
        Assert.DoesNotContain("class=\"btn btn-secondary sign-in\"", html);
    }

    [WebAppFact]
    public async Task Sai_mat_khau_bi_tu_choi_va_hien_loi()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var (token, handler) = await GetLoginFormAsync(client);
        var response = await PostLoginAsync(
            client, token, handler, TestDataSeeder.AdminEmail, "SaiMatKhau123!");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid login attempt", html);
    }

    [WebAppFact]
    public async Task Trang_UI_demo_tra_200()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/dev/components");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<(string Token, string? Handler)> GetLoginFormAsync(HttpClient client)
    {
        var response = await client.GetAsync("/Account/Login?returnUrl=%2F");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var token = HiddenValue(html, "__RequestVerificationToken")
            ?? throw new InvalidOperationException("Không tìm thấy antiforgery token trên trang đăng nhập.");
        var handler = HiddenValue(html, "_handler");

        return (token, handler);
    }

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string token, string? handler, string email, string password)
    {
        var fields = new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = token,
        };

        if (!string.IsNullOrEmpty(handler))
        {
            fields["_handler"] = handler;
        }

        return client.PostAsync("/Account/Login?returnUrl=%2F", new FormUrlEncodedContent(fields));
    }

    private static string? HiddenValue(string html, string name)
    {
        foreach (Match match in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var tag = match.Value;
            if (!Regex.IsMatch(tag, $"name=\"{Regex.Escape(name)}\"", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var value = Regex.Match(tag, "value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
            if (value.Success)
            {
                return WebUtility.HtmlDecode(value.Groups[1].Value);
            }
        }

        return null;
    }
}
