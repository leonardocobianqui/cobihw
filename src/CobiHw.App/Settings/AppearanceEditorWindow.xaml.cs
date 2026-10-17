using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace CobiHw.App.Settings;

public partial class AppearanceEditorWindow : Window
{
    private const double ScreenMargin = 24;

    public AppearanceEditorWindow(AppTheme theme)
    {
        WindowTheme.Apply(this, theme);
        InitializeComponent();
        Loaded += (_, _) => KeepInsideWorkArea();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        MaxHeight = WorkArea().Height - 2 * ScreenMargin;
    }

    private void KeepInsideWorkArea()
    {
        Rect area = WorkArea();
        Left = Math.Max(area.Left, Math.Min(Left, area.Right - ActualWidth));
        Top = Math.Max(area.Top + ScreenMargin, Math.Min(Top, area.Bottom - ScreenMargin - ActualHeight));
    }

    private Rect WorkArea()
    {
        nint handle = new WindowInteropHelper(Owner ?? this).Handle;
        System.Drawing.Rectangle pixels = Forms.Screen.FromHandle(handle).WorkingArea;
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(pixels.Left / dpi.DpiScaleX, pixels.Top / dpi.DpiScaleY, pixels.Width / dpi.DpiScaleX, pixels.Height / dpi.DpiScaleY);
    }

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
