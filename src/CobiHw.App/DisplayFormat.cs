using CobiHw.Core;

namespace CobiHw.App;

internal static class DisplayFormat
{
    public const string Unavailable = "—";

    private const double BytesPerGigabyte = 1024d * 1024 * 1024;

    private static readonly string[] Trademarks = ["(R)", "(TM)", "®", "™"];
    private static readonly string[] GenericWords = ["Intel", "AMD", "NVIDIA", "CPU", "Processor"];
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB"];

    public static PercentText PercentParts(double? value) =>
        value is null ? new PercentText(Unavailable, string.Empty) : new PercentText($"{value.Value:0}", "%");

    public static string Percent(double? value) => PercentParts(value).Text;

    public static string Gigahertz(double? value) =>
        value is null ? Unavailable : $"{value.Value:0.00} GHz";

    public static string UsedOfTotal(ulong? usedBytes, ulong? totalBytes) =>
        usedBytes is null || totalBytes is null
            ? Unavailable
            : $"{usedBytes.Value / BytesPerGigabyte:0.0} / {totalBytes.Value / BytesPerGigabyte:0.0} GB";

    public static string MegabitsPerSecond(double? bytesPerSecond) =>
        bytesPerSecond is null ? Unavailable : $"{bytesPerSecond.Value * 8 / 1_000_000:0.00} Mbps";

    public static string Size(ulong bytes)
    {
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < SizeUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {SizeUnits[unit]}";
    }

    public static string DiskLabel(DiskActivity disk) =>
        disk.Letters.Length == 0 ? $"{disk.Number}" : $"{disk.Number} ({disk.Letters})";

    public static string ShortName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Unavailable;
        }

        string name = fullName;

        int frequencySuffix = name.IndexOf('@');
        if (frequencySuffix >= 0)
        {
            name = name[..frequencySuffix];
        }

        foreach (string trademark in Trademarks)
        {
            name = name.Replace(trademark, " ", StringComparison.OrdinalIgnoreCase);
        }

        IEnumerable<string> words = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => !GenericWords.Contains(word, StringComparer.OrdinalIgnoreCase));

        string shortName = string.Join(' ', words);
        return shortName.Length == 0 ? fullName.Trim() : shortName;
    }
}
