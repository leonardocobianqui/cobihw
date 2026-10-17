using CobiHw.App.Localization;
using CobiHw.App.Resources;

namespace CobiHw.App.Settings;

public static class AppearanceSummary
{
    public static string Shape(AppearanceSettings appearance) =>
        Localizer.Format(
            Strings.Format_ShapeSummary,
            ResizeRule.ClampPercent(appearance.Scale),
            CornerRule.Clamp(appearance.CornerRadius),
            Math.Clamp(appearance.BarRadius, 0, AppearanceSettings.MaximumBarRadius),
            Border(appearance));

    public static string TextColors(AppearanceSettings appearance) =>
        Localizer.Format(
            Strings.Format_TextColorsSummary,
            HexColor.Normalize(appearance.BackgroundColor, AppearanceSettings.DefaultBackgroundColor),
            Math.Clamp(appearance.Transparency, 0, AppearanceSettings.MaximumTransparency),
            HexColor.Normalize(appearance.TextColor, AppearanceSettings.DefaultTextColor),
            Lower(WeightLabel(appearance.TextWeight)));

    public static string UsageColors(ColorSettings colors) =>
        colors.Enabled
            ? Localizer.Format(Strings.Format_UsageSummary, string.Join(" · ", new (MetricItem Item, UsageThresholds Thresholds)[]
            {
                (MetricItem.Cpu, colors.Cpu),
                (MetricItem.Gpu, colors.Gpu),
                (MetricItem.Memory, colors.Memory),
                (MetricItem.Disk, colors.Disk),
            }.Select(entry => Localizer.Format(Strings.Format_UsageThreshold, MetricRowViewModel.NameOf(entry.Item), entry.Thresholds.Warning, entry.Thresholds.Critical))))
            : Strings.Appearance_UsageOff;

    public static string WeightLabel(TextWeight weight) => weight switch
    {
        TextWeight.Light => Strings.Weight_Light,
        TextWeight.Medium => Strings.Weight_Medium,
        TextWeight.SemiBold => Strings.Weight_SemiBold,
        TextWeight.Bold => Strings.Weight_Bold,
        _ => Strings.Weight_Normal,
    };

    private static string Border(AppearanceSettings appearance)
    {
        int thickness = Math.Clamp(appearance.BorderThickness, AppearanceSettings.MinimumBorderThickness, AppearanceSettings.MaximumBorderThickness);

        return appearance.BorderMode switch
        {
            BorderMode.Always => Localizer.Format(Strings.Format_BorderSummary, thickness, Lower(Strings.BorderMode_Always)),
            BorderMode.Hover => Localizer.Format(Strings.Format_BorderSummary, thickness, Lower(Strings.BorderMode_Hover)),
            _ => Strings.Appearance_NoBorder,
        };
    }

    private static string Lower(string text) => text.ToLower(Localizer.Instance.Culture);
}
