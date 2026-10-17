using System.Diagnostics;
using System.Windows;
using CobiHw.App.Settings;

namespace CobiHw.App.Devices;

public partial class NetworkDevicesWindow : Window
{
    private const string LocationSettingsUri = "ms-settings:privacy-location";

    public NetworkDevicesWindow(AppTheme theme)
    {
        ApplyTheme(theme);
        InitializeComponent();
    }

    public void ApplyTheme(AppTheme theme) => WindowTheme.Apply(this, theme);

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnOpenLocationSettingsClick(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(LocationSettingsUri) { UseShellExecute = true })?.Dispose();
}
