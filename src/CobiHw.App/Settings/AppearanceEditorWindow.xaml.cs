using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace CobiHw.App.Settings;

/// <summary>
/// Appearance editor window; logic lives in <see cref="AppearanceEditorViewModel"/>.
/// </summary>
public partial class AppearanceEditorWindow : Window
{
    // Space kept between the window and the screen edges.
    private const double ScreenMargin = 24;

    /// <summary>Creates the window with the given theme.</summary>
    public AppearanceEditorWindow(AppTheme theme)
    {
        WindowTheme.Apply(this, theme);
        InitializeComponent();
        Loaded += (_, _) => KeepInsideWorkArea();
    }

    // Limits the height to the screen, so the buttons never end up off-screen on small displays.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        MaxHeight = WorkArea().Height - 2 * ScreenMargin;
    }

    // The window opens next to its owner; this pulls it back inside the work area if it went past an edge.
    private void KeepInsideWorkArea()
    {
        Rect area = WorkArea();
        Left = Math.Max(area.Left, Math.Min(Left, area.Right - ActualWidth));
        Top = Math.Max(area.Top + ScreenMargin, Math.Min(Top, area.Bottom - ScreenMargin - ActualHeight));
    }

    // Work area of the owner's monitor (taskbar excluded), converted from pixels to WPF units.
    private Rect WorkArea()
    {
        nint handle = new WindowInteropHelper(Owner ?? this).Handle;
        System.Drawing.Rectangle pixels = Forms.Screen.FromHandle(handle).WorkingArea;
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(pixels.Left / dpi.DpiScaleX, pixels.Top / dpi.DpiScaleY, pixels.Width / dpi.DpiScaleX, pixels.Height / dpi.DpiScaleY);
    }

    // Opens the Windows color picker for the clicked color row.
    private void OnChooseColorClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ColorChoiceViewModel choice ||
            !HexColor.TryParse(choice.Color, out byte red, out byte green, out byte blue))
        {
            return;
        }

        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(red, green, blue),
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            choice.Color = HexColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
        }
    }
}
