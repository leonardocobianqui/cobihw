using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

/// <summary>
/// user32.dll declarations used to find the foreground app for the FPS counter.
/// </summary>
internal static partial class User32
{
    /// <summary>Window the user is currently working in.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    /// <summary>Process that owns <paramref name="window"/>.</summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint window, out uint processId);
}
