namespace CobiHw.App.Linux;

/// <summary>Facts about the running desktop that change how the widget can be kept under other windows.</summary>
internal static class DesktopEnvironment
{
    /// <summary>
    /// True on GNOME, where desktop mode can't use _NET_WM_STATE_BELOW.
    /// </summary>
    /// <remarks>
    /// GNOME has no desktop layer for icons. Its desktop-icons extensions (DING, and the forks Ubuntu and Zorin ship)
    /// draw them in a transparent full-screen window kept in the below layer, the same layer _NET_WM_STATE_BELOW puts the
    /// widget in. Whichever was raised last takes the clicks, so the icons end up covering the widget. KDE, Xfce,
    /// Cinnamon and MATE draw icons in a desktop-type window, a layer under that one, so there the below state works.
    /// </remarks>
    public static bool BelowLayerIsShared { get; } = IsGnome(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"));

    // A colon-separated list, most specific first: "GNOME", "ubuntu:GNOME", "zorin:GNOME".
    private static bool IsGnome(string? currentDesktop) =>
        currentDesktop?.Split(':').Any(name => name.Equals("GNOME", StringComparison.OrdinalIgnoreCase)) == true;
}
