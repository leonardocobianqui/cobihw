using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CobiHw.App.Interop;

namespace CobiHw.App;

/// <summary>
/// The app's own window frame (ThemedWindowTemplate in Controls.xaml): title bar, rounded corners and shadow, the same on Windows 10 and 11.
/// The window is borderless and transparent, so this class does what the Windows frame did: it tells Windows which part is the title bar,
/// the borders and the maximize button (Windows 11 shows the snap layouts over it), and keeps a maximized window inside the work area.
/// </summary>
public static class WindowFrame
{
    /// <summary>Transparent space around the window for its shadow; matches the margin of "Frame" in ThemedWindowTemplate.</summary>
    public const double ShadowSize = 16;

    /// <summary>The mouse is over the maximize button; Windows reports it as non-client, so WPF's IsMouseOver doesn't see it.</summary>
    public static readonly DependencyProperty IsNonClientHoverProperty =
        DependencyProperty.RegisterAttached("IsNonClientHover", typeof(bool), typeof(WindowFrame), new PropertyMetadata(false));

    /// <summary>The maximize button is held down; like <see cref="IsNonClientHoverProperty"/>, WPF's IsPressed doesn't see it.</summary>
    public static readonly DependencyProperty IsNonClientPressedProperty =
        DependencyProperty.RegisterAttached("IsNonClientPressed", typeof(bool), typeof(WindowFrame), new PropertyMetadata(false));

    // Corner radius of the content: the window's 12 minus its 1 px border.
    private const double ContentRadius = 11;
    // Resize grips around the visible edge: this far out, over the shadow, and this far in; a corner takes this much of each edge.
    private const double GripOutside = 8;
    private const double GripInside = 4;
    private const double CornerGrip = 14;
    // Size of the icon in the title bar.
    private const double IconSize = 16;

    // Sizes inside the app icon, smallest first; the title bar picks the one that fits the monitor's DPI.
    private static BitmapFrame[]? _iconFrames;

    /// <summary>Reads <see cref="IsNonClientHoverProperty"/>.</summary>
    /// <param name="element">Caption button.</param>
    public static bool GetIsNonClientHover(DependencyObject element) => (bool)element.GetValue(IsNonClientHoverProperty);

    /// <summary>Sets <see cref="IsNonClientHoverProperty"/>.</summary>
    /// <param name="element">Caption button.</param>
    /// <param name="value">True while the mouse is over it.</param>
    public static void SetIsNonClientHover(DependencyObject element, bool value) => element.SetValue(IsNonClientHoverProperty, value);

    /// <summary>Reads <see cref="IsNonClientPressedProperty"/>.</summary>
    /// <param name="element">Caption button.</param>
    public static bool GetIsNonClientPressed(DependencyObject element) => (bool)element.GetValue(IsNonClientPressedProperty);

    /// <summary>Sets <see cref="IsNonClientPressedProperty"/>.</summary>
    /// <param name="element">Caption button.</param>
    /// <param name="value">True while it is held down.</param>
    public static void SetIsNonClientPressed(DependencyObject element, bool value) => element.SetValue(IsNonClientPressedProperty, value);

    /// <summary>Makes the window borderless and transparent and gives it the frame; call it before the window has a handle.</summary>
    /// <param name="window">Window that uses ThemedWindowTemplate.</param>
    internal static void Attach(Window window) => _ = new Frame(window);

    // One per window; the window's events and its message hook keep it alive.
    private sealed class Frame
    {
        private readonly Window _window;
        private FrameworkElement? _card;
        private FrameworkElement? _content;
        private FrameworkElement? _titleBar;
        private Image? _icon;
        private Button? _maximize;
        private bool _trackingLeave;

        public Frame(Window window)
        {
            _window = window;
            window.WindowStyle = WindowStyle.None;
            window.AllowsTransparency = true;
            window.SourceInitialized += OnSourceInitialized;
            window.Loaded += OnLoaded;
            window.StateChanged += (_, _) => UpdateClip();
            window.DpiChanged += (_, _) => UpdateIcon();
        }

        private bool CanResize => _window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

        private bool IsMaximized => _window.WindowState == WindowState.Maximized;

        private void OnSourceInitialized(object? sender, EventArgs e) =>
            HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle)?.AddHook(WndProc);

