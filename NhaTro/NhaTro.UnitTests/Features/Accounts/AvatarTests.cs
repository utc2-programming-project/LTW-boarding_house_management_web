using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.TimeProvider.Testing;
using Web.Data;
using Web.Endpoints.Avatars;
using Web.Features.Accounts.Avatar;

namespace NhaTro.UnitTests.Features.Accounts;

/// <summary>
/// PLAN-030: ảnh đại diện lưu ở server. Hai phần thuần logic — giải mã payload và kiểm tra/nạp ảnh.
/// </summary>
public class AvatarTests
{
    private const string UserA = "avatar-owner-a";

    private static byte[] Jpeg(int length = 32)
    {
        var data = new byte[Math.Max(length, 3)];
        data[0] = 0xFF;
        data[1] = 0xD8;
        data[2] = 0xFF;
        return data;
    }

    private static byte[] Png()
    {
        var data = new byte[16];
        byte[] magic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        magic.CopyTo(data);
        return data;
    }

    private static (ApplicationDbContext Db, FakeTimeProvider Clock, AvatarService Service) NewService()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 22, 7, 30, 0, TimeSpan.Zero));
        return (db, clock, new AvatarService(db, clock));
    }

    // ---- AvatarEndpoints.TryDecode ----

    [Fact]
    public void Data_url_base64_hop_le_dec_duoc_byte()
    {
        var payload = "data:image/jpeg;base64," + Convert.ToBase64String(Jpeg());

        var outcome = AvatarEndpoints.TryDecode(payload, out var data);

        Assert.Equal(AvatarPayloadOutcome.Ok, outcome);
        Assert.Equal(Jpeg(), data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://x/y")]
    [InlineData("data:application/pdf;base64,AAAA")]
    [InlineData("data:image/jpeg,AAAA")]
    [InlineData("data:image/jpeg;charset=utf-8,AAAA")]
    public void Payload_khong_phai_anh_thu_nho_bat_duoc_bi_tat(string? payload)
    {
        var outcome = AvatarEndpoints.TryDecode(payload, out var data);

        Assert.Equal(AvatarPayloadOutcome.Invalid, outcome);
        Assert.Null(data);
    }

    [Fact]
    public void Base64_khong_hop_le_bi_tat_thanh_invalid_khong_phai_500()
    {
        var outcome = AvatarEndpoints.TryDecode("data:image/jpeg;base64,!!!not-base64!!!", out var data);

        Assert.Equal(AvatarPayloadOutcome.Invalid, outcome);
        Assert.Null(data);
    }

    [Fact]
    public void Payload_dai_hon_tran_bi_chan_truoc_khi_decode()
    {
        var payload = "data:image/jpeg;base64," + new string('A', AvatarService.MaxBytes / 3 * 4 + 400);

        var outcome = AvatarEndpoints.TryDecode(payload, out var data);

        Assert.Equal(AvatarPayloadOutcome.TooLarge, outcome);
        Assert.Null(data);
    }

    // ---- AvatarService ----

    [Fact]
    public async Task Nhap_anh_hop_le_luu_loai_noi_dung_va_moc_thoi_gian_server()
    {
        var (db, clock, service) = NewService();

        var error = await service.SaveAsync(UserA, Png());

        Assert.Null(error);
        var avatar = await db.UserAvatars.SingleAsync();
        Assert.Equal(UserA, avatar.UserId);
        Assert.Equal("image/png", avatar.ContentType);
        Assert.Equal(clock.GetUtcNow(), avatar.UpdatedAt);
    }

    [Fact]
    public async Task Noi_dung_khong_phai_anh_bi_tu_choi_va_khong_luu_dong_nao()
    {
        var (db, _, service) = NewService();
        var pdf = new byte[64];
        byte[] magic = [0x25, 0x50, 0x44, 0x46];
        magic.CopyTo(pdf);

        var error = await service.SaveAsync(UserA, pdf);

        Assert.Equal(AvatarError.InvalidType, error);
        Assert.Empty(db.UserAvatars);
    }

    [Fact]
    public async Task Noi_dung_khong_bat_dau_bang_magic_anh_bi_tu_choi_InvalidType()
    {
        var (db, _, service) = NewService();
        var raw = new byte[AvatarService.MaxBytes + 1];

        var error = await service.SaveAsync(UserA, raw);

        Assert.Equal(AvatarError.InvalidType, error);
        Assert.Empty(db.UserAvatars);
    }

    [Fact]
    public async Task Anh_nop_qua_tran_byte_tra_ve_413()
    {
        var (_, _, service) = NewService();
        var big = Jpeg(AvatarService.MaxBytes + 1);

        var error = await service.SaveAsync(UserA, big);

        Assert.Equal(AvatarError.TooLarge, error);
    }

    [Fact]
    public async Task Nhap_lan_hai_upsert_khong_tao_dong_thu_hai()
    {
        var (db, clock, service) = NewService();
        await service.SaveAsync(UserA, Jpeg());

        clock.SetUtcNow(new DateTimeOffset(2026, 11, 23, 1, 0, 0, TimeSpan.Zero));
        var second = Png();
        await service.SaveAsync(UserA, second);

        var avatar = await db.UserAvatars.SingleAsync();
        Assert.Equal(second, avatar.Data);
        Assert.Equal("image/png", avatar.ContentType);
        Assert.Equal(clock.GetUtcNow(), avatar.UpdatedAt);
    }

    [Fact]
    public async Task GetInfo_tra_ve_loai_va_moc_thoi_gian_khi_co_anh()
    {
        var (db, clock, service) = NewService();

        Assert.Null(await service.GetInfoAsync(UserA));

        await service.SaveAsync(UserA, Jpeg());
        var info = await service.GetInfoAsync(UserA);

        Assert.NotNull(info);
        Assert.Equal("image/jpeg", info!.Item1);
        Assert.Equal(clock.GetUtcNow(), info.Item2);
    }

    [Fact]
    public async Task Go_anh_xoa_dong_va_go_anh_moi_lan_khong_bi_lech()
    {
        var (db, _, service) = NewService();
        await service.SaveAsync(UserA, Jpeg());

        await service.RemoveAsync(UserA);
        await service.RemoveAsync("khong-ton-tai");

        Assert.Empty(db.UserAvatars);
        Assert.Null(await service.GetInfoAsync(UserA));
    }
}
