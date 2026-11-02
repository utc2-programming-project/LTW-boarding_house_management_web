using System.Net;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Storage;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// VHT-119: endpoint giấy tờ dùng chung policy file-upload nên vượt ngưỡng phải trả 429
/// (hồi quy VHT-27 sau khi VHT-36 tách nhóm endpoint). Lớp riêng để không lấn bộ đếm với test khác.
/// </summary>
public sealed class TenantDocumentRateLimitTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Liet_ke_giay_to_qua_nguong_tra_429()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.ChuTroAEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        // Ngưỡng mặc định 20 lần/phút; gửi dư để chắc chắn chạm giới hạn.
        var path = $"/api/tenants/{fixture.Ids.TenantAId}/documents";
        for (var attempt = 1; attempt <= 22; attempt++)
        {
            var response = await client.GetAsync(path);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.Contains(StorageErrorCodes.UploadFailed, body);
                return;
            }
        }

        Assert.Fail("Không nhận được 429 sau 22 lần liệt kê giấy tờ liên tiếp.");
    }
}
