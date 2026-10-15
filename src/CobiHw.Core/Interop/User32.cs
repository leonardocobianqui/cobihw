using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

internal static partial class User32
{
    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint window, out uint processId);
}
