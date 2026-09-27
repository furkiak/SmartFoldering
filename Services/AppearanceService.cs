using Avalonia;
using Avalonia.Styling;
using SmartFoldering.Models;

namespace SmartFoldering.Services;

public static class AppearanceService
{
    public static void Apply(AppSettings settings)
    {
        SettingsManager.ValidateAppearance(settings.AppearanceTheme, settings.TextScalePercent);
        if (Application.Current is not { } application) return;
        var scale = settings.TextScalePercent / 100d;
        application.Resources["BodyFontSize"] = 14d * scale;
        application.Resources["CaptionFontSize"] = 12d * scale;
        application.Resources["SectionFontSize"] = 17d * scale;
        application.Resources["HeadingFontSize"] = 22d * scale;
        application.Resources["TitleFontSize"] = 26d * scale;
        application.RequestedThemeVariant = settings.AppearanceTheme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
