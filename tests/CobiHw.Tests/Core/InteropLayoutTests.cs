using CobiHw.Core.Interop;

namespace CobiHw.Tests.Core;

/// <summary>
/// The Windows structures in CobiHw.Core keep the layout of the SDK headers in 64-bit and 32-bit processes.
/// </summary>
/// <remarks>
/// Expected values come from the Windows headers compiled for x64 and x86. A wrong layout doesn't crash:
/// the readings just come out wrong, so these tests are the only place that catches it.
/// </remarks>
[Collection(AppState.Name)]
public sealed class InteropLayoutTests
{
    [Fact]
    public void AdapterDesc1_MatchesDxgiAdapterDesc1()
    {
        // Three SIZE_T memory fields: 8 bytes each in x64, 4 in x86.
        NativeLayout.AssertSize<Dxgi.AdapterDesc1>(x64: 312, x86: 296);
        NativeLayout.AssertOffset<Dxgi.AdapterDesc1>(nameof(Dxgi.AdapterDesc1.AdapterLuidLowPart), x64: 296, x86: 284);
    }

    [Fact]
    public void CounterValue_MatchesPdhFmtCounterValue() =>
        NativeLayout.AssertSize<Pdh.CounterValue>(x64: 16, x86: 16);

    [Fact]
    public void CounterValueItem_MatchesPdhFmtCounterValueItem()
    {
        // The name pointer shrinks in x86, but the value stays aligned to 8.
        NativeLayout.AssertSize<Pdh.CounterValueItem>(x64: 24, x86: 24);
        NativeLayout.AssertOffset<Pdh.CounterValueItem>(nameof(Pdh.CounterValueItem.Value), x64: 8, x86: 8);
    }

    [Fact]
    public void MibIfRow2_MatchesMibIfRow2()
    {
        NativeLayout.AssertSize<IpHelper.MibIfRow2>(x64: 1352, x86: 1352);
        NativeLayout.AssertOffset<IpHelper.MibIfRow2>(nameof(IpHelper.MibIfRow2.InOctets), x64: 1208, x86: 1208);
        NativeLayout.AssertValue(IpHelper.TableRowsOffset, x64: 8, x86: 8);
    }

    [Fact]
    public void WlanStructures_MatchWlanApi()
    {
        NativeLayout.AssertSize<Wlan.ConnectionAttributes>(x64: 604, x86: 604);
        NativeLayout.AssertSize<Wlan.BssEntry>(x64: 360, x86: 360);
        NativeLayout.AssertOffset<Wlan.BssEntry>(nameof(Wlan.BssEntry.Timestamp), x64: 72, x86: 72);
        NativeLayout.AssertValue(Wlan.BssListHeaderSize, x64: 8, x86: 8);
    }

    [Fact]
    public void D3dkmtStructures_MatchD3dkmthk()
    {
        NativeLayout.AssertSize<D3dkmt.OpenAdapterFromLuid>(x64: 12, x86: 12);
        // The driver data pointer: 8 bytes in x64, 4 in x86.
        NativeLayout.AssertSize<D3dkmt.QueryAdapterInfo>(x64: 24, x86: 16);
        NativeLayout.AssertSize<D3dkmt.AdapterPerfData>(x64: 64, x86: 64);
        NativeLayout.AssertOffset<D3dkmt.AdapterPerfData>(nameof(D3dkmt.AdapterPerfData.Temperature), x64: 56, x86: 56);
    }

    [Fact]
    public void Kernel32Structures_MatchWinbase()
    {
        NativeLayout.AssertSize<Kernel32.MemoryStatusEx>(x64: 64, x86: 64);
        NativeLayout.AssertSize<Kernel32.SystemPowerStatus>(x64: 12, x86: 12);
    }
}
