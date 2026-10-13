using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CobiHw.Core.Interop;

internal static partial class Pdh
{
    public const uint ErrorSuccess = 0x00000000;
    public const uint MoreData = 0x800007D2;
    public const uint NoData = 0x800007D5;
    public const uint CStatusValidData = 0x00000000;
    public const uint CStatusNewData = 0x00000001;
    public const uint FormatDouble = 0x00000200;
    public const uint FormatNoCap100 = 0x00008000;

    [StructLayout(LayoutKind.Explicit)]
    public struct CounterValue
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CounterValueItem
    {
        public nint Name;
        public CounterValue Value;
    }

    public sealed class QueryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public QueryHandle() : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => PdhCloseQuery(handle) == ErrorSuccess;
    }

    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhOpenQuery(string? dataSource, nint userData, out QueryHandle query);

    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhAddEnglishCounter(QueryHandle query, string fullCounterPath, nint userData, out nint counter);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCollectQueryData(QueryHandle query);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhGetFormattedCounterValue(nint counter, uint format, nint counterType, out CounterValue value);

    [LibraryImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW")]
    public static partial uint PdhGetFormattedCounterArray(nint counter, uint format, ref uint bufferSize, out uint itemCount, nint itemBuffer);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhCloseQuery(nint query);
}
