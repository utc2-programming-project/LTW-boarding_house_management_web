using System.Net;
using System.Text.RegularExpressions;

namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>
/// thao tác form đăng nhập thật (GET lấy token rồi POST) cho các test bảo mật.
/// Không dùng DB demo; chỉ dùng fixture SQL test.
/// </summary>
internal static class LoginFormClient
{
    public const string LoginPath = "/Account/Login";

    public static async Task<(string Token, string? Handler)> GetFormAsync(
        HttpClient client,
        string path = LoginPath)
    {
        var response = await client.GetAsync($"{path}?returnUrl=%2F");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        var token = HiddenValue(html, "__RequestVerificationToken")
            ?? throw new InvalidOperationException("Không tìm thấy antiforgery token trên trang đăng nhập.");
        return (token, HiddenValue(html, "_handler"));
    }

    public static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string token,
        string? handler,
        string email,
        string password,
        string path = LoginPath)
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

        return client.PostAsync($"{path}?returnUrl=%2F", new FormUrlEncodedContent(fields));
    }

    public static async Task<HttpResponseMessage> PostFreshAsync(
        HttpClient client,
        string email,
        string password,
        string path = LoginPath)
    {
        var (token, handler) = await GetFormAsync(client, path);
        return await PostAsync(client, token, handler, email, password, path);
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
