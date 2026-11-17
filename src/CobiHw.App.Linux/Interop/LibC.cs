using System.Runtime.InteropServices;

namespace CobiHw.App.Linux.Interop;

/// <summary>The C library calls the app needs.</summary>
internal static partial class LibC
{
    /// <summary>Numeric id of the user the process runs as.</summary>
    [LibraryImport("libc", EntryPoint = "geteuid")]
    public static partial uint GetEffectiveUserId();
}
