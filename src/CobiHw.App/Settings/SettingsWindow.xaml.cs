using System.Windows;

namespace CobiHw.App.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
