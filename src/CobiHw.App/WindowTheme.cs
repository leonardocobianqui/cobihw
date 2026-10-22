using System.Windows;
using CobiHw.App.Settings;

namespace CobiHw.App;

/// <summary>Applies the app theme to a WPF window.</summary>
internal static class WindowTheme
{
    /// <summary>Uses the WPF Fluent theme in light, dark or following Windows. ThemeMode is still marked experimental (WPF0001).</summary>
    /// <param name="window">Window to theme.</param>
    /// <param name="theme">Theme chosen in the app.</param>
    public static void Apply(Window window, AppTheme theme)
    {
#pragma warning disable WPF0001
        window.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
#pragma warning restore WPF0001
    }
}
