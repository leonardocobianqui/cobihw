using CobiHw.App.Settings;

namespace CobiHw.App;

public enum UsageLevel
{
    None,
    Normal,
    Warning,
    Critical,
}

internal static class UsageLevels
{
    public static UsageLevel Classify(double? percent, UsageThresholds thresholds, bool enabled)
    {
        if (!enabled || percent is null)
        {
            return UsageLevel.None;
        }

        double displayed = Math.Round(percent.Value, MidpointRounding.AwayFromZero);

        if (displayed >= thresholds.Critical)
        {
            return UsageLevel.Critical;
        }

        return displayed >= thresholds.Warning ? UsageLevel.Warning : UsageLevel.Normal;
    }
}
