namespace CobiHw.App.Tray;

public static class TrayTooltip
{
    public const string Title = "cobihw";
    public const int MaximumLength = 127;

    public static string Build(IReadOnlyList<string> lines)
    {
        int count = lines.Count;
        string text = string.Join('\n', lines);

        while (text.Length > MaximumLength && count > 1)
        {
            count--;
            text = string.Join('\n', lines.Take(count));
        }

        return text;
    }
}
