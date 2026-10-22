using System.Windows;
using System.Windows.Media.Imaging;
using CobiHw.App.Settings;

namespace CobiHw.App.About;

/// <summary>About window. Logic lives in <see cref="AboutViewModel"/>.</summary>
public partial class AboutWindow : Window
{
    /// <summary>Creates the window with the app theme and icon.</summary>
    /// <param name="theme">Theme of the app.</param>
    /// <param name="iconResource">App icon inside the resources.</param>
    public AboutWindow(AppTheme theme, Uri iconResource)
    {
        ApplyTheme(theme);
        InitializeComponent();
        // The .ico holds several sizes; the largest stays sharp when scaled down.
        AppIcon.Source = BitmapDecoder.Create(iconResource, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames.MaxBy(frame => frame.PixelWidth);
    }

    /// <summary>Changes the theme while the window is open.</summary>
    /// <param name="theme">New theme.</param>
    public void ApplyTheme(AppTheme theme) => WindowTheme.Apply(this, theme);

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
