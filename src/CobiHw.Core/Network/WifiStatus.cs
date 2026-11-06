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
/// Band and channel from a Wi-Fi center frequency; the same on every platform.
/// </summary>
public static class WifiMath
{
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
}
