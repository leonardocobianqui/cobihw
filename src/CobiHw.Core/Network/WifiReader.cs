using System.Text;
using CobiHw.Core.Interop;

namespace CobiHw.Core.Network;

/// <summary>
/// Reads Wi-Fi details through the native WLAN API.
/// </summary>
/// <remarks>Thread-safe; keeps one WLAN handle open until disposed.</remarks>
public sealed unsafe class WifiReader : IDisposable
{
    private readonly Lock _gate = new();
    private nint _handle;

    /// <summary>Opens the WLAN client; if that fails, every read returns <see cref="WifiStatus.Unavailable"/>.</summary>
    public WifiReader()
    {
        if (Wlan.WlanOpenHandle(Wlan.ClientVersion, 0, out _, out nint handle) == 0)
        {
            _handle = handle;
        }
    }

    /// <summary>Reads the current connection of one wireless interface.</summary>
    public WifiStatus Read(Guid interfaceId)
    {
        lock (_gate)
        {
            return ReadLocked(interfaceId);
        }
    }

    /// <summary>Closes the WLAN client handle.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_handle != 0)
            {
                Wlan.WlanCloseHandle(_handle, 0);
                _handle = 0;
            }
        }
    }

    private WifiStatus ReadLocked(Guid interfaceId)
    {
        if (_handle == 0)
        {
            return WifiStatus.Unavailable;
        }

        uint result = Wlan.WlanQueryInterface(_handle, interfaceId, Wlan.OpcodeCurrentConnection, 0, out _, out nint data, out _);

        if (result == Wlan.ErrorAccessDenied)
        {
            return WifiStatus.PermissionDenied;
        }

        if (result != 0)
        {
            return WifiStatus.Disconnected;
        }

        try
        {
            var connection = (Wlan.ConnectionAttributes*)data;

            if (connection->State != Wlan.StateConnected)
            {
                return WifiStatus.Disconnected;
            }

            Wlan.AssociationAttributes* association = &connection->Association;
            // RSSI and frequency are only in the BSS list, matched by the access point's BSSID.
            Wlan.BssEntry? bss = FindBss(interfaceId, association, connection->Security.SecurityEnabled);
            double? frequencyMhz = bss?.CenterFrequencyKhz / 1000.0;
            double? band = frequencyMhz is double mhz ? WifiMath.BandGhz(mhz) : null;

            return new WifiStatus(
                WifiState.Connected,
                Ssid(&association->Ssid),
                (int)association->SignalQuality,
                bss?.Rssi,
                band,
                frequencyMhz is double value ? WifiMath.Channel(value) : null,
                WifiMath.Standard(association->PhyType, band),
                association->RxRateKbps / 1000.0,
                association->TxRateKbps / 1000.0);
        }
        finally
        {
            Wlan.WlanFreeMemory(data);
        }
    }

    private Wlan.BssEntry? FindBss(Guid interfaceId, Wlan.AssociationAttributes* association, int securityEnabled)
    {
        if (Wlan.WlanGetNetworkBssList(_handle, interfaceId, &association->Ssid, Wlan.BssTypeInfrastructure, securityEnabled, 0, out nint list) != 0)
        {
            return null;
        }

        try
        {
            // WLAN_BSS_LIST: total size, then the number of items.
            uint count = *(uint*)(list + sizeof(uint));
            var entries = (Wlan.BssEntry*)(list + Wlan.BssListHeaderSize);

            for (int i = 0; i < count; i++)
            {
                if (new ReadOnlySpan<byte>(entries[i].Bssid, 6).SequenceEqual(new ReadOnlySpan<byte>(association->Bssid, 6)))
                {
                    return entries[i];
                }
            }

            return null;
        }
        finally
        {
            Wlan.WlanFreeMemory(list);
        }
    }

    // SSIDs are raw bytes; UTF-8 is what Windows and routers use in practice.
    private static string Ssid(Wlan.Dot11Ssid* ssid) =>
        Encoding.UTF8.GetString(ssid->Bytes, (int)Math.Min(ssid->Length, 32));
}
