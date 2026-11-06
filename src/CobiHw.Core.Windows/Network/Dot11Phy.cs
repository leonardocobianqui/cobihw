namespace CobiHw.Core.Windows.Network;

/// <summary>
/// Wi-Fi standard names for the DOT11_PHY_TYPE values the Native Wi-Fi API reports.
/// </summary>
public static class Dot11Phy
{
    public const int Ofdm = 4;
    public const int HrDsss = 5;
    public const int Erp = 6;
    public const int Ht = 7;
    public const int Vht = 8;
    public const int Dmg = 9;
    public const int He = 10;
    public const int Eht = 11;

    /// <summary>Marketing name of the PHY type; 802.11ax on 6 GHz is Wi-Fi 6E.</summary>
    public static string? Standard(int phyType, double? bandGhz) => phyType switch
    {
        HrDsss => "802.11b",
        Erp => "802.11g",
        Ofdm => "802.11a",
        Ht => "Wi-Fi 4 (802.11n)",
        Vht => "Wi-Fi 5 (802.11ac)",
        He when bandGhz == 6 => "Wi-Fi 6E (802.11ax)",
        He => "Wi-Fi 6 (802.11ax)",
        Eht => "Wi-Fi 7 (802.11be)",
        Dmg => "802.11ad",
        _ => null,
    };
}
