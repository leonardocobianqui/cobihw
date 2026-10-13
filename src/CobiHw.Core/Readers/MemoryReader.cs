using System.Runtime.InteropServices;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

internal static class MemoryReader
{
    public static MemorySnapshot? Read()
    {
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
