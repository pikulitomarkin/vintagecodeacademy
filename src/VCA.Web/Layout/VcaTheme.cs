using MudBlazor;

namespace VCA.Web.Layout;

public static class VcaTheme
{
    public static MudTheme Instance { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#00C9C9",
            Secondary = "#7AE582",
            Background = "#0D0D0D",
            BackgroundGray = "#121212",
            Surface = "#1A1A1A",
            AppbarBackground = "rgba(13,13,13,0.88)",
            DrawerBackground = "#101010",
            DrawerText = "#F5F5F5",
            DrawerIcon = "#00C9C9",
            TextPrimary = "#F5F5F5",
            TextSecondary = "rgba(245,245,245,0.72)",
            ActionDefault = "#00C9C9",
            ActionDisabled = "rgba(255,255,255,0.24)",
            LinesDefault = "rgba(255,255,255,0.08)",
            Success = "#7AE582",
            Warning = "#F4B860",
            Error = "#FF6B6B",
            Info = "#5BC0EB"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "sans-serif"]
            },
            H1 = new H1Typography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 800,
                LetterSpacing = "-0.04em"
            },
            H2 = new H2Typography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 700,
                LetterSpacing = "-0.03em"
            },
            H3 = new H3Typography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 700
            },
            H4 = new H4Typography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 700
            },
            H5 = new H5Typography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 700
            },
            H6 = new H6Typography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 700
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 600
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 600
            },
            Body1 = new Body1Typography
            {
                FontFamily = ["Inter", "sans-serif"]
            },
            Body2 = new Body2Typography
            {
                FontFamily = ["Inter", "sans-serif"]
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = 700,
                TextTransform = "none"
            },
            Caption = new CaptionTypography
            {
                FontFamily = ["Inter", "sans-serif"]
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "18px"
        }
    };
}