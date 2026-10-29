using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.App;

/// <summary>
/// Turns readings into the texts shown on the widget, in the details and in the tray.
/// Methods that take an optional value return <see cref="Unavailable"/> when it is missing; numbers follow the app language.
/// </summary>
internal static class DisplayFormat
{
    /// <summary>Shown in place of a value that can't be read.</summary>
    public const string Unavailable = "—";

    // Rounding limits for three significant digits: 9.995 would print "10.00", so it already gets one decimal.
    private const double TwoDecimalsBelow = 9.995;
    private const double OneDecimalBelow = 99.95;
    // 999.5 would round to "1000", so the next unit starts there.
    private const double NextUnitFrom = 999.5;
    // "99.9" is the widest three-digit text; used to reserve width so the widget doesn't resize.
    private const double WidestRate = 99.9;
    // Binary units: 1 KB = 1024 bytes.
    private const double BytesPerKilobyte = 1024d;
    private const double BytesPerMegabyte = 1024d * 1024;
    private const double BytesPerGigabyte = 1024d * 1024 * 1024;

    // Removed by ShortName so processor and GPU names fit on the widget.
    private static readonly string[] Trademarks = ["(R)", "(TM)", "®", "™"];
    private static readonly string[] GenericWords = ["Intel", "AMD", "NVIDIA", "CPU", "Processor"];

    /// <summary>Whole percentage, e.g. "42%".</summary>
    public static string Percent(double? value) =>
        value is null ? Unavailable : Localizer.Format("{0:0}%", value.Value);

    /// <summary>Link speed reported by the network adapter, in Mbps.</summary>
    public static string LinkRate(double? megabitsPerSecond) =>
        megabitsPerSecond is null ? Unavailable : Localizer.Format(Strings.Format_LinkRate, megabitsPerSecond.Value);

    /// <summary>Joins download and upload texts as "↓ down · ↑ up".</summary>
    public static string UpDown(string down, string up) => Localizer.Format(Strings.Format_UpDown, down, up);

    /// <summary>One value per line.</summary>
    public static string Lines(IReadOnlyList<string> values) => string.Join(Environment.NewLine, values);

    /// <summary>Translated name of a network device type.</summary>
    public static string DeviceKind(NetworkDeviceKind kind) => kind switch
    {
        NetworkDeviceKind.Ethernet => Strings.Kind_Ethernet,
        NetworkDeviceKind.WiFi => Strings.Kind_WiFi,
        NetworkDeviceKind.Bluetooth => Strings.Kind_Bluetooth,
        NetworkDeviceKind.Virtual => Strings.Kind_Virtual,
        NetworkDeviceKind.Cellular => Strings.Kind_Cellular,
        NetworkDeviceKind.Tunnel => Strings.Kind_Tunnel,
        NetworkDeviceKind.Loopback => Strings.Kind_Loopback,
        _ => Strings.Kind_Other,
    };

    /// <summary>Whole frames per second.</summary>
    public static string FramesPerSecond(double? value) =>
        value is null ? Unavailable : Localizer.Format("{0:0}", value.Value);

    /// <summary>Clock speed with two decimals, e.g. "3.42 GHz".</summary>
    public static string Gigahertz(double? value) =>
        value is null ? Unavailable : Localizer.Format(Strings.Format_Gigahertz, value.Value);

    /// <summary>Used and total memory in GB, e.g. "7.8 / 16.0 GB".</summary>
    public static string UsedOfTotal(ulong? usedBytes, ulong? totalBytes) =>
        usedBytes is null || totalBytes is null
            ? Unavailable
            : Localizer.Format(Strings.Format_UsedOfTotal, usedBytes.Value / BytesPerGigabyte, totalBytes.Value / BytesPerGigabyte);

    /// <summary>Amount in GB with one decimal.</summary>
    public static string Gigabytes(ulong? bytes) =>
        bytes is null ? Unavailable : Localizer.Format(Strings.Format_Gigabytes, bytes.Value / BytesPerGigabyte);

