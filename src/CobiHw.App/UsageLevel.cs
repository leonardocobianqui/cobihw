using CobiHw.App.Settings;

namespace CobiHw.App;

/// <summary>Color level of a value.</summary>
public enum UsageLevel
{
    /// <summary>Usage colors are off, or there is no reading.</summary>
    None,
    /// <summary>Below the warning threshold.</summary>
    Normal,
    /// <summary>At or above the warning threshold.</summary>
    Warning,
    /// <summary>At or above the critical threshold.</summary>
    Critical,
}

/// <summary>Decides the usage level of each value.</summary>
internal static class UsageLevels
{
    /// <summary>Battery at or below this charge is a warning.</summary>
    public const int BatteryWarningPercent = 20;
    /// <summary>Battery at or below this charge is critical.</summary>
    public const int BatteryCriticalPercent = 10;

    /// <summary>Battery level: lower charge is worse.</summary>
    /// <param name="percent">Charge, or null.</param>
    /// <param name="enabled">Whether usage colors are on.</param>
    public static UsageLevel ClassifyBattery(int? percent, bool enabled) => percent switch
    {
        _ when !enabled => UsageLevel.None,
        null => UsageLevel.None,
        <= BatteryCriticalPercent => UsageLevel.Critical,
        <= BatteryWarningPercent => UsageLevel.Warning,
        _ => UsageLevel.Normal,
    };

    /// <summary>Level of a usage value against its thresholds.</summary>
    /// <param name="percent">Usage, or null.</param>
    /// <param name="thresholds">Warning and critical limits.</param>
    /// <param name="enabled">Whether usage colors are on.</param>
    public static UsageLevel Classify(double? percent, UsageThresholds thresholds, bool enabled)
    {
        if (!enabled || percent is null)
        {
            return UsageLevel.None;
        }

        // Compares the rounded value, so the color matches the number on screen.
        double displayed = Math.Round(percent.Value, MidpointRounding.AwayFromZero);

        if (displayed >= thresholds.Critical)
        {
            return UsageLevel.Critical;
        }

        return displayed >= thresholds.Warning ? UsageLevel.Warning : UsageLevel.Normal;
    }
}
