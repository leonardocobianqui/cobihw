using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CobiHw.App;

/// <summary>Speed test gauge: a 270° arc on the <see cref="SpeedGaugeScale"/> with a needle, like Ookla's Speedtest.</summary>
/// <remarks>
/// The needle slides to each new value; with Windows animations off, it jumps there.
/// Sized by its width: the arc ends at about 0.83 × width from the top.
/// </remarks>
public sealed class SpeedGauge : FrameworkElement
{
    // The arc starts at the bottom left (135°, clockwise from the right) and sweeps 270°.
    private const double StartAngle = 135;
    private const double SweepAngle = 270;
    private const double ArcThickness = 14;
    // Room between the arc and the edge of the control.
    private const double EdgeGap = 20;
    // Mark labels and the needle tip sit inside the arc, this far from its center line.
    private const double LabelInset = 26;
    private const double LabelFontSize = 11;
    private const double NeedleHalfWidth = 3.5;
    private const double HubRadius = 7;
    private static readonly Duration SlideDuration = new(TimeSpan.FromMilliseconds(300));

    /// <summary>Speed under the needle, in Mbps.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(SpeedGauge), new PropertyMetadata(0d, OnValueChanged));

    /// <summary>Filled part of the arc.</summary>
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(SpeedGauge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Empty part of the arc.</summary>
    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(SpeedGauge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Needle and hub.</summary>
    public static readonly DependencyProperty NeedleProperty = DependencyProperty.Register(
        nameof(Needle), typeof(Brush), typeof(SpeedGauge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Mark labels.</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(Brush), typeof(SpeedGauge), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Font of the mark labels, inherited from the window.</summary>
    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(SpeedGauge), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    // Where the needle is drawn, 0 to 1 along the arc; animated towards Value.
    private static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        "Position", typeof(double), typeof(SpeedGauge), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <inheritdoc cref="ValueProperty" />
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="FillProperty" />
    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <inheritdoc cref="TrackProperty" />
    public Brush? Track
    {
        get => (Brush?)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    /// <inheritdoc cref="NeedleProperty" />
    public Brush? Needle
    {
        get => (Brush?)GetValue(NeedleProperty);
        set => SetValue(NeedleProperty, value);
    }

    /// <inheritdoc cref="LabelProperty" />
    public Brush? Label
    {
        get => (Brush?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="FontFamilyProperty" />
    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        var center = new Point(ActualWidth / 2, ActualWidth / 2);
        double radius = ActualWidth / 2 - EdgeGap;
        if (radius <= 0)
        {
            return;
        }

        double position = (double)GetValue(PositionProperty);
        drawingContext.DrawGeometry(null, ArcPen(Track), Arc(center, radius, 1));
        if (position > 0)
        {
            drawingContext.DrawGeometry(null, ArcPen(Fill), Arc(center, radius, position));
        }

        DrawLabels(drawingContext, center, radius - LabelInset);
        DrawNeedle(drawingContext, center, radius - LabelInset, position);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gauge = (SpeedGauge)d;
        double target = SpeedGaugeScale.Fraction((double)e.NewValue);

        if (!SystemParameters.ClientAreaAnimation)
        {
            gauge.BeginAnimation(PositionProperty, null);
            gauge.SetValue(PositionProperty, target);
            return;
        }

        // Starts from where the needle is now, so a new value mid-slide doesn't jump.
        gauge.BeginAnimation(PositionProperty, new DoubleAnimation(target, SlideDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    // Point on a circle at a fraction of the sweep.
    private static Point At(Point center, double radius, double fraction)
    {
        double angle = (StartAngle + SweepAngle * fraction) * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
    }

    private static StreamGeometry Arc(Point center, double radius, double fraction)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(At(center, radius, 0), isFilled: false, isClosed: false);
            context.ArcTo(
                At(center, radius, fraction),
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: SweepAngle * fraction > 180,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Pen ArcPen(Brush? brush) =>
        new(brush, ArcThickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

    private void DrawLabels(DrawingContext drawingContext, Point center, double radius)
    {
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        double[] marks = SpeedGaugeScale.Marks;
        for (int i = 0; i < marks.Length; i++)
        {
            var text = new FormattedText(
                marks[i].ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                LabelFontSize,
                Label,
                pixelsPerDip);
            Point spot = At(center, radius, (double)i / (marks.Length - 1));
            drawingContext.DrawText(text, new Point(spot.X - text.Width / 2, spot.Y - text.Height / 2));
        }
    }

    // A thin triangle from the hub to the needle tip, plus the hub.
    private void DrawNeedle(DrawingContext drawingContext, Point center, double length, double position)
    {
        Point tip = At(center, length, position);
        double angle = (StartAngle + SweepAngle * position) * Math.PI / 180;
        var side = new Vector(-Math.Sin(angle) * NeedleHalfWidth, Math.Cos(angle) * NeedleHalfWidth);

        var needle = new StreamGeometry();
        using (StreamGeometryContext context = needle.Open())
        {
            context.BeginFigure(center + side, isFilled: true, isClosed: true);
            context.LineTo(tip, isStroked: false, isSmoothJoin: false);
            context.LineTo(center - side, isStroked: false, isSmoothJoin: false);
        }

        needle.Freeze();
        drawingContext.DrawGeometry(Needle, null, needle);
        drawingContext.DrawEllipse(Needle, null, center, HubRadius, HubRadius);
    }
}
