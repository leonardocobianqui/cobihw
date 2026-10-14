using System.Globalization;

namespace CobiHw.App;

public static class HexColor
{
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

    public static string FromRgb(byte red, byte green, byte blue) => $"#{red:X2}{green:X2}{blue:X2}";

    public static string Normalize(string? text, string fallback) =>
        TryParse(text, out byte red, out byte green, out byte blue) ? FromRgb(red, green, blue) : fallback;

    public static string WithAlpha(string color, byte alpha) => $"#{alpha:X2}{color[1..]}";
}
