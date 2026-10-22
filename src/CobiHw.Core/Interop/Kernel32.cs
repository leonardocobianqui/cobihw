using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

/// <summary>
/// kernel32.dll declarations for memory, processor count and power status.
/// </summary>
internal static partial class Kernel32
{
    /// <summary>ALL_PROCESSOR_GROUPS: counts processors in every group (PCs with more than 64 threads).</summary>
    public const ushort AllProcessorGroups = 0xFFFF;

    /// <summary>Mirrors MEMORYSTATUSEX. <see cref="Length"/> must be set before the call.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    /// <summary>Fills <paramref name="buffer"/> with physical and virtual memory usage.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    /// <summary>Number of logical processors in a group, or in all of them with <see cref="AllProcessorGroups"/>.</summary>
    [LibraryImport("kernel32.dll")]
    public static partial uint GetActiveProcessorCount(ushort groupNumber);

    /// <summary>ACLineStatus: plugged in.</summary>
    public const byte AcLineOnline = 1;
    /// <summary>BatteryFlag bit: charging.</summary>
    public const byte BatteryFlagCharging = 0x08;
    /// <summary>BatteryFlag bit: no system battery (desktops).</summary>
    public const byte BatteryFlagNoSystemBattery = 0x80;
    /// <summary>BatteryFlag value: Windows can't read the battery state.</summary>
    public const byte BatteryFlagUnknown = 0xFF;
    /// <summary>BatteryLifePercent value: charge unknown.</summary>
    public const byte BatteryPercentUnknown = 0xFF;
    /// <summary>BatteryLifeTime value: remaining time unknown (e.g. while plugged in).</summary>
    public const uint BatteryLifeTimeUnknown = 0xFFFFFFFF;

    /// <summary>Mirrors SYSTEM_POWER_STATUS.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        /// <summary>Seconds of battery left.</summary>
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    /// <summary>Reads AC and battery state; no admin rights needed.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetSystemPowerStatus(out SystemPowerStatus status);
}
