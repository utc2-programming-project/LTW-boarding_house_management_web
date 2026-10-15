using System.ComponentModel.DataAnnotations;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Entities;
using Web.Features.Properties.Mapping;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Rooms.Mapping;

namespace NhaTro.UnitTests.Features.Properties;

// VHT-112 (dot 1B): chot DTO P2. Chi test validation/mapping, service SQL lam o 1C (VHT-7).
public class RoomPropertyDtoTests
{
    private static IList<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
        return results;
    }

    [Fact]
    public void PropertySave_thieu_ten_bao_loi()
    {
        var dto = new PropertySave { Name = "  " };
        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void PropertySave_ten_qua_200_bao_loi()
    {
        var dto = new PropertySave { Name = new string('a', 201) };
        Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(PropertySave.Name)));
    }

    [Fact]
    public void RoomSave_thieu_ma_hoac_gia_am_bao_loi()
    {
        Assert.NotEmpty(Validate(new RoomSave { PropertyId = 1, Code = " " }));
        Assert.NotEmpty(Validate(new RoomSave { PropertyId = 1, Code = "P101", StandardPrice = -1 }));
        Assert.NotEmpty(Validate(new RoomSave { PropertyId = 0, Code = "P101" }));
    }

    [Fact]
    public void RoomSave_hop_le_qua_validation()
    {
        var dto = new RoomSave { PropertyId = 1, Code = "P101", StandardPrice = 2500000 };
        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void Mapping_cat_khoang_trang_khi_luu()
    {
        var property = new Property();
        PropertyMapping.ApplySave(new PropertySave { Name = "  Tro An Binh  ", Address = " " }, property);
        Assert.Equal("Tro An Binh", property.Name);
        Assert.Null(property.Address);

        var room = new Room();
        RoomMapping.ApplySave(new RoomSave { PropertyId = 2, Code = "  P101  ", StandardPrice = 1000000 }, room);
        Assert.Equal("P101", room.Code);
        Assert.Equal(2, room.PropertyId);
    }

    [Fact]
    public void Filter_normalize_gia_tri_mac_dinh()
    {
        var pf = new PropertyFilter { Page = 0, PageSize = 500, Keyword = " " };
        pf.Normalize();
        Assert.Equal(1, pf.Page);
        Assert.Equal(20, pf.PageSize);
        Assert.Null(pf.Keyword);

        var rf = new RoomFilter { Page = -1, MinPrice = -5, Keyword = " P1 " };
        rf.Normalize();
        Assert.Equal(1, rf.Page);
        Assert.Null(rf.MinPrice);
        Assert.Equal("P1", rf.Keyword);
    }

    [Fact]
    public void RoomOption_du_truong_cho_form_hop_dong()
    {
        var room = new Room { Id = 7, PropertyId = 3, Code = "P301", StandardPrice = 3000000, Status = RoomStatus.Available };
        var option = RoomMapping.ToOption(room, "Tro An Binh");
        Assert.Equal(7, option.Id);
        Assert.Equal("P301", option.Code);
        Assert.Equal(RoomStatus.Available, option.Status);
    }
}
