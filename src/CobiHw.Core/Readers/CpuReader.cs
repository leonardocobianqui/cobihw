using System.Runtime.InteropServices;
using Microsoft.Win32;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

/// <summary>
/// Reads the processor name, usage and current clock speed, matching what Task Manager shows.
/// </summary>
internal sealed class CpuReader
{
    private const string ProcessorRegistryKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

    // Name and base clock don't change while Windows is running, so they're read once.
    private readonly string? _name;
    private readonly uint? _baseFrequencyMhz;
    private readonly PdhCounter? _utility;
    private readonly PdhCounter? _performance;

    /// <summary>Registers the CPU counters on the shared PDH query.</summary>
    /// <param name="query">Query collected once per sample by <see cref="HardwareMonitor"/>.</param>
    public CpuReader(PdhQuery query)
    {
        _name = ReadProcessorName();
        _baseFrequencyMhz = ReadBaseFrequencyMhz();

        // "% Processor Utility" is what Task Manager shows: unlike "% Processor Time", it accounts for clock speed.
        _utility = query.TryAddCounter(@"\Processor Information(_Total)\% Processor Utility");
        _performance = query.TryAddCounter(@"\Processor Information(_Total)\% Processor Performance");
    }

    /// <summary>Builds a snapshot from the values collected in the latest query sample.</summary>
    public CpuSnapshot Read() => new(_name, ReadUsagePercent(), ReadFrequencyGhz());

    private double? ReadUsagePercent()
    {
        double? utility = _utility?.GetValue();
        return utility is null ? null : Math.Clamp(utility.Value, 0, 100);
    }

    // Current clock = base clock × performance %; the counter goes above 100 while boosting.
    private double? ReadFrequencyGhz()
    {
        double? performancePercent = _performance?.GetValue(allowAbove100: true);

        if (performancePercent is null || _baseFrequencyMhz is null)
        {
            return null;
        }

        return _baseFrequencyMhz.Value * performancePercent.Value / 100 / 1000;
    }

    private static string? ReadProcessorName()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(ProcessorRegistryKey);
        string? name = key?.GetValue("ProcessorNameString") as string;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    // CallNtPowerInformation fills one entry per logical processor; the first one's MaxMhz is the base clock.
    private static uint? ReadBaseFrequencyMhz()
    {
        uint processorCount = Kernel32.GetActiveProcessorCount(Kernel32.AllProcessorGroups);
        if (processorCount == 0)
        {
            return null;
        }

        var processors = new PowrProf.ProcessorPowerInformation[processorCount];
        uint bufferSize = (uint)(processors.Length * Marshal.SizeOf<PowrProf.ProcessorPowerInformation>());

        uint status = PowrProf.CallNtPowerInformation(
            PowrProf.ProcessorInformationLevel, 0, 0, processors, bufferSize);

        return status == PowrProf.StatusSuccess && processors[0].MaxMhz > 0 ? processors[0].MaxMhz : null;
    }
}
