using CobiHw.Core.Interop;

namespace CobiHw.Core.Readers;

/// <summary>
/// Reads the system battery through <c>GetSystemPowerStatus</c>, the same source as the taskbar battery icon.
/// </summary>
internal static class BatteryReader
{
    /// <summary>Reads the current battery state.</summary>
    /// <returns>The battery state, or <see langword="null"/> when the call fails or the PC has no system battery.</returns>
    public static BatterySnapshot? Read() =>
        Kernel32.GetSystemPowerStatus(out Kernel32.SystemPowerStatus status) ? Describe(status) : null;

    /// <summary>Converts the raw Windows power status into a <see cref="BatterySnapshot"/>.</summary>
    /// <param name="status">Structure filled by <c>GetSystemPowerStatus</c>.</param>
    /// <returns>The battery state, or <see langword="null"/> for desktops (no system battery) and unknown states.</returns>
    /// <remarks>A USB UPS with a data cable is reported by Windows as a battery and shows up here too.</remarks>
    public static BatterySnapshot? Describe(Kernel32.SystemPowerStatus status)
    {
        if (status.BatteryFlag == Kernel32.BatteryFlagUnknown || (status.BatteryFlag & Kernel32.BatteryFlagNoSystemBattery) != 0)
        {
            return null;
        }

        return new BatterySnapshot(
            status.BatteryLifePercent == Kernel32.BatteryPercentUnknown ? null : Math.Min((int)status.BatteryLifePercent, 100),
            (status.BatteryFlag & Kernel32.BatteryFlagCharging) != 0,
            status.ACLineStatus == Kernel32.AcLineOnline,
            status.BatteryLifeTime == Kernel32.BatteryLifeTimeUnknown ? null : TimeSpan.FromSeconds(status.BatteryLifeTime));
    }
}
