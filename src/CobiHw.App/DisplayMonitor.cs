namespace CobiHw.App;

public sealed record DisplayMonitor(string Device, int Number, int Width, int Height, bool IsPrimary)
{
    public static int NumberOf(string device, int fallback)
    {
        string digits = new([.. device.Reverse().TakeWhile(char.IsAsciiDigit).Reverse()]);
        return int.TryParse(digits, out int number) ? number : fallback;
    }
}
