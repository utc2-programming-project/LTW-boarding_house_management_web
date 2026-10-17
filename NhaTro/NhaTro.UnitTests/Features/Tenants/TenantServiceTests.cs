using System.ComponentModel.DataAnnotations;
using Web.Features.Accounts;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Entities;
using Web.Features.Tenants.Enums;
using Web.Features.Tenants.Mapping;

namespace NhaTro.UnitTests.Features.Tenants;

// VHT-23 (dot 1C): chot DTO + service khach thue. Test phan thuan (chuan hoa/che/mapping/validation);
// query SQL, authz, concurrency va vi pham unique index chay o integration test (SQL Server that).
public class TenantServiceTests
{
    private static IList<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
        return results;
    }

    [Theory]
    [InlineData("  Nguyễn   Văn   An ", "Nguyễn Văn An")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Chuan_hoa_ho_ten_gop_khoang_trang(string? input, string expected) =>
        Assert.Equal(expected, TenantNormalizer.NormalizeFullName(input));

    [Theory]
    [InlineData("0901 234-567", "0901234567")]
    [InlineData("090.123.4567", "0901234567")]
    [InlineData("+84 912 345 678", "0912345678")]
    [InlineData("0084 912 345 678", "0912345678")]
    [InlineData("84912345678", "0912345678")]
    [InlineData("+84", "0")]
    [InlineData("abc", "")]
    [InlineData("", "")]
    public void Chuan_hoa_dien_thoai_bo_ky_tu_va_doi_84(string input, string expected) =>
        Assert.Equal(expected, TenantNormalizer.NormalizePhone(input));

    [Theory]
    [InlineData("0912345678", true)]
    [InlineData("0292123456", true)]
    [InlineData("090", false)]
    [InlineData("0", false)]
    [InlineData("012345678901", false)]
    [InlineData("", false)]
    public void Kiem_tra_dien_thoai_vn_sau_chuan_hoa(string phone, bool expected) =>
        Assert.Equal(expected, TenantNormalizer.IsValidVietnamesePhone(phone));

    [Theory]
    [InlineData("079 201-004821", "079201004821")]
    [InlineData("b1234567", "B1234567")]
    [InlineData("B1234567", "B1234567")]
    [InlineData(null, "")]
    public void Chuan_hoa_so_giay_to_giu_chu_so_ascii_va_in_hoa(string? input, string expected) =>
        Assert.Equal(expected, TenantNormalizer.NormalizeIdNumber(input));

    [Theory]
    [InlineData("079201004821", "********4821")]
    [InlineData("1234", "********")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void Che_so_giay_to_chi_giu_4_ky_tu_cuoi(string? input, string? expected) =>
        Assert.Equal(expected, TenantNormalizer.MaskIdNumber(input));

    [Fact]
    public void Mapping_tao_ho_so_chuan_hoa_va_khong_de_lo_so_day_du_o_list()
    {
        var dto = new CreateTenantRequest
        {
            FullName = "  Nguyễn Văn An ",
            PhoneNumber = "+84 901 234 567",
            IdNumber = "079 201 004821",
        };
        dto.Normalize();

        var entity = new Tenant();
        TenantMapping.ApplyCreate(dto, entity);

        Assert.Equal("Nguyễn Văn An", entity.FullName);
        Assert.Equal("0901234567", entity.Phone);
        Assert.Equal("079201004821", entity.CitizenId);
        Assert.True(entity.IsActive);

        entity.Id = 5;
        var listItem = TenantMapping.ToListItem(entity);
        Assert.Equal(5, listItem.Id);
        Assert.Equal("********4821", listItem.IdNumberMasked);
        Assert.False(listItem.HasAccount);
        Assert.False(listItem.HasActiveContract);
    }

    [Fact]
    public void Mapping_chi_tra_note_cho_quan_ly()
    {
        var entity = new Tenant
        {
            Id = 9,
            FullName = "Trần Thị B",
            Phone = "0912345678",
            Note = "Nội bộ",
            CreatedAt = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc),
        };

        var managerView = TenantMapping.ToDetail(entity, includeNote: true);
        Assert.Equal("Nội bộ", managerView.Note);
        Assert.Equal(DateTimeKind.Utc, managerView.CreatedAt.UtcDateTime.Kind);
        Assert.Null(managerView.UpdatedAt);

        var tenantView = TenantMapping.ToDetail(entity, includeNote: false);
        Assert.Null(tenantView.Note);
    }

    [Fact]
    public void Mapping_coi_thoi_gian_khong_co_kind_la_utc()
    {
        var entity = new Tenant
        {
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            UpdatedAt = new DateTime(2026, 6, 7, 8, 9, 10, DateTimeKind.Unspecified),
        };

        var detail = TenantMapping.ToDetail(entity, includeNote: false);
        Assert.Equal(DateTimeKind.Utc, detail.CreatedAt.UtcDateTime.Kind);
        Assert.NotNull(detail.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, detail.UpdatedAt!.Value.UtcDateTime.Kind);
    }

    [Fact]
    public void Mapping_khach_tu_sua_chi_dung_3_truong()
    {
        var entity = new Tenant { FullName = "Giữ nguyên", CitizenId = "079200000001", Note = "Giữ nguyên" };
        var dto = new UpdateOwnProfileRequest
        {
            PhoneNumber = "090 999 8888",
            Email = " b@example.com ",
            PermanentAddress = " 12 Lê Lợi ",
        };
        dto.Normalize();

        TenantMapping.ApplyOwnUpdate(dto, entity);

        Assert.Equal("0909998888", entity.Phone);
        Assert.Equal("b@example.com", entity.Email);
        Assert.Equal("12 Lê Lợi", entity.PermanentAddress);
        Assert.Equal("Giữ nguyên", entity.FullName);
        Assert.Equal("079200000001", entity.CitizenId);
    }

    [Fact]
    public void Mapping_cap_nhat_ghi_de_null_cac_truong_khong_gui()
    {
        var entity = new Tenant { Email = "cu@example.com", CitizenId = "079200000001", Note = "cũ" };
        var dto = new UpdateTenantRequest { FullName = "An", PhoneNumber = "0901234567", RowVersion = "AQID" };
        dto.Normalize();

        TenantMapping.ApplyUpdate(dto, entity);

        Assert.Null(entity.Email);
        Assert.Null(entity.CitizenId);
        Assert.Null(entity.Note);
    }

    [Fact]
    public void Filter_va_picker_normalize_kep_gia_tri_bien()
    {
        var filter = new TenantFilterRequest { Page = 0, PageSize = 500, Keyword = "  an  " };
        filter.Normalize();
        Assert.Equal(1, filter.Page);
        Assert.Equal(100, filter.PageSize);
        Assert.Equal("an", filter.Keyword);
        Assert.Equal(TenantSortBy.FullName, filter.SortBy);
        Assert.Equal(SortDirection.Asc, filter.SortDirection);

        var low = new TenantFilterRequest { PageSize = 0 };
        low.Normalize();
        Assert.Equal(20, low.PageSize);

        var exact = new TenantFilterRequest { PageSize = 100 };
        exact.Normalize();
        Assert.Equal(100, exact.PageSize);

        var picker = new TenantPickerRequest { Limit = 999, Keyword = " " };
        picker.Normalize();
        Assert.Equal(50, picker.Limit);
        Assert.Null(picker.Keyword);
    }

    [Fact]
    public void Picker_gioi_han_so_khach_loai_tru()
    {
        var picker = new TenantPickerRequest
        {
            ExcludeTenantIds = Enumerable.Range(1, 250).ToList(),
        };
        picker.Normalize();
        Assert.Equal(100, picker.ExcludeTenantIds!.Count);
    }

    [Fact]
    public void Normalize_xoa_so_giay_to_rong_thanh_null()
    {
        var create = new CreateTenantRequest { FullName = "An", PhoneNumber = "0901234567", IdNumber = "   " };
        create.Normalize();
        Assert.Null(create.IdNumber);

        var update = new UpdateTenantRequest { FullName = "An", PhoneNumber = "0901234567", RowVersion = "AQID" };
        update.Normalize();
        Assert.Null(update.IdNumber);
    }

    [Fact]
    public void Dto_bat_buoc_truong_toi_thieu()
    {
        Assert.NotEmpty(Validate(new CreateTenantRequest { FullName = " ", PhoneNumber = "" }));
        Assert.NotEmpty(Validate(new UpdateTenantRequest { FullName = "An", PhoneNumber = "090", RowVersion = "" }));
        Assert.NotEmpty(Validate(new UpdateOwnProfileRequest { PhoneNumber = "090", RowVersion = "" }));
        Assert.Empty(Validate(new UpdateTenantRequest { FullName = "An", PhoneNumber = "090", RowVersion = "AQID" }));
    }

    [Fact]
    public void Dto_chan_email_sai_dinh_dang()
    {
        var result = Validate(new CreateTenantRequest
        {
            FullName = "An",
            PhoneNumber = "0901",
            Email = "khong-phai-email",
        });

        Assert.Contains(result, r => r.MemberNames.Contains(nameof(CreateTenantRequest.Email)));
    }

    [Fact]
    public void TenantCaller_phan_biet_vai_tro()
    {
        Assert.True(new TenantCaller("u1", Roles.ChuTro).IsManager);
        Assert.True(new TenantCaller("u1", Roles.QuanLy).IsManager);
        Assert.False(new TenantCaller("u1", Roles.QuanLy).IsAdmin);
        Assert.True(new TenantCaller("u1", Roles.Admin).IsAdmin);
        Assert.True(new TenantCaller("u1", Roles.KhachThue).IsTenant);
    }
}
