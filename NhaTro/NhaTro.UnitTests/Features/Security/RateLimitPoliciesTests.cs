using System.Net;
using Microsoft.AspNetCore.Http;
using Web.Infrastructure.Security;

namespace NhaTro.UnitTests.Features.Security;

/// <summary>
/// VHT-27: khoá phân vùng và nhận diện request đăng nhập là hàm thuần, test không cần app/DB.
/// </summary>
public class RateLimitPoliciesTests
{
    [Theory]
    [InlineData("/login")]
    [InlineData("/Account/Login")]
    [InlineData("/account/login")]
    [InlineData("/Account/Login/")]
    [InlineData("/Account/LoginWith2fa")]
    [InlineData("/Account/LoginWithRecoveryCode/")]
    public void Nhan_dien_moi_duong_dang_nhap(string path)
    {
        var http = Post(path);

        Assert.True(RateLimitPolicies.IsLoginRequest(http));
    }

    [Theory]
    [InlineData("GET", "/Account/Login")]
    [InlineData("POST", "/Account/Logout")]
    [InlineData("POST", "/api/files/")]
    [InlineData("POST", "/Account/Manage/DownloadPersonalData")]
    public void Khong_chan_nham_request_khac(string method, string path)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Path = path;

        Assert.False(RateLimitPolicies.IsLoginRequest(http));
    }

    [Fact]
    public void ClientKey_lay_ip_va_chi_tin_X_forwarded_for_tu_loopback()
    {
        Assert.Equal("10.0.0.5", ClientKey("10.0.0.5"));

        // Proxy cùng máy (IIS): lấy IP cuối của X-Forwarded-For.
        Assert.Equal("203.0.113.7",
            ClientKey("127.0.0.1", "198.51.100.9, 203.0.113.7"));

        // Client ngoài không giả được X-Forwarded-For.
        Assert.Equal("10.0.0.5", ClientKey("10.0.0.5", "1.2.3.4"));

        // Header sai định dạng thì giữ IP peer.
        Assert.Equal("127.0.0.1", ClientKey("127.0.0.1", "khong-phai-ip"));
    }

    private static string ClientKey(string remoteIp, string? forwardedFor = null)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        if (forwardedFor is not null)
        {
            http.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }

        return RateLimitPolicies.ClientKey(http);
    }

    private static DefaultHttpContext Post(string path)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = path;
        return http;
    }
}
