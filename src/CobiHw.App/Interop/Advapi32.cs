using System.Runtime.InteropServices;

namespace CobiHw.App.Interop;

internal static partial class Advapi32
{
    public const int TokenElevationTypeClass = 18;
    public const int TokenElevationTypeLimited = 3;

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(nint token, int informationClass, out int information, int informationLength, out int returnLength);
}
