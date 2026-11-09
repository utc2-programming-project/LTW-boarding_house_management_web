using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Enums;

namespace NhaTro.UnitTests.Features.Properties;

/// <summary>PLAN-010: nháp thiết lập nhà trọ — roundtrip, TTL, version, key theo user.</summary>
public class PropertySetupDraftStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Roundtrip_giu_du_lieu_va_phong_dang_nhap()
    {
        var draft = new PropertySetupDraft
        {
            PropertyId = 42,
            Step = PropertySetupStep.Rooms,
            Name = "Nhà trọ Bình Minh",
            Address = "12 Nguyễn Trãi, Quận 5",
            IsActive = true,
            UpdatedAt = Now,
            PendingRoom = new RoomSave
            {
                PropertyId = 42,
                Code = "P101",
                Floor = 1,
                AreaM2 = 20.5m,
                StandardPrice = 2_500_000m,
                Status = RoomStatus.Available,
                IsActive = true
            }
        };

        var decoded = PropertySetupDraftCodec.Decode(PropertySetupDraftCodec.Encode(draft), Now);

        Assert.NotNull(decoded);
        Assert.Equal(42, decoded.PropertyId);
        Assert.Equal(PropertySetupStep.Rooms, decoded.Step);
        Assert.Equal("Nhà trọ Bình Minh", decoded.Name);
        Assert.Equal("P101", decoded.PendingRoom?.Code);
        Assert.Equal(2_500_000m, decoded.PendingRoom?.StandardPrice);
    }

    [Fact]
    public void Nhap_qua_30_ngay_bi_loai()
    {
        var draft = new PropertySetupDraft { Name = "Cũ", UpdatedAt = Now.AddDays(-31) };

        var decoded = PropertySetupDraftCodec.Decode(PropertySetupDraftCodec.Encode(draft), Now);

        Assert.Null(decoded);
    }

    [Fact]
    public void Nhap_trong_30_ngay_van_dung_duoc()
    {
        var draft = new PropertySetupDraft { Name = "Mới", UpdatedAt = Now.AddDays(-29) };

        var decoded = PropertySetupDraftCodec.Decode(PropertySetupDraftCodec.Encode(draft), Now);

        Assert.NotNull(decoded);
        Assert.Equal("Mới", decoded.Name);
    }

    [Fact]
    public void Version_khac_bi_loai()
    {
        var draft = new PropertySetupDraft { Name = "Sai version", Version = 99, UpdatedAt = Now };

        var decoded = PropertySetupDraftCodec.Decode(PropertySetupDraftCodec.Encode(draft), Now);

        Assert.Null(decoded);
    }

    [Fact]
    public void Key_tach_theo_user()
    {
        Assert.NotEqual(
            PropertySetupDraftStore.BuildKey("user-a"),
            PropertySetupDraftStore.BuildKey("user-b"));
        Assert.StartsWith("nhatro.property-setup.v1.", PropertySetupDraftStore.BuildKey("user-a"));
    }
}
