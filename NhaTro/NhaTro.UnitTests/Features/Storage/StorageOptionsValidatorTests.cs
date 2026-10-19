using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Configuration.Validation;
using NhaTro.UnitTests.TestSupport;

namespace NhaTro.UnitTests.Features.Storage;

// VHT-13: cau hinh storage phai duoc kiem ngay khi khoi dong, dac biet la "ngoai wwwroot".
public class StorageOptionsValidatorTests
{
    private static StorageOptionsValidator ValidatorFor(string contentRoot, string webRoot) =>
        new(new FakeWebHostEnvironment { ContentRootPath = contentRoot, WebRootPath = webRoot });

    [Fact]
    public void Thieu_RootPath_thi_bao_loi()
    {
        var result = ValidatorFor(@"C:\app", @"C:\app\wwwroot")
            .Validate(null, new StorageOptions { RootPath = "  " });

        Assert.True(result.Failed);
    }

    [Fact]
    public void RootPath_trong_wwwroot_thi_bao_loi()
    {
        var result = ValidatorFor(@"C:\app", @"C:\app\wwwroot")
            .Validate(null, new StorageOptions { RootPath = @"wwwroot\uploads" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, message => message.Contains("wwwroot"));
    }

    [Fact]
    public void RootPath_ngoai_wwwroot_thi_hop_le()
    {
        var result = ValidatorFor(@"C:\app", @"C:\app\wwwroot")
            .Validate(null, new StorageOptions { RootPath = @"..\..\storage" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Gioi_han_khong_hop_le_thi_bao_loi()
    {
        var options = new StorageOptions { RootPath = @"C:\data\storage" };
        options.Limits["RoomPhoto"] = 0;
        options.MaxFilesPerEntity["RoomPhoto"] = -1;

        var result = ValidatorFor(@"C:\app", @"C:\app\wwwroot").Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(2, result.Failures!.Count());
    }

    [Fact]
    public void Resolve_RootPath_giu_duong_dan_tuyet_doi_va_ghep_content_root()
    {
        var absolute = new StorageOptions { RootPath = @"D:\data\storage" };
        Assert.Equal(@"D:\data\storage", absolute.ResolveRootPath(@"C:\app"));

        var relative = new StorageOptions { RootPath = @"..\..\storage" };
        Assert.Equal(@"C:\storage", relative.ResolveRootPath(@"C:\app\web"));
    }
}
