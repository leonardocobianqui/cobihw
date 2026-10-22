using System.Runtime.InteropServices;
using System.Windows;
using CobiHw.App.Resources;
using CobiHw.App.Settings;

namespace CobiHw.App;

/// <summary>Shown after an unexpected error, with details the user can copy into a bug report.</summary>
public partial class ErrorWindow : Window
{
    private readonly string _details;

    /// <summary>Builds the details from the error and the diagnostic header.</summary>
    /// <param name="exception">Unhandled error.</param>
    /// <param name="theme">Theme of the app.</param>
    public ErrorWindow(Exception exception, AppTheme theme)
    {
        _details = $"{AppInfo.DiagnosticHeader}{Environment.NewLine}{Environment.NewLine}{exception}";
        WindowTheme.Apply(this, theme);
        InitializeComponent();
        DetailsBox.Text = _details;
    }

    // Another app may be holding the clipboard; then nothing is copied.
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_details);
            CopyButton.Content = Strings.Error_Copied;
        }
        catch (ExternalException)
        {
        }
    }
}
