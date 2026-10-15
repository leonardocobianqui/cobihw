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

public partial class MainWindow : Window
{
    private const double SnapDistance = 20;
    private const double ResizeBorder = 6;

    private static readonly TimeSpan KeyCheckInterval = TimeSpan.FromMilliseconds(100);

    private readonly DispatcherTimer _keyTimer = new() { Interval = KeyCheckInterval };
    private bool _keepAtBottom;
    private bool _clickThrough;
    private InteractionKey _interactionKey;
    private bool _passingThrough;
    private ResizeDrag? _resize;
    private User32.Point _moveOffset;

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

    public event EventHandler? DragCompleted;

    public event EventHandler? LayoutCycleRequested;

    public event EventHandler<int>? ScaleDragged;

    public event EventHandler<int>? ScaleCompleted;

    public bool IsPositionLocked { get; set; }

    public bool SnapToEdges { get; set; }

    public int ScalePercent { get; set; } = AppearanceSettings.DefaultScale;

    public string? CurrentMonitor => Handle == 0 ? null : Forms.Screen.FromHandle(Handle).DeviceName;

    public void RestorePosition(double? left, double? top)
    {
        if (left is null || top is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left.Value;
        Top = top.Value;
    }

    public void ApplyMode(WidgetMode mode)
    {
        _keepAtBottom = mode == WidgetMode.Desktop;
        Topmost = mode == WidgetMode.AlwaysOnTop;

        if (_keepAtBottom)
        {
            SendToBottom();
        }
    }

    public void MoveToMonitor(string device)
    {
        Forms.Screen? target = Forms.Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == device);

        if (target is null || Handle == 0 || !User32.GetWindowRect(Handle, out User32.Rect window))
        {
            return;
        }

        Forms.Screen current = Forms.Screen.FromHandle(Handle);
        if (current.DeviceName == target.DeviceName)
        {
            return;
        }

        PixelRect placed = WindowFit.MoveTo(ToPixelRect(window), ToPixelRect(current.WorkingArea), ToPixelRect(target.WorkingArea));
        User32.SetWindowPos(Handle, 0, placed.Left, placed.Top, 0, 0, User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate);
    }

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

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        HwndSource.FromHwnd(Handle)?.AddHook(WindowProc);
        UpdatePassThrough();
    }

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

    private static bool IsKeyDown(int key) => (User32.GetAsyncKeyState(key) & 0x8000) != 0;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (User32.MonitorFromWindow(Handle, User32.MonitorDefaultToNull) == 0)
        {
            CenterOnPrimaryWorkArea();
        }
    }

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

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
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

    private void SendToBottom()
    {
        if (Handle != 0)
        {
            User32.SetWindowPos(Handle, User32.HwndBottom, 0, 0, 0, 0,
                User32.SwpNoMove | User32.SwpNoSize | User32.SwpNoActivate);
        }
    }

    private void CenterOnPrimaryWorkArea()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Top + (workArea.Height - ActualHeight) / 2;
    }

    private static PixelRect ToPixelRect(User32.Rect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static PixelRect ToPixelRect(System.Drawing.Rectangle rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private nint Handle => new WindowInteropHelper(this).Handle;

    private readonly record struct ResizeDrag(ResizeEdge Edge, int StartPercent, PixelRect StartRect, PixelRect WorkArea, Point StartPoint);
}
