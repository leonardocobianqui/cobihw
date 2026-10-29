using System.Windows;
using CobiHw.App.Settings;

namespace CobiHw.App;

/// <summary>Speed test window. Logic lives in <see cref="SpeedTestViewModel"/>; the phase colors come from the theme.</summary>
public partial class SpeedTestWindow : Window
{
    /// <summary>Creates the window with the app theme.</summary>
    /// <param name="theme">Theme of the app.</param>
    public SpeedTestWindow(AppTheme theme)
    {
        WindowTheme.Apply(this, theme);
        InitializeComponent();
    }
}
