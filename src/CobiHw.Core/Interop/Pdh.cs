using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CobiHw.Core.Interop;

/// <summary>
/// pdh.dll declarations for Performance Data Helper, the API behind Performance Monitor and Task Manager.
/// </summary>
/// <remarks>Wrapped by <see cref="PdhQuery"/> and <see cref="PdhCounter"/>; nothing else should call these directly.</remarks>
internal static partial class Pdh
{
    public const uint ErrorSuccess = 0x00000000;
    /// <summary>PDH_MORE_DATA: the buffer is too small; retry with the size returned.</summary>
    public const uint MoreData = 0x800007D2;
    /// <summary>PDH_NO_DATA: the query has no counters yet; not an error.</summary>
    public const uint NoData = 0x800007D5;
    /// <summary>PDH_CSTATUS_VALID_DATA.</summary>
    public const uint CStatusValidData = 0x00000000;
    /// <summary>PDH_CSTATUS_NEW_DATA: also a usable value.</summary>
    public const uint CStatusNewData = 0x00000001;
    public const uint FormatDouble = 0x00000200;
    /// <summary>PDH_FMT_NOCAP100: don't clamp to 100 (needed for byte counts and turbo clocks).</summary>
    public const uint FormatNoCap100 = 0x00008000;

    /// <summary>Mirrors PDH_FMT_COUNTERVALUE for the double format; the value union starts at offset 8.</summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct CounterValue
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    /// <summary>Mirrors PDH_FMT_COUNTERVALUE_ITEM_W: instance name pointer plus its value.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CounterValueItem
    {
        public nint Name;
        public CounterValue Value;
    }

    /// <summary>Query handle closed with PdhCloseQuery when disposed.</summary>
    public sealed class QueryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public QueryHandle() : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => PdhCloseQuery(handle) == ErrorSuccess;
    }

    /// <summary>Opens a query on the local machine.</summary>
    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhOpenQuery(string? dataSource, nint userData, out QueryHandle query);

    /// <summary>Adds a counter by its English path, so it works on any Windows language.</summary>
    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhAddEnglishCounter(QueryHandle query, string fullCounterPath, nint userData, out nint counter);

    /// <summary>Takes a sample of every counter in the query.</summary>
    [LibraryImport("pdh.dll")]
    public static partial uint PdhCollectQueryData(QueryHandle query);

    /// <summary>Reads a single-instance counter from the last sample.</summary>
    [LibraryImport("pdh.dll")]
    public static partial uint PdhGetFormattedCounterValue(nint counter, uint format, nint counterType, out CounterValue value);

    /// <summary>Reads every instance of a wildcard counter, e.g. "PhysicalDisk(*)".</summary>
    [LibraryImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW")]
    public static partial uint PdhGetFormattedCounterArray(nint counter, uint format, ref uint bufferSize, out uint itemCount, nint itemBuffer);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhCloseQuery(nint query);
}
