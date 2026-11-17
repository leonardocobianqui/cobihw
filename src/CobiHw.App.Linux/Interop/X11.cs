using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace CobiHw.App.Linux.Interop;

/// <summary>
/// The X11 requests Avalonia doesn't expose: EWMH window state (keep below), lowering a window within its layer, and
/// an empty input shape (click-through).
/// </summary>
/// <remarks>
/// Avalonia runs on X11, through XWayland on a Wayland session, so these work on both. The app opens its own display
/// connection: window ids are global to the X server. The event layout is the 64-bit one, the only one .NET supports on Linux.
/// </remarks>
internal static partial class X11
{
    private const int ClientMessage = 33;
    private const nint SubstructureNotifyMask = 1 << 19;
    private const nint SubstructureRedirectMask = 1 << 20;
    private const int StateRemove = 0;
    private const int StateAdd = 1;
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;
    private const int Unsorted = 0;

    private static nint _display;

    /// <summary>Adds or removes an _NET_WM_STATE atom, such as _NET_WM_STATE_BELOW, by asking the window manager as EWMH requires.</summary>
    /// <returns>False when the window isn't an X11 window.</returns>
    public static bool SetState(Window window, string state, bool add)
    {
        if (!TryGetWindow(window, out nint xid))
        {
            return false;
        }

        var message = new ClientMessageEvent
        {
            Type = ClientMessage,
            Window = xid,
            MessageType = XInternAtom(_display, "_NET_WM_STATE", false),
            Format = 32,
            L0 = add ? StateAdd : StateRemove,
            L1 = XInternAtom(_display, state, false),
            // Source indication: a normal application.
            L3 = 1,
        };
        _ = XSendEvent(_display, XDefaultRootWindow(_display), false, SubstructureRedirectMask | SubstructureNotifyMask, ref message);
        _ = XFlush(_display);
        return true;
    }

    /// <summary>Asks to put the window at the bottom of its layer, under every other window of that layer.</summary>
    /// <remarks>
    /// A ConfigureRequest the window manager may refuse: Mutter, for one, refuses it while another app has the focus,
    /// and accepts it right after the user clicked the window.
    /// </remarks>
    /// <returns>False when the window isn't an X11 window.</returns>
    public static bool Lower(Window window)
    {
        if (!TryGetWindow(window, out nint xid))
        {
            return false;
        }

        _ = XLowerWindow(_display, xid);
        _ = XFlush(_display);
        return true;
    }

    /// <summary>An empty input shape lets every click through to the windows below; resetting the shape takes clicks again.</summary>
    /// <returns>False when the window isn't an X11 window.</returns>
    public static bool SetClickThrough(Window window, bool enabled)
    {
        if (!TryGetWindow(window, out nint xid))
        {
            return false;
        }

        if (enabled)
        {
            XShapeCombineRectangles(_display, xid, ShapeInput, 0, 0, 0, 0, ShapeSet, Unsorted);
        }
        else
        {
            XShapeCombineMask(_display, xid, ShapeInput, 0, 0, 0, ShapeSet);
        }

        _ = XFlush(_display);
        return true;
    }

    private static bool TryGetWindow(Window window, out nint xid)
    {
        xid = 0;
        if (window.TryGetPlatformHandle() is not { HandleDescriptor: "XID" } handle)
        {
            return false;
        }

        if (_display == 0)
        {
            _display = XOpenDisplay(0);
        }

        xid = handle.Handle;
        return _display != 0;
    }

    // XClientMessageEvent on 64-bit Linux, padded to the size of XEvent (24 longs), which XSendEvent reads in full.
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct ClientMessageEvent
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(8)] public nuint Serial;
        [FieldOffset(16)] public int SendEvent;
        [FieldOffset(24)] public nint Display;
        [FieldOffset(32)] public nint Window;
        [FieldOffset(40)] public nint MessageType;
        [FieldOffset(48)] public int Format;
        [FieldOffset(56)] public nint L0;
        [FieldOffset(64)] public nint L1;
        [FieldOffset(72)] public nint L2;
        [FieldOffset(80)] public nint L3;
        [FieldOffset(88)] public nint L4;
    }

    [LibraryImport("libX11.so.6")]
    private static partial nint XOpenDisplay(nint name);

    [LibraryImport("libX11.so.6")]
    private static partial nint XDefaultRootWindow(nint display);

    [LibraryImport("libX11.so.6", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint XInternAtom(nint display, string name, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [LibraryImport("libX11.so.6")]
    private static partial int XSendEvent(nint display, nint window, [MarshalAs(UnmanagedType.Bool)] bool propagate, nint eventMask, ref ClientMessageEvent message);

    [LibraryImport("libX11.so.6")]
    private static partial int XFlush(nint display);

    [LibraryImport("libX11.so.6")]
    private static partial int XLowerWindow(nint display, nint window);

    [LibraryImport("libXext.so.6")]
    private static partial void XShapeCombineRectangles(nint display, nint window, int destKind, int xOffset, int yOffset, nint rectangles, int count, int operation, int ordering);

    [LibraryImport("libXext.so.6")]
    private static partial void XShapeCombineMask(nint display, nint window, int destKind, int xOffset, int yOffset, nint pixmap, int operation);
}
