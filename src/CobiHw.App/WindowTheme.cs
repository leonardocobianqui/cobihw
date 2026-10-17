using System.Windows;
using CobiHw.App.Settings;

namespace CobiHw.App;

internal static class WindowTheme
{
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
