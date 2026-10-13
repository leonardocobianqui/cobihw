using System.Runtime.InteropServices;
using Microsoft.Win32;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

internal sealed class CpuReader
{
    private const string ProcessorRegistryKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

    private readonly string? _name;
    private readonly uint? _baseFrequencyMhz;
    private readonly PdhCounter? _utility;
    private readonly PdhCounter? _performance;

    public CpuReader(PdhQuery query)
    {
        _name = ReadProcessorName();
        _baseFrequencyMhz = ReadBaseFrequencyMhz();
        _utility = query.TryAddCounter(@"\Processor Information(_Total)\% Processor Utility");
        _performance = query.TryAddCounter(@"\Processor Information(_Total)\% Processor Performance");
    }

    public CpuSnapshot Read() => new(_name, ReadUsagePercent(), ReadFrequencyGhz());

    private double? ReadUsagePercent()
    {
        double? utility = _utility?.GetValue();
        return utility is null ? null : Math.Clamp(utility.Value, 0, 100);
    }

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
