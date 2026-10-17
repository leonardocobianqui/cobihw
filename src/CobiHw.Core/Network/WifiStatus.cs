namespace CobiHw.Core.Network;

public enum WifiState
{
    Connected,
    Disconnected,
    PermissionDenied,
    Unavailable,
}

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

public static class WifiMath
{
    public const int PhyOfdm = 4;
    public const int PhyHrDsss = 5;
    public const int PhyErp = 6;
    public const int PhyHt = 7;
    public const int PhyVht = 8;
    public const int PhyDmg = 9;
    public const int PhyHe = 10;
    public const int PhyEht = 11;

    public static double BandGhz(double frequencyMhz) => frequencyMhz switch
    {
        < 2500 => 2.4,
        < 5925 => 5,
        _ => 6,
    };

    public static int Channel(double frequencyMhz) => BandGhz(frequencyMhz) switch
    {
        2.4 when frequencyMhz >= 2484 => 14,
        2.4 => (int)Math.Round((frequencyMhz - 2407) / 5),
        5 => (int)Math.Round((frequencyMhz - 5000) / 5),
        _ when frequencyMhz < 5950 => 2,
        _ => (int)Math.Round((frequencyMhz - 5950) / 5),
    };

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
