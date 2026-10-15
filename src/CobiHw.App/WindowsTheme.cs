using Microsoft.Win32;
using CobiHw.App.Settings;

namespace CobiHw.App;

public static class WindowsTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    public static bool IsDark(AppTheme theme, bool windowsUsesDark) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => windowsUsesDark,
    };

    public static bool AppsUseDarkTheme()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(AppsUseLightThemeValue) is int useLight && useLight == 0;
    }
}