    /// <summary>Short GPU memory text for the widget: total used on integrated GPUs, dedicated used/total on the others.</summary>
    public static string GpuMemory(GpuActivity? gpu) => gpu switch
    {
        null => Unavailable,
        { IsIntegrated: true } => Gigabytes(gpu.TotalUsedBytes),
        _ => UsedOfTotal(gpu.DedicatedUsedBytes, gpu.DedicatedTotalBytes),
    };

    /// <summary>GPU memory text for the details; integrated GPUs show dedicated and shared memory separately.</summary>
    public static string GpuMemoryDetail(GpuActivity? gpu) => gpu switch
    {
        null => Unavailable,
        { IsIntegrated: true, DedicatedTotalBytes: 0 } => Localizer.Format(Strings.Format_GpuSharedMemory, Gigabytes(gpu.SharedUsedBytes)),
        { IsIntegrated: true } => Localizer.Format(Strings.Format_GpuIntegratedMemory, UsedOfTotal(gpu.DedicatedUsedBytes, gpu.DedicatedTotalBytes), Gigabytes(gpu.SharedUsedBytes)),
        _ => Localizer.Format(Strings.Format_GpuDedicatedMemory, UsedOfTotal(gpu.DedicatedUsedBytes, gpu.DedicatedTotalBytes)),
    };

    /// <summary>Whole degrees Celsius, e.g. "47 °C".</summary>
    public static string Temperature(double? celsius) =>
        celsius is null ? Unavailable : Localizer.Format(Strings.Format_Celsius, celsius.Value);

    /// <summary>Disk read and write speeds; each value picks its own unit (KB/s, MB/s or GB/s).</summary>
    public static string DiskRates(double? readBytesPerSecond, double? writeBytesPerSecond)
    {
        if (readBytesPerSecond is null || writeBytesPerSecond is null)
        {
            return Unavailable;
        }

        return Localizer.Format(Strings.Format_DiskRates, DiskRate(readBytesPerSecond.Value), DiskRate(writeBytesPerSecond.Value));
    }

    /// <summary>Widest possible <see cref="DiskRates"/> text, used to reserve width on the widget.</summary>
    public static string DiskRatesWidest
    {
        get
        {
            string widest = Localizer.Format(Strings.Format_MegabytesPerSecond, ThreeDigits(WidestRate));
            return Localizer.Format(Strings.Format_DiskRates, widest, widest);
        }
    }

    // Moves to the next unit once the value would need four digits.
    private static string DiskRate(double bytesPerSecond)
    {
        if (bytesPerSecond / BytesPerKilobyte < NextUnitFrom)
        {
            return Localizer.Format(Strings.Format_KilobytesPerSecond, ThreeDigits(bytesPerSecond / BytesPerKilobyte));
        }

        return bytesPerSecond / BytesPerMegabyte < NextUnitFrom
            ? Localizer.Format(Strings.Format_MegabytesPerSecond, ThreeDigits(bytesPerSecond / BytesPerMegabyte))
            : Localizer.Format(Strings.Format_GigabytesPerSecond, ThreeDigits(bytesPerSecond / BytesPerGigabyte));
    }

    // Three significant digits: 1.23, 12.3, 123. Zero stays "0".
    private static string ThreeDigits(double value) => Localizer.Format(
        value switch
        {
            0d => "{0:0}",
            < TwoDecimalsBelow => "{0:0.00}",
            < OneDecimalBelow => "{0:0.0}",
            _ => "{0:0}",
        },
        value);

    /// <summary>Battery charge, e.g. "80%".</summary>
    public static string BatteryPercent(BatterySnapshot? battery) =>
        battery?.Percent is int percent ? Localizer.Format("{0}%", percent) : Unavailable;

    /// <summary>Charging, plugged in, time left, or just "on battery" when Windows has no estimate.</summary>
    public static string BatteryState(BatterySnapshot? battery) => battery switch
    {
        null => Unavailable,
        { IsCharging: true } => Strings.Battery_Charging,
        { OnPower: true } => Strings.Battery_OnPower,
        { Remaining: TimeSpan left } => Localizer.Format(Strings.Format_BatteryRemaining, (int)left.TotalHours, left.Minutes),
        _ => Strings.Battery_OnBattery,
    };