        // The template is applied by now: finds its parts and wires the caption buttons. Until then every point counts as content.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _window.Loaded -= OnLoaded;
            _card = Part<FrameworkElement>("PART_Card");
            _content = Part<FrameworkElement>("PART_Content");
            _titleBar = Part<FrameworkElement>("PART_TitleBar");
            _icon = Part<Image>("PART_Icon");
            _maximize = Part<Button>("PART_MaximizeButton");

            if (Part<Button>("PART_MinimizeButton") is Button minimize)
            {
                minimize.Click += (_, _) => SystemCommands.MinimizeWindow(_window);
            }

            if (Part<Button>("PART_CloseButton") is Button close)
            {
                close.Click += (_, _) => SystemCommands.CloseWindow(_window);
            }

            // Clicks on the maximize button arrive as window messages (WndProc); this covers the rare click WPF still gets.
            if (_maximize is not null)
            {
                _maximize.Click += (_, _) => ToggleMaximize();
            }

            if (_content is not null)
            {
                _content.SizeChanged += (_, _) => UpdateClip();
            }

            UpdateIcon();
            UpdateClip();
        }

        private T? Part<T>(string name)
            where T : class => _window.Template?.FindName(name, _window) as T;

        private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        {
            switch (message)
            {
                case User32.WmNcHitTest:
                    handled = true;
                    return HitTest(lParam);
                case User32.WmGetMinMaxInfo:
                    // Not handled: WPF still applies MinWidth and MaxWidth afterwards.
                    FitMaximizedToWorkArea(hwnd, lParam);
                    break;
                case User32.WmNcMouseMove:
                    SetMaximizeHover(hwnd, wParam == User32.HtMaxButton);
                    break;
                case User32.WmNcMouseLeave:
                    _trackingLeave = false;
                    SetMaximizeHover(hwnd, false);
                    break;
                case User32.WmNcLButtonDown or User32.WmNcLButtonDoubleClick when wParam == User32.HtMaxButton:
                    // Handled here, otherwise Windows draws and tracks its own classic button.
                    SetPressed(true);
                    handled = true;
                    break;
                case User32.WmNcLButtonUp when wParam == User32.HtMaxButton:
                    if (_maximize is not null && GetIsNonClientPressed(_maximize))
                    {
                        ToggleMaximize();
                    }

                    SetPressed(false);
                    handled = true;
                    break;
                case User32.WmNcRButtonUp when wParam == User32.HtCaption:
                    // Right click on the title bar: the window menu (restore, move, size, minimize, maximize, close), as on any window.
                    SystemCommands.ShowSystemMenu(_window, ToDips(ScreenPoint(lParam)));
                    handled = true;
                    break;
            }

            return 0;
        }

        // Which part is under the mouse: the borders (to resize), the maximize button, the title bar (to drag) or the content.
        private nint HitTest(nint lParam)
        {
            if (_card is null || _titleBar is null)
            {
                return User32.HtClient;
            }

            Point point = _window.PointFromScreen(ScreenPoint(lParam));
            Rect card = BoundsOf(_card);

            if (!IsMaximized && CanResize && BorderAt(point, card) is int border)
            {
                return border;
            }

            if (!card.Contains(point))
            {
                // The shadow: nothing there.
                return User32.HtNowhere;
            }

            if (_maximize is { IsVisible: true } && BoundsOf(_maximize).Contains(point))
            {
                return User32.HtMaxButton;
            }

            if (BoundsOf(_titleBar).Contains(point) && !IsOverButton(point))
            {
                return User32.HtCaption;
            }

            return User32.HtClient;
        }

        // The resize border under the point, if the point is on the grip around the visible edge.
        private static int? BorderAt(Point point, Rect card)
        {
            Rect outer = card;
            outer.Inflate(GripOutside, GripOutside);
            Rect inner = card;
            inner.Inflate(-GripInside, -GripInside);
            if (!outer.Contains(point) || inner.Contains(point))
            {
                return null;
            }

            bool left = point.X < card.Left + CornerGrip;
            bool right = point.X > card.Right - CornerGrip;
            bool top = point.Y < card.Top + CornerGrip;
            bool bottom = point.Y > card.Bottom - CornerGrip;

            return (top, bottom, left, right) switch
            {
                (true, _, true, _) => User32.HtTopLeft,
                (true, _, _, true) => User32.HtTopRight,
                (_, true, true, _) => User32.HtBottomLeft,
                (_, true, _, true) => User32.HtBottomRight,
                _ when point.X < inner.Left => User32.HtLeft,
                _ when point.X > inner.Right => User32.HtRight,
                _ when point.Y < inner.Top => User32.HtTop,
                _ => User32.HtBottom,
            };
        }

        // True over the minimize or close button (or any other button in the title bar), which WPF handles as content.
        private bool IsOverButton(Point point)
        {
            for (DependencyObject? element = _window.InputHitTest(point) as DependencyObject; element is not null && element != _titleBar; element = VisualTreeHelper.GetParent(element))
            {
                if (element is ButtonBase)
                {
                    return true;
                }
            }

            return false;
        }

        private Rect BoundsOf(FrameworkElement element) =>
            element.TransformToAncestor(_window).TransformBounds(new Rect(element.RenderSize));

        private void ToggleMaximize()
        {
            if (IsMaximized)
            {
                SystemCommands.RestoreWindow(_window);
            }
            else
            {
                SystemCommands.MaximizeWindow(_window);
            }
        }

        // Without the Windows frame, a maximized window would cover the taskbar: it gets the work area of its monitor instead.
        private static void FitMaximizedToWorkArea(nint hwnd, nint lParam)
        {
            nint monitor = User32.MonitorFromWindow(hwnd, User32.MonitorDefaultToNearest);
            var info = new User32.MonitorInfo { Size = (uint)Marshal.SizeOf<User32.MonitorInfo>() };
            if (monitor == 0 || !User32.GetMonitorInfo(monitor, ref info))
            {
                return;
            }

            User32.MinMaxInfo limits = Marshal.PtrToStructure<User32.MinMaxInfo>(lParam);
            limits.MaxPosition = new User32.Point { X = info.Work.Left - info.Monitor.Left, Y = info.Work.Top - info.Monitor.Top };
            limits.MaxSize = new User32.Point { X = info.Work.Right - info.Work.Left, Y = info.Work.Bottom - info.Work.Top };
            Marshal.StructureToPtr(limits, lParam, fDeleteOld: false);
        }

        // Hover on the maximize button. Windows sends WM_NCMOUSELEAVE only when asked, so the first move over it asks.
        private void SetMaximizeHover(nint hwnd, bool hover)
        {
            if (_maximize is null)
            {
                return;
            }

            if (hover && !_trackingLeave)
            {
                var track = new User32.TrackMouseEventInfo
                {
                    Size = (uint)Marshal.SizeOf<User32.TrackMouseEventInfo>(),
                    Flags = User32.TmeLeave | User32.TmeNonClient,
                    Window = hwnd,
                };
                _trackingLeave = User32.TrackMouseEvent(ref track);
            }

            SetIsNonClientHover(_maximize, hover);
            if (!hover)
            {
                SetPressed(false);
            }
        }

        private void SetPressed(bool pressed)
        {
            if (_maximize is not null)
            {
                SetIsNonClientPressed(_maximize, pressed);
            }
        }

        // The content follows the rounded corners, so the title bar buttons and the footer band don't stick out of them.
        private void UpdateClip()
        {
            if (_content is null)
            {
                return;
            }

            double radius = IsMaximized ? 0 : ContentRadius;
            _content.Clip = new RectangleGeometry(new Rect(_content.RenderSize), radius, radius);
        }

        // The smallest size of the app icon that covers 16 px at this DPI, so it stays sharp without being scaled up.
        private void UpdateIcon()
        {
            if (_icon is null)
            {
                return;
            }

            _iconFrames ??= [.. BitmapDecoder.Create(App.IconResource, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames.OrderBy(frame => frame.PixelWidth)];
            double pixels = IconSize * VisualTreeHelper.GetDpi(_window).DpiScaleX;
            _icon.Source = _iconFrames.FirstOrDefault(frame => frame.PixelWidth >= pixels) ?? _iconFrames[^1];
        }

        // Screen point in physical pixels from a mouse message (two signed 16-bit values).
        private static Point ScreenPoint(nint lParam) =>
            new((short)((long)lParam & 0xFFFF), (short)(((long)lParam >> 16) & 0xFFFF));

        private Point ToDips(Point physical)
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(_window);
            return new Point(physical.X / dpi.DpiScaleX, physical.Y / dpi.DpiScaleY);
        }
    }
}
