using System.Runtime.InteropServices;

namespace CobiHw.App.Interop;

/// <summary>user32.dll declarations for window position, monitors and keyboard state.</summary>
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
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static partial nint GetWindowLongPtr(nint hwnd, int index);

    /// <summary>Changes a window attribute.</summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);

    /// <summary>Key state right now; the high bit is set while the key is down.</summary>
    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int key);
}
