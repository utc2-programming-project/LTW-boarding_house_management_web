using System.Globalization;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Infrastructure.Email;

namespace NhaTro.UnitTests.Features.Email;

/// <summary>
/// Dữ liệu mẫu dùng email không tồn tại (admin@admin.com...): thư phải được chuyển về
/// hộp thư thật để không mất, nhưng vẫn ghi rõ người nhận gốc.
/// </summary>
public sealed class EmailRecipientResolverTests
{
    private static EmailRecipientResolver Create(EmailOptions options) =>
        new(Options.Create(options));

    [Fact]
    public void Danh_sach_chuyen_huong_duoc_uu_tien_hon_che_do_demo()
    {
        var resolver = Create(new EmailOptions
        {
            DemoMode = true,
            FallbackRecipient = "demo@st.utc2.edu.vn",
            RecipientOverrides = new Dictionary<string, string>
            {
                ["admin@admin.com"] = "rieng@st.utc2.edu.vn",
            },
        });

        var recipient = resolver.Resolve("admin@admin.com");

        Assert.Equal("rieng@st.utc2.edu.vn", recipient.Address);
        Assert.Equal("admin@admin.com", recipient.OriginalAddress);
        Assert.True(recipient.Redirected);
    }

    [Fact]
    public void Che_do_demo_chuyen_moi_dia_chi_ve_hop_thu_that()
    {
        var resolver = Create(new EmailOptions
        {
            DemoMode = true,
            FallbackRecipient = "demo@st.utc2.edu.vn",
        });

        var recipient = resolver.Resolve("nguoidung@khongtontai.local");

        Assert.Equal("demo@st.utc2.edu.vn", recipient.Address);
        Assert.Equal("nguoidung@khongtontai.local", recipient.OriginalAddress);
        Assert.True(recipient.Redirected);
    }

    [Fact]
    public void Tat_che_do_demo_thi_giu_nguyen_dia_chi_goc()
    {
        var resolver = Create(new EmailOptions
        {
            DemoMode = false,
            FallbackRecipient = "demo@st.utc2.edu.vn",
        });

        var recipient = resolver.Resolve("that@gmail.com");

        Assert.Equal("that@gmail.com", recipient.Address);
        Assert.False(recipient.Redirected);
    }

    [Fact]
    public void So_khop_danh_sach_khong_phan_biet_hoa_thuong_va_khoang_trang()
    {
        var resolver = Create(new EmailOptions
        {
            RecipientOverrides = new Dictionary<string, string>
            {
                ["Admin@Admin.com"] = "demo@st.utc2.edu.vn",
            },
        });

        var recipient = resolver.Resolve("  admin@admin.com ");

        Assert.Equal("demo@st.utc2.edu.vn", recipient.Address);
        Assert.Equal("admin@admin.com", recipient.OriginalAddress);
    }

    [Fact]
    public void Dia_chi_rong_thi_khong_bi_doi_sang_hop_thu_demo()
    {
        var resolver = Create(new EmailOptions
        {
            DemoMode = true,
            FallbackRecipient = "demo@st.utc2.edu.vn",
        });

        var recipient = resolver.Resolve(null);

        Assert.Equal(string.Empty, recipient.Address);
        Assert.False(recipient.Redirected);
    }
}

public sealed class EmailTemplateTests
{
    [Fact]
    public void Thu_dat_lai_mat_khau_co_link_va_ghi_ro_nguoi_nhan_goc()
    {
        using var culture = new CultureSwitch("vi-VN");
        var recipient = new EmailRecipient("demo@st.utc2.edu.vn", "admin@admin.com");
        const string link = "https://localhost/Account/ResetPassword?code=abc";

        var content = EmailTemplates.PasswordReset("Nguyễn Văn A", "admin@admin.com", recipient, link);

        Assert.StartsWith("[Demo]", content.Subject);
        Assert.Contains("admin@admin.com", content.Subject);
        Assert.Contains(link, content.Html);
        Assert.Contains("Nguyễn Văn A", content.Html);
        Assert.Contains("admin@admin.com", content.Html);
        Assert.Contains("Đặt lại mật khẩu", content.Html);
    }

    [Fact]
    public void Thu_khong_chuyen_huong_thi_khong_co_nhan_demo()
    {
        using var culture = new CultureSwitch("vi-VN");
        var recipient = new EmailRecipient("that@gmail.com", "that@gmail.com");

        var content = EmailTemplates.PasswordReset("Trần B", "that@gmail.com", recipient, "https://x/y");

        Assert.DoesNotContain("[Demo]", content.Subject);
        Assert.Contains("Đặt lại mật khẩu", content.Html);
    }

    [Fact]
    public void Nguoi_dung_chon_tieng_anh_thi_thu_theo_tieng_anh()
    {
        using var culture = new CultureSwitch("en-US");
        var recipient = new EmailRecipient("that@gmail.com", "that@gmail.com");

        var content = EmailTemplates.PasswordReset("Tran B", "that@gmail.com", recipient, "https://x/y");

        Assert.Contains("Reset your password", content.Html);
        Assert.Contains("Reset your password", content.Subject);
    }

    [Fact]
    public void Ten_nguoi_dung_duoc_ma_hoa_html()
    {
        using var culture = new CultureSwitch("vi-VN");
        var recipient = new EmailRecipient("that@gmail.com", "that@gmail.com");

        var content = EmailTemplates.PasswordReset("<script>x</script>", "that@gmail.com", recipient, "https://x/y");

        Assert.DoesNotContain("<script>", content.Html);
        Assert.Contains("&lt;script&gt;", content.Html);
    }

    /// <summary>Đổi ngôn ngữ hiện tại trong phạm vi một test rồi trả lại như cũ.</summary>
    private sealed class CultureSwitch : IDisposable
    {
        private readonly CultureInfo previous = CultureInfo.CurrentUICulture;

        public CultureSwitch(string cultureName) =>
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        public void Dispose() => CultureInfo.CurrentUICulture = previous;
    }
}
