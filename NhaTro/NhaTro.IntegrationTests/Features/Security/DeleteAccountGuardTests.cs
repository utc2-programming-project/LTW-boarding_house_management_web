using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// VHT-27: Admin cuối cùng không được tự xoá tài khoản (trang xoá dữ liệu cá nhân).
/// Lớp test riêng để có app + bộ đếm rate limit mới.
/// </summary>
public sealed class DeleteAccountGuardTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private const string DeletePath = "/Account/Manage/DeletePersonalData";

    [WebAppFact]
    public async Task Admin_cuoi_cung_khong_tu_xoa_duoc()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.AdminEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var (token, handler) = await LoginFormClient.GetAnyFormAsync(client, DeletePath);
        var fields = new Dictionary<string, string>
        {
            ["Input.Password"] = TestDataSeeder.Password,
            ["__RequestVerificationToken"] = token,
        };
        if (!string.IsNullOrEmpty(handler))
        {
            fields["_handler"] = handler;
        }

        var response = await client.PostAsync(DeletePath, new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "last admin",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }
}
