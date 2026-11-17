using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using CobiHw.App.Linux.Interop;
using CobiHw.Presentation;
using CobiHw.Presentation.Settings;
using PixelBox = CobiHw.Presentation.PixelRect;

namespace CobiHw.App.Linux;

/// <summary>
/// The widget window: borderless, transparent and always sized to its content. Handles moving, snapping to screen
/// edges, resizing by zoom, click-through and staying under other windows; everything shown comes from <see cref="MainViewModel"/>.
/// </summary>
/// <remarks>
/// The move is the window manager's own (<see cref="Window.BeginMoveDrag"/>), which X11 doesn't let the app follow or
/// correct while it runs, unlike WM_MOVING on Windows. So the snap and the keep-on-screen rule run once the window has
/// stopped moving for <see cref="MoveSettleDelay"/>.
/// </remarks>
public partial class WidgetWindow : Window
{
    // Distances in DIPs, converted to pixels with the screen scaling.
    private const double SnapDistance = 20;
    private const double ResizeBorder = 6;
    // A press turns into a move only after this much travel, so a double-click still reaches the widget.
    private const double MoveThreshold = 4;

    // PositionChanged comes in bursts while the window manager moves the window; a pause this long means it stopped.
    private static readonly TimeSpan MoveSettleDelay = TimeSpan.FromMilliseconds(300);

    private readonly DispatcherTimer _moveSettled = new() { Interval = MoveSettleDelay };
    private readonly object? _details;
    private MainViewModel? _viewModel;
    private WidgetMode _mode = WidgetMode.AlwaysOnTop;
    private bool _clickThrough;
    private PointerPressedEventArgs? _press;
    private Point _pressPoint;
    private ResizeDrag? _resize;
    // Positions the app sets itself, which aren't the user's moves and must not snap or be reported.
    private PixelPoint? _ownPosition;

