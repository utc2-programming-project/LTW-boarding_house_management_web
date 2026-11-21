using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts.Avatar;

namespace NhaTro.IntegrationTests.Features.Accounts;

/// <summary>
/// PLAN-030: bảng UserAvatars trên SQL Server thật — nạp/đọc ảnh và cascade khi xoá tài khoản.
/// </summary>
public sealed class AvatarPersistenceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private const string User = "avatar-it-1";

    private static byte[] Png()
    {
        var data = new byte[24];
        byte[] magic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        magic.CopyTo(data);
        return data;
    }

    private static async Task AddUserAsync(ApplicationDbContext db, string userId)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@test.local",
            Email = $"{userId}@test.local",
            EmailConfirmed = true,
            ApprovedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [SqlServerFact]
    public async Task Anh_hop_le_luu_va_doc_lai_dung_loai_noi_dung()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await AddUserAsync(db, User);
        var service = new AvatarService(db);
        var png = Png();

        Assert.Null(await service.SaveAsync(User, png));

        var avatar = await db.UserAvatars.SingleAsync();
        Assert.Equal(png, avatar.Data);
        Assert.Equal("image/png", avatar.ContentType);

        var info = await service.GetInfoAsync(User);
        Assert.Equal("image/png", info!.Value.Item1);

        var loaded = await service.FindAsync(User);
        Assert.Equal(png, loaded!.Data);
    }

    [SqlServerFact]
    public async Task Nhap_anh_moi_ghi_de_len_dong_cu_khong_tao_them_dong()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await AddUserAsync(db, User);
        var service = new AvatarService(db);
        await service.SaveAsync(User, Png());

        var jpeg = new byte[24];
        byte[] magic = [0xFF, 0xD8, 0xFF];
        magic.CopyTo(jpeg);
        await service.SaveAsync(User, jpeg);

        Assert.Single(db.UserAvatars);
        var avatar = await db.UserAvatars.SingleAsync();
        Assert.Equal(jpeg, avatar.Data);
        Assert.Equal("image/jpeg", avatar.ContentType);
    }

    [SqlServerFact]
    public async Task Xoa_tai_khoan_thi_anh_mat_theo_khong_sot_blob_mo_coi()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await AddUserAsync(db, User);
        var service = new AvatarService(db);
        await service.SaveAsync(User, Png());
        var user = await db.Users.SingleAsync(item => item.Id == User);

        db.Users.Remove(user);
        await db.SaveChangesAsync();

        Assert.Empty(db.UserAvatars);
        Assert.Null(await service.FindAsync(User));
    }

    [SqlServerFact]
    public async Task Tai_khoan_khong_co_anh_thi_Find_va_GetInfo_cung_tra_ve_null()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new AvatarService(db);

        Assert.Null(await service.FindAsync("avatar-khong-co"));
        Assert.Null(await service.GetInfoAsync("avatar-khong-co"));
    }
}
