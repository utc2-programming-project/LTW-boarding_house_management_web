using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Web.Features.AccessControl.Components;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Enums;

namespace NhaTro.UnitTests.Components;

/// <summary>Bảng phiếu ra vào: phân trang 10 dòng, không hiện pager khi rỗng.</summary>
public sealed class GatePassListTests : BunitContext
{
    public GatePassListTests()
    {
        Services.AddLogging();
        Services.AddLocalization(o => o.ResourcesPath = "Resources");
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Hien_10_dong_va_chuyen_trang_cuoi()
    {
        WithVietNamCulture(() =>
        {
            var cut = Render<GatePassList>(p => p.Add(x => x.Passes, Passes(23)));

            Assert.Equal(10, cut.FindAll("tbody tr").Count);
            Assert.Equal("Hiển thị 1–10 / 23", cut.Find(".table-pager__summary").TextContent.Trim());

            PageButton(cut, 3).Click();

            Assert.Equal(3, cut.FindAll("tbody tr").Count);
            Assert.Contains("Khách 21", cut.Find("tbody tr").TextContent);
            Assert.Equal("Hiển thị 21–23 / 23", cut.Find(".table-pager__summary").TextContent.Trim());
        });
    }

    [Fact]
    public void Danh_sach_rong_khong_hien_pager()
    {
        var cut = Render<GatePassList>(p => p.Add(x => x.Passes, Passes(0)));

        Assert.Empty(cut.FindAll(".table-pager"));
    }

    private static void WithVietNamCulture(Action test)
    {
        var originalUi = CultureInfo.CurrentUICulture;
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentUICulture = new CultureInfo("vi-VN");
        CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
        try
        {
            test();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
            CultureInfo.CurrentCulture = original;
        }
    }

    private static IElement PageButton(IRenderedComponent<GatePassList> cut, int page) =>
        cut.FindAll(".table-pager__pages button").Single(button => button.TextContent.Trim() == page.ToString());

    private static GatePassDto[] Passes(int count) =>
        Enumerable.Range(1, count).Select(i => new GatePassDto(
            Id: i,
            PropertyId: 1,
            PropertyName: "Nhà A",
            RoomId: 1,
            RoomCode: "A101",
            VisitorName: $"Khách {i}",
            VisitorPhone: null,
            PassType: GatePassType.Guest,
            StayType: GateStayType.Visit,
            Status: GatePassStatus.Pending,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            MaxUses: 1,
            UsedCount: 0,
            IssuerRole: GateIssuerRole.ChuTro,
            IssuedByUserId: "owner-1",
            AutoApproved: false,
            RejectedReason: null,
            RevokedReason: null,
            CreatedAt: DateTimeOffset.UtcNow,
            GatePassInviteId: null)).ToArray();
}
