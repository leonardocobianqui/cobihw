using Microsoft.Win32;
using CobiHw.App.Settings;

namespace CobiHw.App;

/// <summary>Light or dark decision for parts WPF doesn't theme, such as the tray menu.</summary>
public static class WindowsTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    /// <summary>True when the app should be dark.</summary>
    /// <param name="theme">Theme chosen in the app.</param>
    /// <param name="windowsUsesDark">Windows app theme, used when the choice is "System".</param>
    public static bool IsDark(AppTheme theme, bool windowsUsesDark) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => windowsUsesDark,
    };

    /// <summary>Reads the app mode from the Windows personalization settings.</summary>
    public static bool AppsUseDarkTheme()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(AppsUseLightThemeValue) is int useLight && useLight == 0;
    }
}
