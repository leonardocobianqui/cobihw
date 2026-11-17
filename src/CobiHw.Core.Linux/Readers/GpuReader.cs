using System.Globalization;

namespace CobiHw.Core.Linux.Readers;

/// <summary>
/// Reads every GPU from /sys/class/drm: identity, name, usage, memory and temperature.
/// </summary>
/// <remarks>
/// amdgpu publishes whole-GPU usage and memory in sysfs. Intel and the other drivers don't, so their numbers come from the
/// per-process client statistics (<see cref="DrmClients"/>). NVIDIA's proprietary driver publishes neither: its GPUs are
/// listed, with usage and memory unknown. GPUs that aren't PCI devices (most ARM boards) aren't listed, since the GPU
/// identity is the PCI vendor and device.
/// </remarks>
internal sealed class GpuReader(SystemFiles files)
{
    private const uint Amd = 0x1002;
    private const uint Intel = 0x8086;

    // Intel puts its integrated GPU at this PCI address on every platform; discrete Arc cards sit elsewhere.
    private const string IntelIntegratedSlot = "0000:00:02.0";

    private readonly DrmClients _clients = new(files);
    private readonly Dictionary<(uint, uint), string> _names = [];

    /// <summary>Reads every GPU now.</summary>
    /// <param name="timestamp"><see cref="System.Diagnostics.Stopwatch"/> timestamp of the sample.</param>
    public GpuSnapshot Read(long timestamp)
    {
        List<Card> cards = Discover();
        Name(cards);

        HashSet<string> fromClients = [.. cards.Where(card => card.Driver is not ("amdgpu" or "nvidia")).Select(card => card.Slot)];
        Dictionary<string, DrmUsage> clients = fromClients.Count > 0 ? _clients.Read(fromClients, timestamp) : [];

        List<GpuActivity> gpus = [.. cards.Select(card => Describe(card, clients.GetValueOrDefault(card.Slot)))];

        // Linux has no system-wide "high performance" order like Windows: a dedicated GPU comes before an integrated
        // one, which matches what Windows picks on a laptop with both; PCI order breaks ties.
        List<GpuActivity> ranked = [.. gpus.OrderBy(gpu => gpu.IsIntegrated)];
        return new GpuSnapshot([.. gpus.Select(gpu => gpu with { Rank = ranked.IndexOf(gpu) })]);
    }

    /// <summary>True when the GPU shares system RAM instead of having its own.</summary>
    /// <remarks>
    /// Intel's integrated GPU is always at 00:02.0. amdgpu reports who made the VRAM chips (mem_info_vram_vendor) only
    /// when there are chips: an APU's "VRAM" is a slice of system RAM, so it has none. Other drivers count as dedicated.
    /// </remarks>
    internal static bool IsIntegrated(uint vendor, string slot, string driver, bool hasVramVendor) => vendor switch
    {
        Intel => slot == IntelIntegratedSlot,
        Amd when driver == "amdgpu" => !hasVramVendor,
        _ => false,
    };

    private GpuActivity Describe(Card card, DrmUsage? clients)
    {
        string device = $"sys/class/drm/{card.Folder}/device";
        bool integrated = IsIntegrated(card.Vendor, card.Slot, card.Driver, files.Exists($"{device}/mem_info_vram_vendor"));
        var id = new GpuId(card.Vendor, card.Device, card.Instance);
        string name = _names[(card.Vendor, card.Device)];

        // Reading sysfs power attributes can wake a dedicated GPU that the kernel powered down to save battery.
        // A suspended GPU is idle by definition, so nothing more is read.
        if (files.Text($"{device}/power/runtime_status") == "suspended")
        {
            return new GpuActivity(id, name, 0, integrated, 0, null, 0, null);
        }

        if (card.Driver == "amdgpu")
        {
            return new GpuActivity(
                id,
                name,
                0,
                integrated,
                files.Number($"{device}/gpu_busy_percent") is long busy ? Math.Clamp(busy, 0, 100) : null,
                Bytes($"{device}/mem_info_vram_used"),
                Bytes($"{device}/mem_info_vram_total") ?? 0,
                integrated ? Bytes($"{device}/mem_info_gtt_used") : null,
                Temperature(device));
        }

        return new GpuActivity(
            id,
            name,
            0,
            integrated,
            clients?.UsagePercent,
            clients?.LocalBytes,
            0,
            integrated ? clients?.SystemBytes : null,
            Temperature(device));
    }

