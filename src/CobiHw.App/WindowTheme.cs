using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CobiHw.App.Interop;
using CobiHw.App.Settings;

namespace CobiHw.App;

/// <summary>
/// Applies the cobihw design system (the Themes folder) to the app windows and the tray menu, the same on Windows 10 and 11.
/// The widget window doesn't use it: its colors come from the Appearance tab.
/// </summary>
internal static class WindowTheme
{
    private const string WindowTemplateKey = "ThemedWindowTemplate";

    private static ResourceDictionary? _dark;
    private static ResourceDictionary? _light;
    private static ResourceDictionary? _controls;

    /// <summary>Control styles, shared by every themed window and the tray menu.</summary>
    public static ResourceDictionary Controls => _controls ??= Load("Controls.xaml");

    /// <summary>Colors of the dark or light theme.</summary>
    /// <param name="dark">True for dark.</param>
    public static ResourceDictionary Tokens(bool dark) => dark ? _dark ??= Load("Dark.xaml") : _light ??= Load("Light.xaml");

    /// <summary>
    /// Themes a window, before or after InitializeComponent: colors first and control styles after them, so the styles find the colors;
    /// the window's own resources come from its XAML and stay on top.
    /// </summary>
    /// <param name="window">Window to theme.</param>
    /// <param name="theme">Theme chosen in the app.</param>
    public static void Apply(Window window, AppTheme theme)
    {
        ResourceDictionary tokens = Tokens(WindowsTheme.IsDark(theme, WindowsTheme.AppsUseDarkTheme()));
        Collection<ResourceDictionary> merged = window.Resources.MergedDictionaries;

        if (merged.Contains(Controls))
        {
            // Swapping the colors refreshes the whole window, so it only happens when they change.
            if (merged[0] != tokens)
            {
                merged[0] = tokens;
            }
        }
        else
        {
            merged.Insert(0, tokens);
            merged.Insert(1, Controls);
            window.Template = (ControlTemplate)Controls[WindowTemplateKey];
            window.SetResourceReference(Control.BackgroundProperty, "WindowBgBrush");
            window.SetResourceReference(Control.ForegroundProperty, "InkBrush");
            window.SourceInitialized += (_, _) => SetDarkTitleBar(window);
        }

        SetDarkTitleBar(window);
    }

    /// <summary>Changes the theme of every open themed window; also called when Windows switches light/dark.</summary>
    /// <param name="theme">Theme chosen in the app.</param>
    public static void ApplyAll(AppTheme theme)
    {
        foreach (Window window in Application.Current.Windows)
        {
            if (window.Resources.MergedDictionaries.Contains(Controls))
            {
                Apply(window, theme);
            }
        }
    }

    // Stage 1 keeps the Windows title bar; this makes it dark with the dark theme. Before the window has a handle it waits for SourceInitialized.
    private static void SetDarkTitleBar(Window window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        int dark = window.Resources.MergedDictionaries[0] == Tokens(dark: true) ? 1 : 0;
        if (Dwmapi.DwmSetWindowAttribute(handle, Dwmapi.UseImmersiveDarkMode, dark, sizeof(int)) != 0)
        {
            // Older Windows 10 builds know only the earlier number; versions without either keep the light bar.
            _ = Dwmapi.DwmSetWindowAttribute(handle, Dwmapi.UseImmersiveDarkModeBefore20H1, dark, sizeof(int));
        }
    }

    private static ResourceDictionary Load(string file) =>
        new() { Source = new Uri($"pack://application:,,,/Themes/{file}", UriKind.Absolute) };
}
