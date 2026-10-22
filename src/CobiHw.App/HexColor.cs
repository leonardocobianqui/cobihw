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
}
