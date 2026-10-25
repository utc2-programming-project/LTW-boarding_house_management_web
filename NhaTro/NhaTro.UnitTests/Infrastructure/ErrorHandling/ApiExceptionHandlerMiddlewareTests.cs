using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Infrastructure.ErrorHandling;

namespace NhaTro.UnitTests.Infrastructure.ErrorHandling;

/// <summary>Lỗi /api trả JSON 500 an toàn, không lộ chi tiết exception.</summary>
public sealed class ApiExceptionHandlerMiddlewareTests
{
    [Fact]
    public async Task Tra_json_500_an_toan_khong_lo_chi_tiet()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddOptions().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();

        var middleware = new ApiExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("bi-mat-noi-bo"),
            NullLogger<ApiExceptionHandlerMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("UploadFailed", body);
        Assert.DoesNotContain("bi-mat-noi-bo", body);
    }
}
