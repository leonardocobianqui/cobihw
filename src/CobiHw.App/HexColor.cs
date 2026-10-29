using System.Globalization;

namespace CobiHw.App;

/// <summary>Reads and writes "#RRGGBB" colors.</summary>
public static class HexColor
{
    /// <summary>Reads a "#RRGGBB" color; spaces around it are ignored.</summary>
    /// <param name="text">Text to read.</param>
    /// <param name="red">Red part.</param>
    /// <param name="green">Green part.</param>
    /// <param name="blue">Blue part.</param>
    /// <returns>False when the text isn't a valid color.</returns>
    public static bool TryParse(string? text, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        string value = text?.Trim() ?? string.Empty;

        if (value.Length != 7 || value[0] != '#' ||
            !uint.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint rgb))
        {
            return false;
        }

        red = (byte)(rgb >> 16);
        green = (byte)(rgb >> 8);
        blue = (byte)rgb;
        return true;
    }

    /// <summary>Writes a color as "#RRGGBB".</summary>
    /// <param name="red">Red part.</param>
    /// <param name="green">Green part.</param>
    /// <param name="blue">Blue part.</param>
    public static string FromRgb(byte red, byte green, byte blue) => $"#{red:X2}{green:X2}{blue:X2}";

    /// <summary>The color in uppercase "#RRGGBB", or <paramref name="fallback"/> when the text is invalid.</summary>
    /// <param name="text">Color to check.</param>
    /// <param name="fallback">Color used when the text is invalid.</param>
    public static string Normalize(string? text, string fallback) =>
        TryParse(text, out byte red, out byte green, out byte blue) ? FromRgb(red, green, blue) : fallback;

    /// <summary>Adds transparency, turning "#RRGGBB" into "#AARRGGBB".</summary>
    /// <param name="color">A valid "#RRGGBB" color.</param>
    /// <param name="alpha">Opacity; 0 = fully transparent.</param>
    public static string WithAlpha(string color, byte alpha) => $"#{alpha:X2}{color[1..]}";

    /// <summary>
    /// <paramref name="color"/> drawn over <paramref name="background"/> at <paramref name="opacity"/>, as one opaque color.
    /// </summary>
    /// <remarks>Same result the screen gives for a semi-transparent color over that background, but the halo of an opaque color is at full strength.</remarks>
    /// <param name="color">A valid "#RRGGBB" color in front.</param>
    /// <param name="background">A valid "#RRGGBB" color behind it.</param>
    /// <param name="opacity">0 = only the background, 1 = only the color.</param>
    public static string Mix(string color, string background, double opacity)
    {
        TryParse(color, out byte red, out byte green, out byte blue);
        TryParse(background, out byte backRed, out byte backGreen, out byte backBlue);
        byte Blend(byte front, byte back) => (byte)Math.Round(front * opacity + back * (1 - opacity));
        return FromRgb(Blend(red, backRed), Blend(green, backGreen), Blend(blue, backBlue));
    }

    /// <summary>Black or white, whichever has more contrast with <paramref name="color"/> (WCAG relative luminance).</summary>
    /// <param name="color">A valid "#RRGGBB" color.</param>
    public static string Contrasting(string color)
    {
        TryParse(color, out byte red, out byte green, out byte blue);
        double luminance = 0.2126 * Linear(red) + 0.7152 * Linear(green) + 0.0722 * Linear(blue);

        // Contrast with black is (L + 0.05) / 0.05 and with white 1.05 / (L + 0.05); they are equal at L ≈ 0.179.
        return (luminance + 0.05) / 0.05 >= 1.05 / (luminance + 0.05) ? "#000000" : "#FFFFFF";
    }

    // sRGB channel (0–255) to linear light (0–1), as in the WCAG definition.
    private static double Linear(byte channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
