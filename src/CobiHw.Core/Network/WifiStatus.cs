namespace CobiHw.Core.Network;

/// <summary>Result of reading a Wi-Fi interface.</summary>
public enum WifiState
{
    Connected,
    Disconnected,
    /// <summary>Windows refused because Location access is off (Windows 11 24H2+).</summary>
    PermissionDenied,
    /// <summary>The WLAN service isn't available (e.g. no Wi-Fi hardware).</summary>
    Unavailable,
}

/// <summary>Wi-Fi connection details.</summary>
/// <param name="State">Connection state; the other values are set only when connected.</param>
/// <param name="Ssid">Network name.</param>
/// <param name="SignalPercent">Signal quality, 0 to 100.</param>
/// <param name="RssiDbm">Signal strength in dBm (e.g. -55).</param>
/// <param name="BandGhz">2.4, 5 or 6.</param>
/// <param name="Channel">Channel number.</param>
/// <param name="Standard">e.g. "Wi-Fi 6 (802.11ax)".</param>
/// <param name="ReceiveMbps">Link rate for receiving.</param>
/// <param name="SendMbps">Link rate for sending.</param>
public sealed record WifiStatus(
    WifiState State,
    string? Ssid = null,
    int? SignalPercent = null,
    int? RssiDbm = null,
    double? BandGhz = null,
    int? Channel = null,
    string? Standard = null,
    double? ReceiveMbps = null,
    double? SendMbps = null)
{
    public static readonly WifiStatus Disconnected = new(WifiState.Disconnected);
    public static readonly WifiStatus PermissionDenied = new(WifiState.PermissionDenied);
    public static readonly WifiStatus Unavailable = new(WifiState.Unavailable);
}

/// <summary>
/// Converts raw WLAN values into band, channel and standard names.
/// </summary>
public static class WifiMath
{
    /// <summary>DOT11_PHY_TYPE values.</summary>
    public const int PhyOfdm = 4;
    public const int PhyHrDsss = 5;
    public const int PhyErp = 6;
    public const int PhyHt = 7;
    public const int PhyVht = 8;
    public const int PhyDmg = 9;
    public const int PhyHe = 10;
    public const int PhyEht = 11;

    /// <summary>Band from the center frequency.</summary>
    public static double BandGhz(double frequencyMhz) => frequencyMhz switch
    {
        < 2500 => 2.4,
        < 5925 => 5,
        _ => 6,
    };

    /// <summary>Channel number from the center frequency, per band (6 GHz channel 2 is a special case).</summary>
    public static int Channel(double frequencyMhz) => BandGhz(frequencyMhz) switch
    {
        2.4 when frequencyMhz >= 2484 => 14,
        2.4 => (int)Math.Round((frequencyMhz - 2407) / 5),
        5 => (int)Math.Round((frequencyMhz - 5000) / 5),
        _ when frequencyMhz < 5950 => 2,
        _ => (int)Math.Round((frequencyMhz - 5950) / 5),
    };

    /// <summary>Marketing name of the PHY type; 802.11ax on 6 GHz is Wi-Fi 6E.</summary>
    public static string? Standard(int phyType, double? bandGhz) => phyType switch
    {
        PhyHrDsss => "802.11b",
        PhyErp => "802.11g",
        PhyOfdm => "802.11a",
        PhyHt => "Wi-Fi 4 (802.11n)",
        PhyVht => "Wi-Fi 5 (802.11ac)",
        PhyHe when bandGhz == 6 => "Wi-Fi 6E (802.11ax)",
        PhyHe => "Wi-Fi 6 (802.11ax)",
        PhyEht => "Wi-Fi 7 (802.11be)",
        PhyDmg => "802.11ad",
        _ => null,
    };
}
