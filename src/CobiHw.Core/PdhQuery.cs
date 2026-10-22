using System.Runtime.InteropServices;
using CobiHw.Core.Interop;

namespace CobiHw.Core;

/// <summary>
/// A PDH query shared by all readers, so every counter is sampled at the same moment.
/// </summary>
internal sealed class PdhQuery : IDisposable
{
    private readonly Pdh.QueryHandle _handle;

    /// <summary>Opens the query.</summary>
    /// <exception cref="InvalidOperationException">PDH refused to open a query.</exception>
    public PdhQuery()
    {
        uint status = Pdh.PdhOpenQuery(null, 0, out _handle);
        if (status != Pdh.ErrorSuccess)
        {
            throw new InvalidOperationException($"PdhOpenQuery failed (0x{status:X8}).");
        }
    }

    /// <summary>Adds a counter by its English path.</summary>
    /// <returns>The counter, or <see langword="null"/> if it doesn't exist on this PC (e.g. no GPU counters in a VM).</returns>
    public PdhCounter? TryAddCounter(string englishPath)
    {
        uint status = Pdh.PdhAddEnglishCounter(_handle, englishPath, 0, out nint counter);
        return status == Pdh.ErrorSuccess ? new PdhCounter(counter) : null;
    }

    /// <summary>Samples every counter. Rate counters need two samples before they report a value.</summary>
    /// <exception cref="InvalidOperationException">PDH failed to collect.</exception>
    public void Collect()
    {
        uint status = Pdh.PdhCollectQueryData(_handle);
        if (status != Pdh.ErrorSuccess && status != Pdh.NoData)
        {
            throw new InvalidOperationException($"PdhCollectQueryData failed (0x{status:X8}).");
        }
    }

    /// <summary>Closes the query and all its counters.</summary>
    public void Dispose() => _handle.Dispose();
}

/// <summary>
/// One counter in a <see cref="PdhQuery"/>; values come from the query's latest sample.
/// </summary>
internal sealed class PdhCounter
{
    private readonly nint _handle;

    internal PdhCounter(nint handle) => _handle = handle;

    /// <summary>Reads a single-instance counter.</summary>
    /// <param name="allowAbove100">Turns off PDH's 0–100 clamp (byte rates, turbo clock).</param>
    /// <returns>The value, or <see langword="null"/> when there's no valid sample yet.</returns>
    public double? GetValue(bool allowAbove100 = false)
    {
        uint status = Pdh.PdhGetFormattedCounterValue(_handle, Format(allowAbove100), 0, out Pdh.CounterValue value);
        return status == Pdh.ErrorSuccess && IsValid(value.CStatus) ? value.DoubleValue : null;
    }

    /// <summary>Reads every instance of a wildcard counter.</summary>
    /// <param name="allowAbove100">Turns off PDH's 0–100 clamp.</param>
    /// <returns>Instance name and value pairs; empty on failure. Instances without valid data are skipped.</returns>
    public IReadOnlyList<(string Instance, double Value)> GetInstanceValues(bool allowAbove100 = false)
    {
        uint format = Format(allowAbove100);
        uint bufferSize = 0;
        nint buffer = 0;

        try
        {
            // The first call asks for the buffer size; GPU counters can grow between calls, hence the loop.
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

    // Item names point into the same buffer, so they must be copied before it is freed.
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