    /// <summary>Creates the widget window.</summary>
    public WidgetWindow()
    {
        InitializeComponent();
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowActivated = false;
        _details = ToolTip.GetTip(Surface);

        _moveSettled.Tick += (_, _) => OnMoveSettled();
        Opened += (_, _) => Dispatcher.UIThread.Post(AfterOpened, DispatcherPriority.Background);
        PositionChanged += OnPositionChanged;
        SizeChanged += OnSizeChanged;
        DataContextChanged += (_, _) => Watch(DataContext as MainViewModel);
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => EndResize();
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

    /// <summary>Snaps the widget to screen edges after a move.</summary>
    public bool SnapToEdges { get; set; }

    /// <summary>Current zoom in percent; the starting point for resizing.</summary>
    public int ScalePercent { get; set; } = AppearanceSettings.DefaultScale;

    /// <summary>Top-left corner in physical pixels, the unit X11 positions windows in.</summary>
    public PixelPoint PixelPosition => Position;

    /// <summary>Sets the start position: the saved pixel position, else the center of the screen.</summary>
    /// <param name="x">Saved left edge in pixels.</param>
    /// <param name="y">Saved top edge in pixels.</param>
    public void RestorePosition(int? x, int? y)
    {
        if (x is int left && y is int top)
        {
            // Before the window opens this is only the start position: no PositionChanged to tell apart.
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(left, top);
            return;
        }

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    /// <summary>Keeps the widget above or under other windows.</summary>
    /// <remarks>
    /// Under means the below layer where it's free. On GNOME the desktop icons own that layer
    /// (<see cref="DesktopEnvironment.BelowLayerIsShared"/>), so the widget stays in the normal layer and goes back to
    /// its bottom after every click, resize and move, the moments the window manager accepts that request.
    /// </remarks>
    /// <param name="mode">Mode to apply.</param>
    public void ApplyMode(WidgetMode mode)
    {
        _mode = mode;
        Topmost = mode == WidgetMode.AlwaysOnTop;
        ApplyBelow();
    }

    /// <summary>Turns click-through on or off. Linux has no interaction key: the tray menu turns it off.</summary>
    /// <param name="enabled">Lets clicks pass to the windows below.</param>
    public void ApplyClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        _ = X11.SetClickThrough(this, enabled);
    }

    /// <summary>
    /// Sends the X11 state again. The window manager forgets _NET_WM_STATE when a window is hidden, so this runs on
    /// every show; the input shape belongs to the window and survives, but resending it costs nothing.
    /// </summary>
    public void ReapplyState()
    {
        ApplyBelow();
        _ = X11.SetClickThrough(this, _clickThrough);
    }

    // Avalonia's Topmost covers "above"; "below" is a state it doesn't know.
    private void ApplyBelow()
    {
        _ = X11.SetState(this, "_NET_WM_STATE_BELOW", _mode == WidgetMode.Desktop && !DesktopEnvironment.BelowLayerIsShared);
        SinkIfDesktop();
    }

    // Desktop mode on GNOME: back under the other windows. Right after the user touched the widget the request is
    // accepted; when another app has the focus (the mode picked from the tray) it may be refused, and then the next
    // click on any window puts the widget behind it.
    private void SinkIfDesktop()
    {
        if (_mode == WidgetMode.Desktop && DesktopEnvironment.BelowLayerIsShared)
        {
            _ = X11.Lower(this);
        }
    }

    // Runs once the window is mapped: a state request sent before that would reach no managed window.
    private void AfterOpened()
    {
        ReapplyState();
        KeepOnScreen();
    }

    private void Watch(MainViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyHalo(viewModel.HaloColor);
        ApplyDetails(viewModel.DetailedTooltip);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.HaloColor))
        {
            ApplyHalo(_viewModel.HaloColor);
        }
        else if (e.PropertyName == nameof(MainViewModel.DetailedTooltip))
        {
            ApplyDetails(_viewModel.DetailedTooltip);
        }
    }

    // An effect isn't part of the visual tree, so a binding inside it never sees the DataContext: each color is a new effect.
    private void ApplyHalo(string color)
    {
        HaloOuter.Effect = Halo(color);
        HaloInner.Effect = Halo(color);
    }

    private static DropShadowEffect? Halo(string color) =>
        Color.TryParse(color, out Color parsed)
            ? new DropShadowEffect { OffsetX = 0, OffsetY = 0, BlurRadius = 3, Opacity = 1, Color = parsed }
            : null;

    // No tip means no tooltip: Avalonia has no per-control switch for it.
    private void ApplyDetails(bool enabled) => ToolTip.SetTip(Surface, enabled ? _details : null);

    // Double-click switches layout; near an edge resizes; anywhere else waits for the pointer to travel, then moves.
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            _press = null;
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

        _press = e;
        _pressPoint = e.GetPosition(this);
    }

    // The window manager takes the pointer from here on: no PointerReleased arrives for a move.
    private void BeginMove(PointerEventArgs e)
    {
        Point point = e.GetPosition(this);
        if (_press is not PointerPressedEventArgs press
            || (Math.Abs(point.X - _pressPoint.X) < MoveThreshold && Math.Abs(point.Y - _pressPoint.Y) < MoveThreshold))
        {
            return;
        }

        _press = null;
        BeginMoveDrag(press);
    }

    private void BeginResize(ResizeEdge edge, PointerPressedEventArgs e)
    {
        PixelBox window = PixelBounds();
        _resize = new ResizeDrag(edge, ScalePercent, window, WorkArea(window), this.PointToScreen(e.GetPosition(this)));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    // Resizing changes the zoom, not the window size: the window follows its content.
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resize is not ResizeDrag drag)
        {
            if (_press is not null)
            {
                BeginMove(e);
                return;
            }

            Cursor = IsPositionLocked ? null : CursorFor(EdgeAt(e));
            return;
        }

        PixelPoint point = this.PointToScreen(e.GetPosition(this));
        int percent = ResizeRule.PercentFor(drag.Edge, drag.StartPercent, drag.StartRect, point.X - drag.StartPoint.X, point.Y - drag.StartPoint.Y, drag.WorkArea);

        if (percent != ScalePercent)
        {
            ScalePercent = percent;
            ScaleDragged?.Invoke(this, percent);
        }
    }

    // Releasing the capture ends the resize (see PointerCaptureLost). A click or double-click ends here too.
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _press = null;
        if (_resize is not null)
        {
            e.Pointer.Capture(null);
        }

        SinkIfDesktop();
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

        SinkIfDesktop();
    }

    private ResizeEdge EdgeAt(PointerEventArgs e)
    {
        Point point = e.GetPosition(this);
        return ResizeRule.HitTest(point.X, point.Y, Bounds.Width, Bounds.Height, ResizeBorder);
    }

    private static Cursor? CursorFor(ResizeEdge edge) => edge switch
    {
        ResizeEdge.Left or ResizeEdge.Right => Cursors.WestEast,
        ResizeEdge.Top or ResizeEdge.Bottom => Cursors.NorthSouth,
        ResizeEdge.TopLeft => Cursors.TopLeft,
        ResizeEdge.TopRight => Cursors.TopRight,
        ResizeEdge.BottomLeft => Cursors.BottomLeft,
        ResizeEdge.BottomRight => Cursors.BottomRight,
        _ => null,
    };

    // Every move restarts the wait; the user's move is over once the bursts stop.
    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (e.Point == _ownPosition)
        {
            _ownPosition = null;
            return;
        }

        _moveSettled.Stop();
        _moveSettled.Start();
    }

    private void OnMoveSettled()
    {
        _moveSettled.Stop();
        PixelBox window = PixelBounds();
        PixelBox workArea = WorkArea(window);

        PixelBox placed = SnapToEdges ? WindowFit.Snap(window, workArea, (int)Math.Round(SnapDistance * DesktopScaling)) : window;
        placed = WindowFit.BringInside(placed, workArea);
        MoveTo(new PixelPoint(placed.Left, placed.Top));
        SinkIfDesktop();

        DragCompleted?.Invoke(this, EventArgs.Empty);
    }

    // The content changed size, by zoom or by its texts: the window stays inside the work area and keeps a docked edge
    // docked; while resizing, the edge opposite the one being dragged stays put.
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!IsVisible || e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0)
        {
            return;
        }

        PixelBox current = PixelBounds(e.PreviousSize);
        PixelBox requested = PixelBounds(e.NewSize);

        if (_resize is ResizeDrag drag)
        {
            requested = ResizeRule.Anchor(drag.Edge, drag.StartRect, requested.Width, requested.Height);
        }

        PixelBox placed = WindowFit.Place(current, requested, WorkArea(requested));
        MoveTo(new PixelPoint(placed.Left, placed.Top));
    }

    // Brings the widget back inside a work area, e.g. when the saved position is on a monitor that was removed.
    private void KeepOnScreen()
    {
        PixelBox window = PixelBounds();
        PixelBox workArea = WorkArea(window);

        if (!WindowFit.IsInside(window, workArea))
        {
            PixelBox placed = WindowFit.BringInside(window, workArea);
            MoveTo(new PixelPoint(placed.Left, placed.Top));
        }
    }

    private void MoveTo(PixelPoint point)
    {
        if (point == Position)
        {
            return;
        }

        _ownPosition = point;
        Position = point;
    }

    private PixelBox PixelBounds() => PixelBounds(Bounds.Size);

    private PixelBox PixelBounds(Size size)
    {
        int width = (int)Math.Ceiling(size.Width * DesktopScaling);
        int height = (int)Math.Ceiling(size.Height * DesktopScaling);
        return new PixelBox(Position.X, Position.Y, Position.X + width, Position.Y + height);
    }

    // Work area = screen minus panels and docks, as the window manager reports it. A window off every screen falls back
    // to the primary one, and with no screen information at all the window itself is the area, which changes nothing.
    private PixelBox WorkArea(PixelBox window)
    {
        Screen? screen = Screens.ScreenFromBounds(new Avalonia.PixelRect(window.Left, window.Top, window.Width, window.Height))
            ?? Screens.Primary;

        if (screen is null)
        {
            return window;
        }

        Avalonia.PixelRect area = screen.WorkingArea;
        return new PixelBox(area.X, area.Y, area.Right, area.Bottom);
    }

    // Created once: the pointer moves many times per second and each Cursor holds a native handle.
    private static class Cursors
    {
        public static readonly Cursor WestEast = new(StandardCursorType.SizeWestEast);
        public static readonly Cursor NorthSouth = new(StandardCursorType.SizeNorthSouth);
        public static readonly Cursor TopLeft = new(StandardCursorType.TopLeftCorner);
        public static readonly Cursor TopRight = new(StandardCursorType.TopRightCorner);
        public static readonly Cursor BottomLeft = new(StandardCursorType.BottomLeftCorner);
        public static readonly Cursor BottomRight = new(StandardCursorType.BottomRightCorner);
    }

    // State captured when a resize starts.
    private readonly record struct ResizeDrag(ResizeEdge Edge, int StartPercent, PixelBox StartRect, PixelBox WorkArea, PixelPoint StartPoint);
}
