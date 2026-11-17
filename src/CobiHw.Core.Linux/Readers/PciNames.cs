namespace CobiHw.Core.Linux.Readers;

/// <summary>
/// GPU names from the PCI ID database (pci.ids), the same names lspci shows.
/// </summary>
/// <remarks>
/// Linux has no marketing name from the driver, as Windows has. pci.ids comes with pciutils or hwdata on practically
/// every distribution; where it's missing, the name falls back to the vendor and the device id.
/// </remarks>
internal static class PciNames
{
    // Where distributions install the database: hwdata (Fedora, Arch, recent Debian and Ubuntu) and pciutils (older ones).
    private static readonly string[] DatabasePaths = ["usr/share/hwdata/pci.ids", "usr/share/misc/pci.ids"];

    // The database names vendors by company ("Advanced Micro Devices, Inc. [AMD/ATI]"); these are the names people know.
    private static readonly Dictionary<uint, string> ShortVendorNames = new()
    {
        [0x1002] = "AMD",
        [0x10DE] = "NVIDIA",
        [0x8086] = "Intel",
    };

    /// <summary>Names of the given devices, e.g. "AMD Mendocino" or "NVIDIA GeForce RTX 3060 Mobile / Max-Q".</summary>
    /// <param name="files">Where to find the database.</param>
    /// <param name="devices">Vendor and device ids to name.</param>
    public static Dictionary<(uint Vendor, uint Device), string> Find(SystemFiles files, IReadOnlyCollection<(uint Vendor, uint Device)> devices)
    {
        string? database = DatabasePaths.Select(files.Text).FirstOrDefault(text => text is not null);
        Dictionary<(uint, uint), string> found = Parse(database, devices);

        foreach ((uint vendor, uint device) in devices)
        {
            found.TryAdd((vendor, device), $"{ShortVendorNames.GetValueOrDefault(vendor) ?? $"{vendor:X4}"} GPU {device:X4}");
        }

        return found;
    }

    /// <summary>Names of the given devices found in the text of pci.ids.</summary>
    /// <remarks>
    /// Vendor lines start at the margin ("1002  Advanced Micro Devices…"), their devices follow indented by one tab
    /// ("\t1506  Mendocino"), subsystems by two. The device classes at the end of the file ("C 03  Display controller")
    /// are a different list and end the search.
    /// </remarks>
    internal static Dictionary<(uint, uint), string> Parse(string? database, IReadOnlyCollection<(uint Vendor, uint Device)> devices)
    {
        var found = new Dictionary<(uint, uint), string>();
        HashSet<uint> vendors = [.. devices.Select(device => device.Vendor)];
        uint? vendor = null;
        string vendorName = string.Empty;

        foreach (string line in (database ?? string.Empty).Split('\n'))
        {
            if (line.StartsWith("C ", StringComparison.Ordinal))
            {
                break;
            }

            if (line.Length < 6 || line[0] == '#' || line.StartsWith("\t\t", StringComparison.Ordinal))
            {
                continue;
            }

            if (line[0] != '\t')
            {
                vendor = TryHex(line[..4]) is uint id && vendors.Contains(id) ? id : null;
                vendorName = vendor is uint known ? ShortVendorNames.GetValueOrDefault(known) ?? Marketing(line[4..].Trim()) : string.Empty;
                continue;
            }

            if (vendor is uint current && TryHex(line[1..5]) is uint device && devices.Contains((current, device)))
            {
                found[(current, device)] = $"{vendorName} {Marketing(line[5..].Trim())}";
            }
        }

        return found;
    }

    // "Navi 21 [Radeon RX 6800/6800 XT / 6900 XT]": the bracketed part is the name on the box; the rest is the chip's codename.
    private static string Marketing(string name)
    {
        int open = name.LastIndexOf('[');
        return open >= 0 && name.EndsWith(']') ? name[(open + 1)..^1] : name;
    }

    private static uint? TryHex(string text) =>
        uint.TryParse(text, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out uint value) ? value : null;
}
