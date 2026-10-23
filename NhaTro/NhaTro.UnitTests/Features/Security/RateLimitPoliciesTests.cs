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
    public void Khoa_theo_ip_that_cua_client()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");

        Assert.Equal("10.0.0.5", RateLimitPolicies.ClientKey(http));
    }

    [Fact]
    public void Sau_proxy_loopback_thi_lay_ip_cuoi_cua_x_forwarded_for()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Loopback;
        http.Request.Headers["X-Forwarded-For"] = "198.51.100.9, 203.0.113.7";

        Assert.Equal("203.0.113.7", RateLimitPolicies.ClientKey(http));
    }

    [Fact]
    public void Client_ngoai_khong_gia_duoc_x_forwarded_for()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        http.Request.Headers["X-Forwarded-For"] = "1.2.3.4";

        Assert.Equal("10.0.0.5", RateLimitPolicies.ClientKey(http));
    }

    [Fact]
    public void X_forwarded_for_sai_dinh_dang_thi_giu_ip_peer()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Loopback;
        http.Request.Headers["X-Forwarded-For"] = "khong-phai-ip";

        Assert.Equal(IPAddress.Loopback.ToString(), RateLimitPolicies.ClientKey(http));
    }

    private static DefaultHttpContext Post(string path)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = path;
        return http;
    }
}
