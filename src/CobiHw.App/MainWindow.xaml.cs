using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CobiHw.App.Interop;
using CobiHw.App.Settings;
using Forms = System.Windows.Forms;

namespace CobiHw.App;

/// <summary>
/// The widget window: borderless, transparent and always sized to its content.
/// Handles moving, snapping to screen edges, resizing by zoom, click-through and staying under other windows; everything shown comes from <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    // Distances in DIPs; the snap distance is converted to pixels with the monitor DPI.
    private const double SnapDistance = 20;
    private const double ResizeBorder = 6;

    // Click-through polls the interaction key: a window that ignores the mouse gets no keyboard input.
    private static readonly TimeSpan KeyCheckInterval = TimeSpan.FromMilliseconds(100);

    private readonly DispatcherTimer _keyTimer = new() { Interval = KeyCheckInterval };
    // Desktop mode: the window stays under every other window.
    private bool _keepAtBottom;
    private bool _clickThrough;
    private InteractionKey _interactionKey;
    // Whether WS_EX_TRANSPARENT is currently set.
    private bool _passingThrough;
    private ResizeDrag? _resize;
    // Cursor position inside the window when a move starts; used to snap.
    private User32.Point _moveOffset;
    // Saved position in physical pixels, applied once the window handle exists.
    private User32.Point? _restorePoint;

    /// <summary>Creates the widget window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        _keyTimer.Tick += (_, _) => UpdatePassThrough();
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        LostMouseCapture += (_, _) => EndResize();
    }

    /// <summary>Raised after the user moves the widget.</summary>
    public event EventHandler? DragCompleted;

    /// <summary>Raised on double-click, to switch to the next layout.</summary>
    public event EventHandler? LayoutCycleRequested;

    /// <summary>Raised while resizing, with the new zoom in percent.</summary>
    public event EventHandler<int>? ScaleDragged;

    /// <summary>Raised when a resize ends with a different zoom.</summary>
    public event EventHandler<int>? ScaleCompleted;

    /// <summary>Blocks moving and resizing.</summary>
    public bool IsPositionLocked { get; set; }

    /// <summary>Snaps the widget to screen edges while moving.</summary>
    public bool SnapToEdges { get; set; }

    /// <summary>Current zoom in percent; the starting point for resizing.</summary>
    public int ScalePercent { get; set; } = AppearanceSettings.DefaultScale;

    /// <summary>Device name of the monitor the widget is on, or null before the window exists.</summary>
    public string? CurrentMonitor => Handle == 0 ? null : Forms.Screen.FromHandle(Handle).DeviceName;

    /// <summary>Top-left corner in physical pixels, which stays exact across monitors with different DPI.</summary>
    internal User32.Point? PixelPosition =>
        Handle != 0 && User32.GetWindowRect(Handle, out User32.Rect window) ? new User32.Point { X = window.Left, Y = window.Top } : null;

    /// <summary>Sets the start position: the saved pixel position, else the old DIP position, else the screen center.</summary>
    /// <param name="x">Saved left edge in pixels.</param>
    /// <param name="y">Saved top edge in pixels.</param>
    /// <param name="legacyLeft">Left edge saved by older versions, in DIPs.</param>
    /// <param name="legacyTop">Top edge saved by older versions, in DIPs.</param>
    public void RestorePosition(int? x, int? y, double? legacyLeft, double? legacyTop)
    {
        if (x is int pixelX && y is int pixelY)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            _restorePoint = new User32.Point { X = pixelX, Y = pixelY };
            return;
        }

        if (legacyLeft is double left && legacyTop is double top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    /// <summary>Keeps the widget above or under other windows.</summary>
    /// <param name="mode">Mode to apply.</param>
    public void ApplyMode(WidgetMode mode)
    {
        _keepAtBottom = mode == WidgetMode.Desktop;
        Topmost = mode == WidgetMode.AlwaysOnTop;

        if (_keepAtBottom)
        {
            SendToBottom();
        }
    }

    /// <summary>Moves the widget to another monitor, keeping its relative position in the work area.</summary>
    /// <param name="device">Device name of the target monitor.</param>
    public void MoveToMonitor(string device)
    {
        Forms.Screen? target = Forms.Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == device);

        if (target is null || Handle == 0 || !User32.GetWindowRect(Handle, out User32.Rect window))
        {
            return;
        }

        Forms.Screen current = Forms.Screen.FromHandle(Handle);
        bool onScreen = User32.MonitorFromWindow(Handle, User32.MonitorDefaultToNull) != 0;

        if (onScreen && current.DeviceName == target.DeviceName)
        {
            return;
        }

        // A widget that is off every screen is just brought inside the target.
        PixelRect placed = onScreen
            ? WindowFit.MoveTo(ToPixelRect(window), ToPixelRect(current.WorkingArea), ToPixelRect(target.WorkingArea))
            : WindowFit.BringInside(ToPixelRect(window), ToPixelRect(target.WorkingArea));
        User32.SetWindowPos(Handle, 0, placed.Left, placed.Top, 0, 0, User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate);
    }

    /// <summary>Turns click-through on or off; holding the interaction key makes the widget clickable again.</summary>
    /// <param name="enabled">Lets clicks pass to the windows below.</param>
    /// <param name="interactionKey">Key that temporarily disables click-through.</param>
    public void ApplyClickThrough(bool enabled, InteractionKey interactionKey)
    {
        _clickThrough = enabled;
        _interactionKey = interactionKey;

        if (enabled)
        {
            _keyTimer.Start();
        }
        else
        {
            _keyTimer.Stop();
        }

        UpdatePassThrough();
    }

    // The window handle exists now: hooks window messages and moves to the saved pixel position.
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        HwndSource.FromHwnd(Handle)?.AddHook(WindowProc);

        if (_restorePoint is User32.Point point)
        {
            User32.SetWindowPos(Handle, 0, point.X, point.Y, 0, 0, User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate);
        }

        UpdatePassThrough();
    }

    // Doesn't switch to click-through while the left button is down, so a drag isn't cut off.
    private void UpdatePassThrough()
    {
        nint handle = Handle;
        if (handle == 0)
        {
            return;
        }

        bool holding = ClickThroughRule.IsHolding(_interactionKey, IsKeyDown);
        bool passThrough = _clickThrough && !holding;

        if (passThrough == _passingThrough || (passThrough && IsKeyDown(User32.VkLeftButton)))
        {
            return;
        }

        nint style = User32.GetWindowLongPtr(handle, User32.GwlExStyle);
        style = passThrough ? style | User32.WsExTransparent : style & ~User32.WsExTransparent;
        User32.SetWindowLongPtr(handle, User32.GwlExStyle, style);
        _passingThrough = passThrough;
    }

    // High bit set = the key is down right now.
    private static bool IsKeyDown(int key) => (User32.GetAsyncKeyState(key) & 0x8000) != 0;

    // Centers the widget if the saved position is off every screen (e.g. a monitor was removed).
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (User32.MonitorFromWindow(Handle, User32.MonitorDefaultToNull) == 0)
        {
            CenterOnPrimaryWorkArea();
        }
    }

    // Double-click switches layout; near an edge resizes; anywhere else moves.
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            LayoutCycleRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (IsPositionLocked)
        {
            return;
        }

        ResizeEdge edge = EdgeAt(e);
        if (edge != ResizeEdge.None)
        {
            BeginResize(edge, e);
            return;
        }

        DragMove();
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }

    // Captures the mouse so the resize continues outside the window.
    private void BeginResize(ResizeEdge edge, MouseButtonEventArgs e)
    {
        if (!User32.GetWindowRect(Handle, out User32.Rect window) || !TryGetWorkArea(window, out User32.Rect workArea))
        {
            return;
        }

        _resize = new ResizeDrag(edge, ScalePercent, ToPixelRect(window), ToPixelRect(workArea), PointToScreen(e.GetPosition(this)));
        CaptureMouse();
        e.Handled = true;
    }

    // Resizing changes the zoom, not the window size: the window follows its content.
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_resize is not ResizeDrag drag)
        {
            Cursor = IsPositionLocked ? null : CursorFor(EdgeAt(e));
            return;
        }

        Point point = PointToScreen(e.GetPosition(this));
        int deltaX = (int)Math.Round(point.X - drag.StartPoint.X);
        int deltaY = (int)Math.Round(point.Y - drag.StartPoint.Y);
        int percent = ResizeRule.PercentFor(drag.Edge, drag.StartPercent, drag.StartRect, deltaX, deltaY, drag.WorkArea);

        if (percent != ScalePercent)
        {
            ScalePercent = percent;
            ScaleDragged?.Invoke(this, percent);
        }
    }

    // Releasing the capture ends the resize (see LostMouseCapture).
    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_resize is not null)
        {
            ReleaseMouseCapture();
        }
    }

    private void EndResize()
    {
        if (_resize is not ResizeDrag drag)
        {
            return;
        }

        _resize = null;

        if (ScalePercent != drag.StartPercent)
        {
            ScaleCompleted?.Invoke(this, ScalePercent);
        }
    }

    private ResizeEdge EdgeAt(MouseEventArgs e)
    {
        Point point = e.GetPosition(this);
        return ResizeRule.HitTest(point.X, point.Y, ActualWidth, ActualHeight, ResizeBorder);
    }

    private static Cursor? CursorFor(ResizeEdge edge) => edge switch
    {
        ResizeEdge.Left or ResizeEdge.Right => Cursors.SizeWE,
        ResizeEdge.Top or ResizeEdge.Bottom => Cursors.SizeNS,
        ResizeEdge.TopLeft or ResizeEdge.BottomRight => Cursors.SizeNWSE,
        ResizeEdge.TopRight or ResizeEdge.BottomLeft => Cursors.SizeNESW,
        _ => null,
    };

    // Hook for the window messages WPF doesn't expose.
    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // Monitors changed: brings the widget back if it fell off-screen.
        if (message == User32.WmDisplayChange)
        {
            Dispatcher.InvokeAsync(KeepOnScreen, DispatcherPriority.Background);
        }

        // A size change keeps the window inside the work area; desktop mode forces the bottom of the Z order.
        if (message == User32.WmWindowPosChanging)
        {
            var position = Marshal.PtrToStructure<User32.WindowPos>(lParam);
            bool changed = FitInsideWorkArea(hwnd, ref position);

            if (_keepAtBottom)
            {
                position.HwndInsertAfter = User32.HwndBottom;
                position.Flags &= ~User32.SwpNoZOrder;
                changed = true;
            }

            if (changed)
            {
                Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
            }
        }

        if (message == User32.WmEnterSizeMove)
        {
            RememberMoveOffset(hwnd);
        }

        // Replaces the proposed rectangle with the snapped one.
        if (message == User32.WmMoving && SnapToEdges)
        {
            SnapMovingRect(lParam);
            handled = true;
            return 1;
        }

        return 0;
    }

    private void RememberMoveOffset(nint hwnd)
    {
        if (User32.GetCursorPos(out User32.Point cursor) && User32.GetWindowRect(hwnd, out User32.Rect window))
        {
            _moveOffset = new User32.Point { X = cursor.X - window.Left, Y = cursor.Y - window.Top };
        }
    }

    // Rebuilds the free position from the cursor, then snaps it to nearby work-area edges.
    private void SnapMovingRect(nint rectPointer)
    {
        var rect = Marshal.PtrToStructure<User32.Rect>(rectPointer);

        if (!User32.GetCursorPos(out User32.Point cursor) || !TryGetWorkArea(cursor, out User32.Rect workArea))
        {
            return;
        }

        int left = cursor.X - _moveOffset.X;
        int top = cursor.Y - _moveOffset.Y;
        var free = new PixelRect(left, top, left + rect.Right - rect.Left, top + rect.Bottom - rect.Top);
        int threshold = (int)Math.Round(SnapDistance * VisualTreeHelper.GetDpi(this).DpiScaleX);
        PixelRect snapped = WindowFit.Snap(free, ToPixelRect(workArea), threshold);

        rect.Left = snapped.Left;
        rect.Top = snapped.Top;
        rect.Right = snapped.Right;
        rect.Bottom = snapped.Bottom;
        Marshal.StructureToPtr(rect, rectPointer, fDeleteOld: false);
    }

    // When the size changes, moves the window so it stays inside the work area; while resizing, the opposite edge stays put.
    private bool FitInsideWorkArea(nint hwnd, ref User32.WindowPos position)
    {
        if ((position.Flags & User32.SwpNoSize) != 0 || !User32.GetWindowRect(hwnd, out User32.Rect window))
        {
            return false;
        }

        var current = new PixelRect(window.Left, window.Top, window.Right, window.Bottom);
        if (position.Width == current.Width && position.Height == current.Height)
        {
            return false;
        }

        bool keepsPosition = (position.Flags & User32.SwpNoMove) != 0;
        int x = keepsPosition ? current.Left : position.X;
        int y = keepsPosition ? current.Top : position.Y;
        var requested = new PixelRect(x, y, x + position.Width, y + position.Height);

        if (_resize is ResizeDrag drag)
        {
            requested = ResizeRule.Anchor(drag.Edge, drag.StartRect, position.Width, position.Height);
        }

        if (!TryGetWorkArea(new User32.Rect { Left = requested.Left, Top = requested.Top, Right = requested.Right, Bottom = requested.Bottom }, out User32.Rect work))
        {
            return false;
        }

        PixelRect placed = WindowFit.Place(current, requested, new PixelRect(work.Left, work.Top, work.Right, work.Bottom));
        if (placed.Left == x && placed.Top == y)
        {
            return false;
        }

        position.X = placed.Left;
        position.Y = placed.Top;
        position.Flags &= ~User32.SwpNoMove;
        return true;
    }

    // Work area = monitor minus the taskbar.
    private static bool TryGetWorkArea(User32.Rect windowRect, out User32.Rect workArea) =>
        TryGetWorkArea(User32.MonitorFromRect(windowRect, User32.MonitorDefaultToNearest), out workArea);

    private static bool TryGetWorkArea(User32.Point point, out User32.Rect workArea) =>
        TryGetWorkArea(User32.MonitorFromPoint(point, User32.MonitorDefaultToNearest), out workArea);

    private static bool TryGetWorkArea(nint monitor, out User32.Rect workArea)
    {
        var info = new User32.MonitorInfo { Size = (uint)Marshal.SizeOf<User32.MonitorInfo>() };

        if (monitor == 0 || !User32.GetMonitorInfo(monitor, ref info))
        {
            workArea = default;
            return false;
        }

        workArea = info.Work;
        return true;
    }

    // Brings the widget back inside the nearest work area.
    private void KeepOnScreen()
    {
        if (Handle == 0 || !User32.GetWindowRect(Handle, out User32.Rect window) || !TryGetWorkArea(window, out User32.Rect work))
        {
            return;
        }

        PixelRect current = ToPixelRect(window);
        PixelRect workArea = ToPixelRect(work);

        if (WindowFit.IsInside(current, workArea))
        {
            return;
        }

        PixelRect placed = WindowFit.BringInside(current, workArea);
        User32.SetWindowPos(Handle, 0, placed.Left, placed.Top, 0, 0, User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate);
    }

    private void SendToBottom()
    {
        if (Handle != 0)
        {
            User32.SetWindowPos(Handle, User32.HwndBottom, 0, 0, 0, 0,
                User32.SwpNoMove | User32.SwpNoSize | User32.SwpNoActivate);
        }
    }

    // The point (0,0) is always on the primary monitor.
    private void CenterOnPrimaryWorkArea()
    {
        if (!User32.GetWindowRect(Handle, out User32.Rect window) || !TryGetWorkArea(new User32.Point(), out User32.Rect work))
        {
            return;
        }

        int x = work.Left + (work.Right - work.Left - (window.Right - window.Left)) / 2;
        int y = work.Top + (work.Bottom - work.Top - (window.Bottom - window.Top)) / 2;
        User32.SetWindowPos(Handle, 0, x, y, 0, 0, User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate);
    }

    private static PixelRect ToPixelRect(User32.Rect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static PixelRect ToPixelRect(System.Drawing.Rectangle rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private nint Handle => new WindowInteropHelper(this).Handle;

    // State captured when a resize starts.
    private readonly record struct ResizeDrag(ResizeEdge Edge, int StartPercent, PixelRect StartRect, PixelRect WorkArea, Point StartPoint);
}
