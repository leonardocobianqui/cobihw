namespace CobiHw.App.Tray;

/// <summary>Builds the tray tooltip text.</summary>
public static class TrayTooltip
{
    /// <summary>First line of the tooltip.</summary>
    public const string Title = "cobihw";
    /// <summary>Windows limit: 128 characters including the terminating null.</summary>
    public const int MaximumLength = 127;

    /// <summary>Joins the lines, dropping lines from the end until the text fits.</summary>
    /// <param name="lines">Title first, then one line per item.</param>
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
