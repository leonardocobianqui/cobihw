using System.Diagnostics;
using System.Windows;
using CobiHw.App.Settings;

namespace CobiHw.App.Devices;

/// <summary>Network devices window. Logic lives in <see cref="NetworkDevicesViewModel"/>.</summary>
public partial class NetworkDevicesWindow : Window
{
    // Opens Settings > Privacy > Location.
    private const string LocationSettingsUri = "ms-settings:privacy-location";

    /// <summary>Creates the window with the app theme.</summary>
    /// <param name="theme">Theme of the app.</param>
    public NetworkDevicesWindow(AppTheme theme)
    {
        WindowTheme.Apply(this, theme);
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnOpenLocationSettingsClick(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(LocationSettingsUri) { UseShellExecute = true })?.Dispose();
}
