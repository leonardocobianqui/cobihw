using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CobiHw.App.Interop;
using CobiHw.App.Settings;

namespace CobiHw.App;

public partial class MainWindow : Window
{
    private const double SnapDistance = 20;

    private static readonly TimeSpan KeyCheckInterval = TimeSpan.FromMilliseconds(100);

    private readonly DispatcherTimer _keyTimer = new() { Interval = KeyCheckInterval };
    private bool _keepAtBottom;
    private bool _clickThrough;
    private InteractionKey _interactionKey;
    private bool _passingThrough;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        _keyTimer.Tick += (_, _) => UpdatePassThrough();
    }

    public event EventHandler? DragCompleted;

    public event EventHandler? LayoutCycleRequested;

    public bool IsPositionLocked { get; set; }

    public bool SnapToEdges { get; set; }

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

        DragMove();
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }

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

        if (message == User32.WmMoving && SnapToEdges)
        {
            SnapMovingRect(lParam);
            handled = true;
            return 1;
        }

        return 0;
    }

    private void SnapMovingRect(nint rectPointer)
    {
        var rect = Marshal.PtrToStructure<User32.Rect>(rectPointer);

        if (!TryGetWorkArea(rect, out User32.Rect workArea))
        {
            return;
        }

        int threshold = (int)Math.Round(SnapDistance * VisualTreeHelper.GetDpi(this).DpiScaleX);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (Math.Abs(rect.Left - workArea.Left) <= threshold)
        {
            rect.Left = workArea.Left;
        }
        else if (Math.Abs(rect.Right - workArea.Right) <= threshold)
        {
            rect.Left = workArea.Right - width;
        }

        if (Math.Abs(rect.Top - workArea.Top) <= threshold)
        {
            rect.Top = workArea.Top;
        }
        else if (Math.Abs(rect.Bottom - workArea.Bottom) <= threshold)
        {
            rect.Top = workArea.Bottom - height;
        }

        rect.Right = rect.Left + width;
        rect.Bottom = rect.Top + height;
        Marshal.StructureToPtr(rect, rectPointer, fDeleteOld: false);
    }

    private static bool FitInsideWorkArea(nint hwnd, ref User32.WindowPos position)
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

    private static bool TryGetWorkArea(User32.Rect windowRect, out User32.Rect workArea)
    {
        nint monitor = User32.MonitorFromRect(windowRect, User32.MonitorDefaultToNearest);
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

    private nint Handle => new WindowInteropHelper(this).Handle;
}