    /// <summary>Speed test result in Mbps, or Gbps from 1000 Mbps.</summary>
    public static string Bandwidth(double bitsPerSecond) => MegabitRate(bitsPerSecond / 1_000_000);

    /// <summary>Number of <see cref="Bandwidth"/> without the unit, for the speed test gauge.</summary>
    public static string BandwidthNumber(double bitsPerSecond)
    {
        double megabits = bitsPerSecond / 1_000_000;
        return ThreeDigits(megabits < NextUnitFrom ? megabits : megabits / 1000);
    }

    /// <summary>Unit of <see cref="BandwidthNumber"/>: Mbps, or Gbps from 1000 Mbps.</summary>
    public static string BandwidthUnit(double bitsPerSecond) =>
        bitsPerSecond / 1_000_000 < NextUnitFrom ? Strings.Unit_Megabits : Strings.Unit_Gigabits;

    /// <summary>Latency in whole milliseconds.</summary>
    public static string Milliseconds(double? value) =>
        value is null ? Unavailable : Localizer.Format(Strings.Format_Milliseconds, value.Value);

    /// <summary>Network speed in Mbps without the unit, for the tray tooltip.</summary>
    public static string Megabits(double? bytesPerSecond) =>
        bytesPerSecond is null ? Unavailable : ThreeDigits(bytesPerSecond.Value * 8 / 1_000_000);

    /// <summary>Network speed in Mbps, or Gbps from 1000 Mbps.</summary>
    public static string NetworkRate(double? bytesPerSecond)
    {
        if (bytesPerSecond is null)
        {
            return Unavailable;
        }

        return MegabitRate(bytesPerSecond.Value * 8 / 1_000_000);
    }

    private static string MegabitRate(double megabits) =>
        megabits < NextUnitFrom
            ? Localizer.Format(Strings.Format_NetworkRate, ThreeDigits(megabits))
            : Localizer.Format(Strings.Format_NetworkRateGigabits, ThreeDigits(megabits / 1000));

    /// <summary>Widest possible <see cref="NetworkRate"/> text, used to reserve width on the widget.</summary>
    public static string NetworkRateWidest => Localizer.Format(Strings.Format_NetworkRate, ThreeDigits(WidestRate));

    /// <summary>Data amount in the largest unit that keeps it under four digits (B, KB, MB, GB or TB).</summary>
    public static string Size(ulong bytes)
    {
        string[] units = [Strings.Unit_Bytes, Strings.Unit_Kilobytes, Strings.Unit_Megabytes, Strings.Unit_Gigabytes, Strings.Unit_Terabytes];
        double value = bytes;
        int unit = 0;

        while (value >= NextUnitFrom && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return Localizer.Format(Strings.Format_Size, unit == 0 ? Localizer.Format("{0}", bytes) : ThreeDigits(value), units[unit]);
    }

    /// <summary>Speed test transfer size in decimal MB or KB.</summary>
    public static string TransferSize(long bytes) =>
        bytes >= 1_000_000
            ? Localizer.Format(Strings.Format_Size, bytes / 1_000_000, Strings.Unit_Megabytes)
            : Localizer.Format(Strings.Format_Size, bytes / 1_000, Strings.Unit_Kilobytes);

    /// <summary>Disk number followed by its drive letters, when it has any.</summary>
    public static string DiskLabel(DiskActivity disk) =>
        disk.Letters.Length == 0
            ? Localizer.Format("{0}", disk.Number)
            : Localizer.Format("{0} ({1})", disk.Number, disk.Letters);

    /// <summary>Shortens a CPU or GPU name for the widget by dropping the clock suffix, trademarks and generic words such as "Processor".</summary>
    public static string ShortName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Unavailable;
        }

        string name = fullName;

        // Intel names end with "@ 3.60GHz".
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
        // If nothing is left, the full name is better than nothing.
        return shortName.Length == 0 ? fullName.Trim() : shortName;
    }
}
