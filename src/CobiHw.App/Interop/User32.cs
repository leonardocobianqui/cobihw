using System.Runtime.InteropServices;

namespace CobiHw.App.Interop;

/// <summary>user32.dll declarations for window position, monitors, keyboard state and the frame of the themed windows.</summary>
internal static partial class User32
{
    /// <summary>WM_WINDOWPOSCHANGING: the window is about to move or resize.</summary>
    public const int WmWindowPosChanging = 0x0046;
    /// <summary>WM_DISPLAYCHANGE: the resolution or the monitors changed.</summary>
    public const int WmDisplayChange = 0x007E;
    /// <summary>GWL_EXSTYLE: index of the extended window style.</summary>
    public const int GwlExStyle = -20;
    /// <summary>WS_EX_TRANSPARENT: on a layered window, mouse input goes to the windows below.</summary>
    public const nint WsExTransparent = 0x20;
    /// <summary>VK_LBUTTON.</summary>
    public const int VkLeftButton = 0x01;
    /// <summary>WM_MOVING: the window is being dragged; lParam points to its rectangle.</summary>
    public const int WmMoving = 0x0216;
    /// <summary>WM_ENTERSIZEMOVE: a move or resize started.</summary>
    public const int WmEnterSizeMove = 0x0231;
    /// <summary>HWND_BOTTOM: bottom of the Z order.</summary>
    public const nint HwndBottom = 1;
    /// <summary>SWP_NOSIZE.</summary>
    public const uint SwpNoSize = 0x0001;
    /// <summary>SWP_NOMOVE.</summary>
    public const uint SwpNoMove = 0x0002;
    /// <summary>SWP_NOZORDER.</summary>
    public const uint SwpNoZOrder = 0x0004;
    /// <summary>SWP_NOACTIVATE.</summary>
    public const uint SwpNoActivate = 0x0010;
    /// <summary>MONITOR_DEFAULTTONULL: returns 0 when off every monitor.</summary>
    public const uint MonitorDefaultToNull = 0x0000;
    /// <summary>MONITOR_DEFAULTTONEAREST.</summary>
    public const uint MonitorDefaultToNearest = 0x0002;
    /// <summary>WM_GETMINMAXINFO: asks for the size and position of the maximized window and the resize limits.</summary>
    public const int WmGetMinMaxInfo = 0x0024;
    /// <summary>WM_NCHITTEST: asks which part of the window is under the mouse.</summary>
    public const int WmNcHitTest = 0x0084;
    /// <summary>WM_NCMOUSEMOVE: the mouse moved over a non-client part, such as a caption button.</summary>
    public const int WmNcMouseMove = 0x00A0;
    /// <summary>WM_NCLBUTTONDOWN.</summary>
    public const int WmNcLButtonDown = 0x00A1;
    /// <summary>WM_NCLBUTTONUP.</summary>
    public const int WmNcLButtonUp = 0x00A2;
    /// <summary>WM_NCLBUTTONDBLCLK.</summary>
    public const int WmNcLButtonDoubleClick = 0x00A3;
    /// <summary>WM_NCRBUTTONUP.</summary>
    public const int WmNcRButtonUp = 0x00A5;
    /// <summary>WM_NCMOUSELEAVE: the mouse left the non-client part watched with TrackMouseEvent.</summary>
    public const int WmNcMouseLeave = 0x02A2;
    /// <summary>HTNOWHERE: nothing there (the shadow around the window).</summary>
    public const int HtNowhere = 0;
    /// <summary>HTCLIENT: the window content.</summary>
    public const int HtClient = 1;
    /// <summary>HTCAPTION: the title bar; Windows drags, snaps and maximizes with a double click.</summary>
    public const int HtCaption = 2;
    /// <summary>HTMAXBUTTON: the maximize button; Windows 11 shows the snap layouts over it.</summary>
    public const int HtMaxButton = 9;
    /// <summary>HTLEFT: the left border, for resizing; the other borders and corners follow, 11 to 17.</summary>
    public const int HtLeft = 10;
    /// <summary>HTRIGHT.</summary>
    public const int HtRight = 11;
    /// <summary>HTTOP.</summary>
    public const int HtTop = 12;
    /// <summary>HTTOPLEFT.</summary>
    public const int HtTopLeft = 13;
    /// <summary>HTTOPRIGHT.</summary>
    public const int HtTopRight = 14;
    /// <summary>HTBOTTOM.</summary>
    public const int HtBottom = 15;
    /// <summary>HTBOTTOMLEFT.</summary>
    public const int HtBottomLeft = 16;
    /// <summary>HTBOTTOMRIGHT.</summary>
    public const int HtBottomRight = 17;
    /// <summary>TME_LEAVE: send a leave message when the mouse leaves.</summary>
    public const uint TmeLeave = 0x00000002;
    /// <summary>TME_NONCLIENT: watch the non-client part (WM_NCMOUSELEAVE).</summary>
    public const uint TmeNonClient = 0x00000010;

