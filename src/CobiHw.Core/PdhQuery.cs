using System.Runtime.InteropServices;
using CobiHw.Core.Interop;

namespace CobiHw.Core;

internal sealed class PdhQuery : IDisposable
{
    private readonly Pdh.QueryHandle _handle;

    public PdhQuery()
    {
        uint status = Pdh.PdhOpenQuery(null, 0, out _handle);
        if (status != Pdh.ErrorSuccess)
        {
            throw new InvalidOperationException($"PdhOpenQuery falhou (0x{status:X8}).");
        }
    }

    public PdhCounter? TryAddCounter(string englishPath)
    {
        uint status = Pdh.PdhAddEnglishCounter(_handle, englishPath, 0, out nint counter);
        return status == Pdh.ErrorSuccess ? new PdhCounter(counter) : null;
    }

    public void Collect()
    {
        uint status = Pdh.PdhCollectQueryData(_handle);
        if (status != Pdh.ErrorSuccess && status != Pdh.NoData)
        {
            throw new InvalidOperationException($"PdhCollectQueryData falhou (0x{status:X8}).");
        }
    }

    public void Dispose() => _handle.Dispose();
}

internal sealed class PdhCounter
{
    private readonly nint _handle;

    internal PdhCounter(nint handle) => _handle = handle;

    public double? GetValue(bool allowAbove100 = false)
    {
        uint status = Pdh.PdhGetFormattedCounterValue(_handle, Format(allowAbove100), 0, out Pdh.CounterValue value);
        return status == Pdh.ErrorSuccess && IsValid(value.CStatus) ? value.DoubleValue : null;
    }

    public IReadOnlyList<(string Instance, double Value)> GetInstanceValues(bool allowAbove100 = false)
    {
        uint format = Format(allowAbove100);
        uint bufferSize = 0;
        nint buffer = 0;

        try
        {
            while (true)
            {
                uint status = Pdh.PdhGetFormattedCounterArray(_handle, format, ref bufferSize, out uint itemCount, buffer);

                if (status == Pdh.MoreData)
                {
                    Marshal.FreeHGlobal(buffer);
                    buffer = Marshal.AllocHGlobal((int)bufferSize);
                    continue;
                }

                if (status != Pdh.ErrorSuccess)
                {
                    return [];
                }

                return ParseItems(buffer, itemCount);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static List<(string Instance, double Value)> ParseItems(nint buffer, uint itemCount)
    {
        int itemSize = Marshal.SizeOf<Pdh.CounterValueItem>();
        var items = new List<(string Instance, double Value)>((int)itemCount);

        for (int i = 0; i < itemCount; i++)
        {
            var item = Marshal.PtrToStructure<Pdh.CounterValueItem>(buffer + i * itemSize);
            if (IsValid(item.Value.CStatus))
            {
                items.Add((Marshal.PtrToStringUni(item.Name) ?? string.Empty, item.Value.DoubleValue));
            }
        }

        return items;
    }

    private static uint Format(bool allowAbove100) =>
        Pdh.FormatDouble | (allowAbove100 ? Pdh.FormatNoCap100 : 0);

    private static bool IsValid(uint cStatus) =>
        cStatus is Pdh.CStatusValidData or Pdh.CStatusNewData;
}
