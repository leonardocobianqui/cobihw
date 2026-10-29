using System.Runtime.InteropServices;

namespace CobiHw.App.Interop;

/// <summary>dwmapi.dll declarations for the window frame.</summary>
internal static partial class Dwmapi
{
    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE: dark title bar, Windows 10 20H1 and later.</summary>
    public const int UseImmersiveDarkMode = 20;
    /// <summary>The same attribute before Windows 10 20H1, from 1809 on.</summary>
    public const int UseImmersiveDarkModeBefore20H1 = 19;

    /// <summary>Sets a frame attribute of the window; returns an HRESULT, 0 when it worked.</summary>
    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int attribute, in int value, int size);
}
