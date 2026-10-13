using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CobiHw.App.Interop;
using CobiHw.App.Settings;

namespace CobiHw.App;

public partial class MainWindow : Window
{
    private const double SnapDistance = 20;

    private bool _keepAtBottom;
    private bool _snapToEdges;
    private DockedEdges _dockedEdges;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    [Flags]
    private enum DockedEdges
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
    }

    public event EventHandler? DragCompleted;

    public event EventHandler? LayoutCycleRequested;

    public bool IsPositionLocked { get; set; }

    public bool SnapToEdges
    {
        get => _snapToEdges;
        set
        {
            _snapToEdges = value;
            UpdateDockedEdges();
        }
    }

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

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        HwndSource.FromHwnd(Handle)?.AddHook(WindowProc);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (User32.MonitorFromWindow(Handle, User32.MonitorDefaultToNull) == 0)
        {
            CenterOnPrimaryWorkArea();
        }

        UpdateDockedEdges();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_snapToEdges)
        {
            KeepInsideWorkArea();
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
        UpdateDockedEdges();
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == User32.WmWindowPosChanging && _keepAtBottom)
        {
            var position = Marshal.PtrToStructure<User32.WindowPos>(lParam);
            position.HwndInsertAfter = User32.HwndBottom;
            position.Flags &= ~User32.SwpNoZOrder;
            Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
        }

        if (message == User32.WmMoving && _snapToEdges)
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

    private void UpdateDockedEdges()
    {
        _dockedEdges = DockedEdges.None;

        if (!_snapToEdges || !User32.GetWindowRect(Handle, out User32.Rect window) || !TryGetWorkArea(window, out User32.Rect workArea))
        {
            return;
        }

        if (window.Left == workArea.Left)
        {
            _dockedEdges |= DockedEdges.Left;
        }

        if (window.Top == workArea.Top)
        {
            _dockedEdges |= DockedEdges.Top;
        }

        if (window.Right == workArea.Right)
        {
            _dockedEdges |= DockedEdges.Right;
        }

        if (window.Bottom == workArea.Bottom)
        {
            _dockedEdges |= DockedEdges.Bottom;
        }
    }

    private void KeepInsideWorkArea()
    {
        if (!User32.GetWindowRect(Handle, out User32.Rect window) || !TryGetWorkArea(window, out User32.Rect workArea))
        {
            return;
        }

        int width = window.Right - window.Left;
        int height = window.Bottom - window.Top;
        int x = window.Left;
        int y = window.Top;

        if (_dockedEdges.HasFlag(DockedEdges.Right) || window.Right > workArea.Right)
        {
            x = workArea.Right - width;
        }

        if (_dockedEdges.HasFlag(DockedEdges.Left) || x < workArea.Left)
        {
            x = workArea.Left;
        }

        if (_dockedEdges.HasFlag(DockedEdges.Bottom) || window.Bottom > workArea.Bottom)
        {
            y = workArea.Bottom - height;
        }

        if (_dockedEdges.HasFlag(DockedEdges.Top) || y < workArea.Top)
        {
            y = workArea.Top;
        }

        if (x != window.Left || y != window.Top)
        {
            User32.SetWindowPos(Handle, 0, x, y, 0, 0,
                User32.SwpNoSize | User32.SwpNoZOrder | User32.SwpNoActivate);
        }
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