    /// <summary>The GPUs in /sys/class/drm, in PCI order, numbered among identical models.</summary>
    /// <remarks>
    /// Each GPU is a "cardN" folder; its outputs ("card1-eDP-1") and render nodes are skipped. The number isn't
    /// meaningful: on recent kernels the firmware framebuffer can hold card0 until the real driver takes over.
    /// </remarks>
    private List<Card> Discover()
    {
        var cards = new List<Card>();
        foreach (string folder in files.Entries("sys/class/drm").Where(IsCardFolder))
        {
            string device = $"sys/class/drm/{folder}/device";
            Dictionary<string, string> uevent = Uevent(files.Text($"{device}/uevent"));
            if (Hex(files.Text($"{device}/vendor")) is uint vendor &&
                Hex(files.Text($"{device}/device")) is uint model &&
                files.Text($"{device}/class")?.StartsWith("0x03", StringComparison.Ordinal) == true &&
                uevent.GetValueOrDefault("PCI_SLOT_NAME") is string slot &&
                cards.All(card => card.Slot != slot))
            {
                cards.Add(new Card(folder, vendor, model, slot, uevent.GetValueOrDefault("DRIVER") ?? string.Empty, 0));
            }
        }

        cards.Sort((left, right) => string.CompareOrdinal(left.Slot, right.Slot));
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i] = cards[i] with { Instance = cards.Take(i).Count(card => card.Vendor == cards[i].Vendor && card.Device == cards[i].Device) };
        }

        return cards;
    }

    // pci.ids is large, so it's only read when a GPU not named yet appears: at start, or when an eGPU is plugged in.
    private void Name(List<Card> cards)
    {
        (uint, uint)[] missing = [.. cards.Select(card => (card.Vendor, card.Device)).Distinct().Where(ids => !_names.ContainsKey(ids))];
        if (missing.Length > 0)
        {
            foreach ((var ids, string name) in PciNames.Find(files, missing))
            {
                _names[ids] = name;
            }
        }
    }

    /// <summary>GPU temperature in °C from the device's hwmon sensors; the "edge" sensor (the die) when there are several.</summary>
    private double? Temperature(string device)
    {
        foreach (string hwmon in files.Entries($"{device}/hwmon"))
        {
            string folder = $"{device}/hwmon/{hwmon}";
            string[] inputs = [.. files.Entries(folder).Where(entry => entry.StartsWith("temp", StringComparison.Ordinal) && entry.EndsWith("_input", StringComparison.Ordinal))];
            string? edge = inputs.FirstOrDefault(input => files.Text($"{folder}/{input.Replace("_input", "_label", StringComparison.Ordinal)}") == "edge");
            string? chosen = edge ?? inputs.FirstOrDefault(input => input == "temp1_input") ?? inputs.FirstOrDefault();

            // hwmon reports millidegrees.
            if (chosen is not null && files.Number($"{folder}/{chosen}") is long millidegrees)
            {
                return millidegrees / 1000.0;
            }
        }

        return null;
    }

    private ulong? Bytes(string path) => files.Number(path) is long value and >= 0 ? (ulong)value : null;

    private static bool IsCardFolder(string name) =>
        name.Length > 4 && name.StartsWith("card", StringComparison.Ordinal) && name[4..].All(char.IsAsciiDigit);

    private static uint? Hex(string? text) =>
        text is not null && text.StartsWith("0x", StringComparison.Ordinal) &&
        uint.TryParse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value)
            ? value
            : null;

    private static Dictionary<string, string> Uevent(string? text) =>
        (text ?? string.Empty)
            .Split('\n')
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0])
            .ToDictionary(group => group.Key, group => group.First()[1].Trim());

    private sealed record Card(string Folder, uint Vendor, uint Device, string Slot, string Driver, int Instance);
}