    /// <summary>Mirrors WINDOWPOS.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPos
    {
        public nint Hwnd;
        public nint HwndInsertAfter;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public uint Flags;
    }

    /// <summary>Mirrors RECT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>Mirrors POINT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    /// <summary>Mirrors MINMAXINFO, in physical pixels; the maximized position is relative to the monitor.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    /// <summary>Mirrors TRACKMOUSEEVENT. <see cref="Size"/> must be set before the call.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TrackMouseEventInfo
    {
        public uint Size;
        public uint Flags;
        public nint Window;
        public uint HoverTime;
    }

    /// <summary>Mirrors MONITORINFO. <see cref="Size"/> must be set before the call.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        /// <summary>Monitor area minus the taskbar.</summary>
        public Rect Work;
        public uint Flags;
    }

    /// <summary>Moves, resizes or changes the Z order of a window.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint hwndInsertAfter, int x, int y, int width, int height, uint flags);

    /// <summary>Monitor that holds most of a window.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    /// <summary>Monitor that holds most of a rectangle.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromRect(in Rect rect, uint flags);

    /// <summary>Monitor that contains a point.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(Point point, uint flags);

    /// <summary>Cursor position in screen pixels.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out Point point);

    /// <summary>Monitor and work area rectangles of a monitor.</summary>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    /// <summary>Window rectangle in screen pixels.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hwnd, out Rect rect);

    /// <summary>Reads a window attribute, such as its extended style.</summary>
    /// <remarks>32-bit user32.dll has no GetWindowLongPtrW (the SDK header maps it to GetWindowLongW), so x86 calls that one.</remarks>
    public static nint GetWindowLongPtr(nint hwnd, int index) =>
        Environment.Is64BitProcess ? GetWindowLongPtr64(hwnd, index) : GetWindowLong32(hwnd, index);

    /// <summary>Changes a window attribute.</summary>
    /// <remarks>32-bit user32.dll has no SetWindowLongPtrW (the SDK header maps it to SetWindowLongW), so x86 calls that one.</remarks>
    public static nint SetWindowLongPtr(nint hwnd, int index, nint value) =>
        Environment.Is64BitProcess ? SetWindowLongPtr64(hwnd, index, value) : SetWindowLong32(hwnd, index, (int)value);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr64(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static partial int GetWindowLong32(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr64(nint hwnd, int index, nint value);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static partial int SetWindowLong32(nint hwnd, int index, int value);

    /// <summary>Key state right now; the high bit is set while the key is down.</summary>
    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int key);

    /// <summary>Asks Windows for a leave message (WM_NCMOUSELEAVE with <see cref="TmeNonClient"/>) when the mouse leaves the window part.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TrackMouseEvent(ref TrackMouseEventInfo info);

    /// <summary>Brings a window to the front; the tray menu needs it to close when the user clicks somewhere else.</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);
}
