namespace CobiHw.Core.Readers;

/// <summary>
/// Reads usage, memory and temperature of every GPU from the "GPU Engine" and "GPU Adapter Memory" counters.
/// </summary>
/// <remarks>
/// Counter instances are named per process and engine, e.g. "pid_1234_luid_0x0_0xB002_phys_0_eng_0_engtype_3D";
/// the LUID inside the name ties each value to its adapter.
/// </remarks>
internal sealed class GpuReader : IDisposable
{
    private const string LuidMarker = "luid_";
    private const int LuidTagLength = 26; // "luid_0x00000000_0x0000B002"
    private const string PhysicalMarker = "phys_";
    private const string EngineTypeMarker = "_engtype_";

    private readonly PdhCounter? _engineUtilization;
    private readonly PdhCounter? _dedicatedUsage;
    private readonly PdhCounter? _sharedUsage;
    private readonly Dictionary<string, bool?> _unifiedMemoryByLuid = new(StringComparer.OrdinalIgnoreCase);
    private List<GpuAdapter> _adapters = [];
    private Dictionary<string, GpuSensor?> _sensors = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? _luidsAtEnumeration;

    /// <summary>Registers the GPU counters on the shared PDH query.</summary>
    /// <param name="query">Query collected once per sample by <see cref="HardwareMonitor"/>.</param>
    public GpuReader(PdhQuery query)
    {
        _engineUtilization = query.TryAddCounter(@"\GPU Engine(*)\Utilization Percentage");
        _dedicatedUsage = query.TryAddCounter(@"\GPU Adapter Memory(*)\Dedicated Usage");
        _sharedUsage = query.TryAddCounter(@"\GPU Adapter Memory(*)\Shared Usage");
    }

    /// <summary>Builds a snapshot with one entry per hardware GPU from the latest query sample.</summary>
    public GpuSnapshot Read()
    {
        IReadOnlyList<(string Instance, double Value)> engines = _engineUtilization?.GetInstanceValues() ?? [];
        IReadOnlyList<(string Instance, double Value)> dedicated = _dedicatedUsage?.GetInstanceValues(allowAbove100: true) ?? [];
        IReadOnlyList<(string Instance, double Value)> shared = _sharedUsage?.GetInstanceValues(allowAbove100: true) ?? [];

        // Adapters are listed again only when the set of LUIDs in the counters changes (driver update, eGPU plugged in).
        HashSet<string> luids = Luids(engines.Concat(dedicated));
        if (_luidsAtEnumeration is null || !_luidsAtEnumeration.SetEquals(luids))
        {
            _adapters = GpuAdapter.Enumerate(_unifiedMemoryByLuid, _dedicatedUsage is null ? null : Luids(dedicated));
            _luidsAtEnumeration = luids;
            CloseSensors();

            // One temperature sensor per adapter, kept open until the adapters change again.
            _sensors = _adapters.ToDictionary(
                adapter => adapter.LuidTag,
                adapter => GpuSensor.Open(adapter.LuidLowPart, adapter.LuidHighPart),
                StringComparer.OrdinalIgnoreCase);
        }

        return new GpuSnapshot([.. _adapters.Select(adapter => new GpuActivity(
            adapter.Id,
            adapter.Name,
            adapter.Rank,
            adapter.IsIntegrated,
            UsagePercent(engines, adapter.LuidTag),
            Sum(dedicated, adapter.LuidTag),
            adapter.DedicatedMemoryBytes,
            adapter.IsIntegrated ? Sum(shared, adapter.LuidTag) : null,
            _sensors.GetValueOrDefault(adapter.LuidTag)?.ReadTemperature()))]);
    }

    /// <summary>Closes the temperature sensors.</summary>
    public void Dispose() => CloseSensors();

    private void CloseSensors()
    {
        foreach (GpuSensor? sensor in _sensors.Values)
        {
            sensor?.Dispose();
        }

        _sensors.Clear();
    }

    /// <summary>Extracts the "luid_0x…_0x…" part of a counter instance name.</summary>
    /// <returns>The LUID tag, or <see langword="null"/> for names without one (e.g. "_Total").</returns>
    internal static string? LuidOf(string instance)
    {
        int start = instance.IndexOf(LuidMarker, StringComparison.OrdinalIgnoreCase);
        return start >= 0 && instance.Length >= start + LuidTagLength ? instance.Substring(start, LuidTagLength) : null;
    }

    /// <summary>Distinct LUID tags found in the counter values (case-insensitive).</summary>
    internal static HashSet<string> Luids(IEnumerable<(string Instance, double Value)> values) =>
        new(values.Select(value => LuidOf(value.Instance)).OfType<string>(), StringComparer.OrdinalIgnoreCase);

    /// <summary>GPU usage the way Task Manager computes it: the busiest engine, summing all processes on that engine.</summary>
    /// <returns>Usage from 0 to 100, or <see langword="null"/> when the adapter has no engine counters.</returns>
    internal static double? UsagePercent(IEnumerable<(string Instance, double Value)> engines, string luidTag)
    {
        var utilizationByEngine = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (instance, utilization) in engines)
        {
            if (!instance.Contains(luidTag, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>Sums the byte counters that belong to one adapter.</summary>
    /// <returns>Total bytes, or <see langword="null"/> when the adapter has no matching counter.</returns>
    internal static ulong? Sum(IEnumerable<(string Instance, double Value)> values, string luidTag)
    {
        double? total = null;

        foreach (var (instance, bytes) in values)
        {
            if (instance.Contains(luidTag, StringComparison.OrdinalIgnoreCase))
            {
                total = (total ?? 0) + bytes;
            }
        }

        return total is null ? null : (ulong)total.Value;
    }

    // "phys_0_eng_3" identifies one engine regardless of which process is using it.
    private static string? ExtractEngineKey(string instance)
    {
        int start = instance.IndexOf(PhysicalMarker, StringComparison.OrdinalIgnoreCase);
        int end = instance.IndexOf(EngineTypeMarker, StringComparison.OrdinalIgnoreCase);

        return start >= 0 && end > start ? instance[start..end] : null;
    }
}
