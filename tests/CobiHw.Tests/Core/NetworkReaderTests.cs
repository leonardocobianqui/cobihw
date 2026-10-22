using System.Diagnostics;
using CobiHw.Core;
using CobiHw.Core.Interop;
using CobiHw.Core.Network;
using CobiHw.Core.Readers;

namespace CobiHw.Tests.Core;

/// <summary>Network speeds and session totals, per device and summed.</summary>
[Collection(AppState.Name)]
public sealed class NetworkReaderTests
{
    private const long Start = 1_000;
    private static readonly Guid WifiId = Guid.Parse("e1482a7e-f330-4641-a8af-98f6f2e14e50");
    private static readonly Guid TailscaleId = Guid.Parse("37217669-42da-4657-a55b-0d995d328200");
    private static readonly Guid LanId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static long Seconds(int seconds) => Start + seconds * Stopwatch.Frequency;

    private static IfRow Row(ulong luid, Guid id, string name, uint type, bool hardware, bool up, ulong received, ulong sent) =>
        new(luid, id, name, name, type, 0, hardware, up, received, sent);

    private static IfRow Wifi(ulong received, ulong sent) => Row(1, WifiId, "Wi-Fi", 71, true, true, received, sent);

    private static IfRow Tailscale(ulong received) => Row(2, TailscaleId, "Tailscale", 53, false, true, received, 100);

    private static IfRow UnpluggedLan() => Row(3, LanId, "Ethernet", 6, true, false, 0, 0);

    [Fact]
    public void FirstReading_HasNoSpeed_AndTheSessionStartsWithWhatTheAdapterHad()
    {
        var reader = new NetworkReader(null);

        NetworkSnapshot first = reader.Process([Wifi(10_000, 2_000), Tailscale(500), UnpluggedLan()], Start);

        Assert.Null(first.DownloadBytesPerSecond);
        Assert.Null(first.Find(WifiId)!.DownloadBytesPerSecond);
        Assert.Equal(10_000UL, first.SessionReceivedBytes);
        Assert.Equal(500UL, first.Find(TailscaleId)!.SessionReceivedBytes);
    }

    [Fact]
    public void SecondReading_SpeedPerDevice_AndTheSumLeavesVirtualAdaptersOut()
    {
        var reader = new NetworkReader(null);
        reader.Process([Wifi(10_000, 2_000), Tailscale(500), UnpluggedLan()], Start);

        NetworkSnapshot second = reader.Process([Wifi(1_010_000, 52_000), Tailscale(250_500), UnpluggedLan()], Seconds(1));

        Assert.Equal(1_000_000, second.Find(WifiId)!.DownloadBytesPerSecond!.Value, 1.0);
        Assert.Equal(250_000, second.Find(TailscaleId)!.DownloadBytesPerSecond!.Value, 1.0);
        Assert.Equal(1_000_000, second.DownloadBytesPerSecond!.Value, 1.0);
        Assert.Equal(50_000, second.UploadBytesPerSecond!.Value, 1.0);
        Assert.Equal(250_500UL, second.Find(TailscaleId)!.SessionReceivedBytes);
        Assert.Equal(1_010_000UL, second.Find(WifiId)!.TotalReceivedBytes);
        Assert.False(second.Find(LanId)!.IsUp);
    }

    [Fact]
    public void ResetSession_OfOneDevice_OrOfTheSum_LeavesTheOthersAlone()
    {
        var reader = new NetworkReader(null);
        reader.Process([Wifi(10_000, 2_000), Tailscale(500), UnpluggedLan()], Start);
        reader.Process([Wifi(1_010_000, 52_000), Tailscale(250_500), UnpluggedLan()], Seconds(1));

        reader.ResetSession(TailscaleId);
        NetworkSnapshot third = reader.Process([Wifi(1_010_100, 52_000), Tailscale(250_600)], Seconds(2));

        Assert.Equal(100UL, third.Find(TailscaleId)!.SessionReceivedBytes);
        Assert.Equal(1_010_100UL, third.Find(WifiId)!.SessionReceivedBytes);
        Assert.Equal(1_010_100UL, third.SessionReceivedBytes);

        reader.ResetSession(null);
        NetworkSnapshot fourth = reader.Process([Wifi(1_010_200, 52_000)], Seconds(3));

        Assert.Equal(100UL, fourth.SessionReceivedBytes);
        Assert.Equal(1_010_200UL, fourth.Find(WifiId)!.SessionReceivedBytes);

        // The per-device session goes to the settings file.
        NetworkSessionState exported = reader.ExportSession();
        Assert.Equal(100UL, exported.Devices[TailscaleId].ReceivedBytes);
        Assert.Equal(1_010_200UL, exported.Devices[WifiId].ReceivedBytes);
    }

    [Fact]
    public void BluetoothPan_CountsInTheSumOfPhysicalAdapters()
    {
        // Windows doesn't flag Bluetooth PAN as hardware, but it is a real connection.
        var reader = new NetworkReader(null);
        Guid bluetoothId = Guid.NewGuid();
        IfRow Bluetooth(ulong received) => new(9, bluetoothId, "Bluetooth", "Bluetooth Device (PAN)", 6, NetworkDeviceClassifier.MediumBluetooth, false, true, received, 0);

        reader.Process([Wifi(0, 0), Bluetooth(0)], Start);
        NetworkSnapshot second = reader.Process([Wifi(1_000, 0), Bluetooth(500)], Seconds(1));

        Assert.Equal(1_500, second.DownloadBytesPerSecond!.Value, 1.0);
        Assert.Equal(1_500UL, second.SessionReceivedBytes);
    }
}
