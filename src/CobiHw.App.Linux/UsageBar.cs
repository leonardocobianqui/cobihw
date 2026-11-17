using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CobiHw.App.Linux;

/// <summary>
/// The thin usage bar of the full layout: a rounded track with a fill for the value, 0 to 100.
/// </summary>
/// <remarks>Drawn directly rather than by restyling ProgressBar, whose Fluent template has animations and minimum sizes the widget doesn't want.</remarks>
public sealed class UsageBar : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<UsageBar, double>(nameof(Value));
    public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<UsageBar, IBrush?>(nameof(Track));
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<UsageBar, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<double> RadiusProperty = AvaloniaProperty.Register<UsageBar, double>(nameof(Radius));

    static UsageBar() => AffectsRender<UsageBar>(ValueProperty, TrackProperty, FillProperty, RadiusProperty);

    /// <summary>Usage, 0 to 100.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Empty part of the bar.</summary>
    public IBrush? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    /// <summary>Filled part, colored by usage level.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Corner radius of the track and the fill.</summary>
    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        double radius = Math.Min(Radius, bounds.Height / 2);
        context.DrawRectangle(Track, null, bounds, radius, radius);

        double width = bounds.Width * Math.Clamp(Value, 0, 100) / 100;
        if (width > 0)
        {
            context.DrawRectangle(Fill, null, new Rect(0, 0, width, bounds.Height), radius, radius);
        }
    }
}
