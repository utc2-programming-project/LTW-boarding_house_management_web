using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Infrastructure.Security;

namespace NhaTro.UnitTests.Infrastructure.Security;

/// <summary>
/// PLAN-027: chặn POST cross-site nhưng không chặn nhầm POST same-origin.
/// Chromium gửi "Origin: null" khi trang đặt Referrer-Policy: no-referrer
/// (trang nhận lời mời /invite, đặt lại mật khẩu), nên phải cho đi tiếp.
/// </summary>
public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Post_tu_trang_no_referrer_voi_Origin_null_duoc_di_tiep()
    {
        var reached = false;
        var context = CreateContext("POST");
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        context.Request.Headers.Origin = "null";

        await InvokeAsync(context, () => reached = true);

        Assert.True(reached);
        Assert.NotEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Post_cross_site_khong_vao_toi_endpoint_va_nap_lai_trang_bang_get()
    {
        var reached = false;
        var context = CreateContext("POST");
        context.Request.Headers["Sec-Fetch-Site"] = "cross-site";

        await InvokeAsync(context, () => reached = true);

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/invite/token", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Post_voi_Origin_host_khac_khong_vao_toi_endpoint()
    {
        var reached = false;
        var context = CreateContext("POST");
        context.Request.Headers.Origin = "https://evil.example";

        await InvokeAsync(context, () => reached = true);

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
    }

    [Fact]
    public async Task Post_voi_Origin_cung_host_duoc_di_tiep()
    {
        var reached = false;
        var context = CreateContext("POST");
        context.Request.Headers.Origin = "https://localhost:7260";

        await InvokeAsync(context, () => reached = true);

        Assert.True(reached);
    }

    private static DefaultHttpContext CreateContext(string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/invite/token";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 7260);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Task InvokeAsync(HttpContext context, Action onNext)
    {
        var middleware = new SecurityHeadersMiddleware(
            _ =>
            {
                onNext();
                return Task.CompletedTask;
            },
            NullLogger<SecurityHeadersMiddleware>.Instance);

        return middleware.InvokeAsync(context);
    }
}
