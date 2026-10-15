using MudBlazor;

namespace Web.Components.Layout;

internal static class AppTheme
{
    // Đồng bộ bảng màu MudBlazor với token CSS của dự án.
    public static readonly MudTheme Value = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#4F46E5", Background = "#F8FAFC", Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF", AppbarText = "#0D131B",
            DrawerBackground = "#FFFFFF", DrawerText = "#475569",
            TextPrimary = "#0D131B", TextSecondary = "#475569",
            Divider = "#E2E8F0", Error = "#B91C1C", Success = "#166534",
            Warning = "#92400E", Info = "#1E40AF"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#4F46E5", Background = "#0F172A", Surface = "#1E293B",
            AppbarBackground = "#111827", AppbarText = "#F8FAFC",
            DrawerBackground = "#111827", DrawerText = "#CBD5E1",
            TextPrimary = "#F8FAFC", TextSecondary = "#CBD5E1",
            Divider = "#334155", Error = "#FCA5A5", Success = "#86EFAC",
            Warning = "#FCD34D", Info = "#93C5FD"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Inter", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"], FontSize = ".875rem", LineHeight = "1.571", LetterSpacing = "0" },
            H1 = new H1Typography { FontSize = "1.75rem", LineHeight = "1.286", FontWeight = "700", LetterSpacing = "-.02em" },
            H2 = new H2Typography { FontSize = "1.25rem", LineHeight = "1.4", FontWeight = "600", LetterSpacing = "-.01em" },
            H3 = new H3Typography { FontSize = "1.125rem", LineHeight = "1.444", FontWeight = "600", LetterSpacing = "0" },
            Body1 = new Body1Typography { FontSize = ".875rem", LineHeight = "1.571", LetterSpacing = "0" },
            Body2 = new Body2Typography { FontSize = ".875rem", LineHeight = "1.571", LetterSpacing = "0" },
            Button = new ButtonTypography { FontSize = ".875rem", LineHeight = "1.429", FontWeight = "600", LetterSpacing = "0", TextTransform = "none" },
            Caption = new CaptionTypography { FontSize = ".75rem", LineHeight = "1.5", LetterSpacing = "0" }
        }
    };
}
