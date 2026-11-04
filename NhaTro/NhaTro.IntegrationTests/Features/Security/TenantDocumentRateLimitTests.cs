using System.Net;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Storage;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// VHT-119: thao tác ghi giấy tờ dùng chung policy file-upload nên vượt ngưỡng phải trả 429
/// (hồi quy VHT-27 sau khi VHT-36 tách nhóm endpoint). Lớp riêng để không lấn bộ đếm với test khác.
/// </summary>
public sealed class TenantDocumentRateLimitTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Tai_giay_to_qua_nguong_tra_429()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.ChuTroAEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        // Ngưỡng mặc định 20 lần/phút; gửi dư để chắc chắn chạm giới hạn.
        var path = $"/api/tenants/{fixture.Ids.TenantAId}/documents";
        for (var attempt = 1; attempt <= 22; attempt++)
        {
            var response = await client.PostAsync(path, BuildUpload());
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.Contains(StorageErrorCodes.UploadFailed, body);
                return;
            }
        }

        Assert.Fail("Không nhận được 429 sau 22 lần tải giấy tờ liên tiếp.");
    }

    [WebAppFact]
    public async Task Doc_giay_to_khong_tieu_hao_quota_upload()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        // Đăng nhập bằng chính khách thuê của hồ sơ: dữ liệu seed không có ContractMembers nên
        // chủ trọ chỉ đạt quyền Own/Owner qua Tenant.UserId, còn đường Chủ trọ trả 404.
        var login = await LoginFormClient.PostFreshAsync(
            client, TestDataSeeder.KhachThueEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        // Xem/tải giấy tờ là thao tác đọc, không được tính vào bucket upload (20 lần/phút).
        // Vượt ngưỡng mà vẫn 200 chứng tỏ GET không dùng policy file-upload.
        var path = $"/api/tenants/{fixture.Ids.TenantAId}/documents";
        for (var attempt = 1; attempt <= 25; attempt++)
        {
            var response = await client.GetAsync(path);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // Nội dung không hợp lệ vẫn tới được limiter: rate limiting chạy trước handler nên 429
    // xuất hiện độc lập với việc tệp có được chấp nhận hay không.
    private static MultipartFormDataContent BuildUpload()
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("Other"), "DocumentType");
        content.Add(new ByteArrayContent([1, 2, 3]), "File", "ratelimit-probe.txt");
        return content;
    }
}
