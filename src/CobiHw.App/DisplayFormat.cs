using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core;

namespace CobiHw.App;

internal static class DisplayFormat
{
    public const string Unavailable = "—";

    private const double BytesPerGigabyte = 1024d * 1024 * 1024;

    private static readonly string[] Trademarks = ["(R)", "(TM)", "®", "™"];
    private static readonly string[] GenericWords = ["Intel", "AMD", "NVIDIA", "CPU", "Processor"];

    public static PercentText PercentParts(double? value) =>
        value is null ? new PercentText(Unavailable, string.Empty) : new PercentText(Localizer.Format("{0:0}", value.Value), "%");

    public static string Percent(double? value) => PercentParts(value).Text;

    public static string Gigahertz(double? value) =>
        value is null ? Unavailable : Localizer.Format(Strings.Format_Gigahertz, value.Value);

    public static string UsedOfTotal(ulong? usedBytes, ulong? totalBytes) =>
        usedBytes is null || totalBytes is null
            ? Unavailable
            : Localizer.Format(Strings.Format_UsedOfTotal, usedBytes.Value / BytesPerGigabyte, totalBytes.Value / BytesPerGigabyte);

    public static string Bandwidth(double bitsPerSecond) =>
        Localizer.Format(Strings.Format_Bandwidth, bitsPerSecond / 1_000_000);

    public static string Milliseconds(double? value) =>
        value is null ? Unavailable : Localizer.Format(Strings.Format_Milliseconds, value.Value);

    public static string MegabitsPerSecond(double? bytesPerSecond) =>
        bytesPerSecond is null ? Unavailable : Localizer.Format(Strings.Format_NetworkRate, bytesPerSecond.Value * 8 / 1_000_000);

    public static string Size(ulong bytes)
    {
        string[] units = [Strings.Unit_Bytes, Strings.Unit_Kilobytes, Strings.Unit_Megabytes, Strings.Unit_Gigabytes, Strings.Unit_Terabytes];
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return Localizer.Format(Strings.Format_Size, value, units[unit]);
    }

    public static string TransferSize(long bytes) =>
        bytes >= 1_000_000
            ? Localizer.Format(Strings.Format_Size, bytes / 1_000_000, Strings.Unit_Megabytes)
            : Localizer.Format(Strings.Format_Size, bytes / 1_000, Strings.Unit_Kilobytes);

    public static string DiskLabel(DiskActivity disk) =>
        disk.Letters.Length == 0
            ? Localizer.Format("{0}", disk.Number)
            : Localizer.Format("{0} ({1})", disk.Number, disk.Letters);

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
