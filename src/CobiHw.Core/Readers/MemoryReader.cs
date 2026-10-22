using System.Runtime.InteropServices;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

/// <summary>
/// Reads physical memory usage through <c>GlobalMemoryStatusEx</c>.
/// </summary>
internal static class MemoryReader
{
    /// <summary>Reads used and total physical memory.</summary>
    /// <returns>The memory state, or <see langword="null"/> if Windows refuses the call.</returns>
    public static MemorySnapshot? Read()
    {
        // Windows requires the structure size to be filled in before the call.
        var status = new Kernel32.MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<Kernel32.MemoryStatusEx>(),
        };

        if (!Kernel32.GlobalMemoryStatusEx(ref status))
        {
            return null;
        }

        return new MemorySnapshot(status.TotalPhys - status.AvailPhys, status.TotalPhys);
    }
}
