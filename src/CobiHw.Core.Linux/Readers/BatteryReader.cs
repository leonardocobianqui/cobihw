using System.Globalization;

namespace CobiHw.Core.Linux.Readers;

/// <summary>
/// Reads the system battery from the power supply class in /sys/class/power_supply.
/// </summary>
/// <remarks>
/// Each supply's "uevent" file lists all its properties at once (POWER_SUPPLY_CAPACITY=87…). Batteries of wireless mice
/// and gamepads show up here too, with SCOPE=Device, and are left out. Laptops with two batteries are combined into one
/// reading, as Windows does.
/// </remarks>
internal static class BatteryReader
{
    /// <summary>Reads the current battery state.</summary>
    /// <returns>The battery state, or <see langword="null"/> when the machine has no system battery.</returns>
    public static BatterySnapshot? Read(SystemFiles files) =>
        Describe([.. files.Entries("sys/class/power_supply").Select(name => ParseUevent(files.Text($"sys/class/power_supply/{name}/uevent")))]);

    /// <summary>Properties of one supply from its uevent text, without the POWER_SUPPLY_ prefix.</summary>
    internal static Dictionary<string, string> ParseUevent(string? uevent)
    {
        const string Prefix = "POWER_SUPPLY_";
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in (uevent ?? string.Empty).Split('\n'))
        {
            string[] parts = line.Split('=', 2);
            if (parts.Length == 2 && parts[0].StartsWith(Prefix, StringComparison.Ordinal))
            {
                properties[parts[0][Prefix.Length..]] = parts[1].Trim();
            }
        }

        return properties;
    }

    /// <summary>Combines the supplies into the system battery state.</summary>
    /// <param name="supplies">Properties of every power supply.</param>
    /// <returns>The battery state, or <see langword="null"/> when none of them is a present system battery.</returns>
    /// <remarks>
    /// Drivers report either energy (µWh, with power in µW) or charge (µAh, with current in µA). One battery's percent is
    /// its CAPACITY, the figure the desktop shows; two batteries are combined from those amounts, so each weighs by its size.
    /// Time left is only estimated while discharging, like Windows: TIME_TO_EMPTY_NOW when the driver has it, otherwise
    /// amount ÷ rate.
    /// </remarks>
    internal static BatterySnapshot? Describe(IReadOnlyList<IReadOnlyDictionary<string, string>> supplies)
    {
        List<IReadOnlyDictionary<string, string>> batteries =
        [
            .. supplies.Where(supply =>
                supply.GetValueOrDefault("TYPE") == "Battery" &&
                supply.GetValueOrDefault("PRESENT") != "0" &&
                supply.GetValueOrDefault("SCOPE") != "Device"),
        ];

        if (batteries.Count == 0)
        {
            return null;
        }

        string[] statuses = [.. batteries.Select(battery => battery.GetValueOrDefault("STATUS") ?? "Unknown")];
        bool discharging = statuses.Contains("Discharging");
        bool[] mains =
        [
            .. supplies
                .Where(supply => supply.GetValueOrDefault("TYPE") is "Mains" or "USB" or "USB_C" or "USB_PD")
                .Select(supply => supply.GetValueOrDefault("ONLINE") == "1"),
        ];

        // Without a mains supply to ask (some ARM boards), the battery status tells whether it's plugged in.
        bool onPower = mains.Length > 0 ? mains.Contains(true) : !discharging;

        return new BatterySnapshot(
            Percent(batteries),
            statuses.Contains("Charging"),
            onPower,
            discharging && !onPower ? Remaining(batteries) : null);
    }

    private static int? Percent(List<IReadOnlyDictionary<string, string>> batteries)
    {
        if (batteries.Count == 1 && Number(batteries[0], "CAPACITY") is double capacity)
        {
            return (int)Math.Clamp(Math.Round(capacity), 0, 100);
        }

        foreach (string unit in (string[])["ENERGY", "CHARGE"])
        {
            double?[] now = [.. batteries.Select(battery => Number(battery, $"{unit}_NOW"))];
            double?[] full = [.. batteries.Select(battery => Number(battery, $"{unit}_FULL"))];
            if (now.All(value => value is not null) && full.All(value => value > 0))
            {
                return (int)Math.Clamp(Math.Round(now.Sum()!.Value / full.Sum()!.Value * 100), 0, 100);
            }
        }

        double?[] capacities = [.. batteries.Select(battery => Number(battery, "CAPACITY"))];
        return capacities.All(value => value is not null) ? (int)Math.Clamp(Math.Round(capacities.Average()!.Value), 0, 100) : null;
    }

    private static TimeSpan? Remaining(List<IReadOnlyDictionary<string, string>> batteries)
    {
        double?[] reported = [.. batteries.Select(battery => Number(battery, "TIME_TO_EMPTY_NOW"))];
        if (reported.All(value => value > 0))
        {
            return TimeSpan.FromSeconds(reported.Sum()!.Value);
        }

        foreach ((string amount, string rate) in ((string, string)[])[("ENERGY_NOW", "POWER_NOW"), ("CHARGE_NOW", "CURRENT_NOW")])
        {
            double?[] amounts = [.. batteries.Select(battery => Number(battery, amount))];
            // Some drivers report the current as negative while discharging.
            double?[] rates = [.. batteries.Select(battery => Number(battery, rate) is double value ? Math.Abs(value) : (double?)null)];
            if (amounts.All(value => value is not null) && rates.All(value => value is not null) && rates.Sum() > 0)
            {
                return TimeSpan.FromHours(amounts.Sum()!.Value / rates.Sum()!.Value);
            }
        }

        return null;
    }

    private static double? Number(IReadOnlyDictionary<string, string> supply, string key) =>
        double.TryParse(supply.GetValueOrDefault(key), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double value) ? value : null;
}
