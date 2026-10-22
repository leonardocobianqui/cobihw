using System.Runtime.InteropServices;

namespace CobiHw.App.Interop;

/// <summary>advapi32.dll declarations for the elevation check.</summary>
internal static partial class Advapi32
{
    /// <summary>TOKEN_INFORMATION_CLASS value TokenElevationType.</summary>
    public const int TokenElevationTypeClass = 18;
    /// <summary>TokenElevationTypeLimited: an administrator running without elevation.</summary>
    public const int TokenElevationTypeLimited = 3;

    /// <summary>Reads one piece of information about an access token.</summary>
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(nint token, int informationClass, out int information, int informationLength, out int returnLength);
}
