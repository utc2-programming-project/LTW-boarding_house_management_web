using MudBlazor;

namespace Web.Components.Layout;

internal static class AppTheme
{
    // Đồng bộ bảng màu MudBlazor với token CSS của dự án.
    public static readonly MudTheme Value = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#4F46E5", Background = "#F2F2F7", Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF", AppbarText = "#000000",
            DrawerBackground = "#FFFFFF", DrawerText = "#3C3C43",
            TextPrimary = "#000000", TextSecondary = "#3C3C43",
            Divider = "#C6C6C8", Error = "#D70015", Success = "#1E7B36",
            Warning = "#8A4B00", Info = "#1E40AF"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#4F46E5", Background = "#000000", Surface = "#1C1C1E",
            AppbarBackground = "#1C1C1E", AppbarText = "#FFFFFF",
            DrawerBackground = "#1C1C1E", DrawerText = "#C7C7CC",
            TextPrimary = "#FFFFFF", TextSecondary = "#C7C7CC",
            Divider = "#38383A", Error = "#FF453A", Success = "#30D158",
            Warning = "#FF9F0A", Info = "#93C5FD"
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
