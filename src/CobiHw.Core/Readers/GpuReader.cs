namespace CobiHw.Core.Readers;

internal sealed class GpuReader
{
    private const string PhysicalMarker = "phys_";
    private const string EngineTypeMarker = "_engtype_";

    private readonly GpuAdapter? _adapter;
    private readonly PdhCounter? _engineUtilization;
    private readonly PdhCounter? _dedicatedUsage;

    public GpuReader(PdhQuery query)
    {
        _adapter = GpuAdapter.FindMain();

        if (_adapter is not null)
        {
            _engineUtilization = query.TryAddCounter(@"\GPU Engine(*)\Utilization Percentage");
            _dedicatedUsage = query.TryAddCounter(@"\GPU Adapter Memory(*)\Dedicated Usage");
        }
    }

    public GpuSnapshot Read() =>
        new(_adapter?.Name, ReadUsagePercent(), ReadDedicatedMemoryUsedBytes(), _adapter?.DedicatedMemoryBytes);

    private double? ReadUsagePercent()
    {
        if (_adapter is null || _engineUtilization is null)
        {
            return null;
        }

        var utilizationByEngine = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (instance, utilization) in _engineUtilization.GetInstanceValues())
        {
            if (!instance.Contains(_adapter.LuidTag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? engine = ExtractEngineKey(instance);
            if (engine is null)
            {
                continue;
            }

            utilizationByEngine[engine] = utilizationByEngine.GetValueOrDefault(engine) + utilization;
        }

        return utilizationByEngine.Count == 0 ? null : Math.Min(100, utilizationByEngine.Values.Max());
    }

    private ulong? ReadDedicatedMemoryUsedBytes()
    {
        if (_adapter is null || _dedicatedUsage is null)
        {
            return null;
        }

        double? usedBytes = null;

        foreach (var (instance, bytes) in _dedicatedUsage.GetInstanceValues())
        {
            if (instance.Contains(_adapter.LuidTag, StringComparison.OrdinalIgnoreCase))
            {
                usedBytes = (usedBytes ?? 0) + bytes;
            }
        }

        return usedBytes is null ? null : (ulong)usedBytes.Value;
    }

    private static string? ExtractEngineKey(string instance)
    {
        int start = instance.IndexOf(PhysicalMarker, StringComparison.OrdinalIgnoreCase);
        int end = instance.IndexOf(EngineTypeMarker, StringComparison.OrdinalIgnoreCase);

        return start >= 0 && end > start ? instance[start..end] : null;
    }
}
