using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

/// <summary>
/// powrprof.dll declarations used to read the processor base clock.
/// </summary>
internal static partial class PowrProf
{
    /// <summary>POWER_INFORMATION_LEVEL.ProcessorInformation.</summary>
    public const int ProcessorInformationLevel = 11;
    public const uint StatusSuccess = 0;

    /// <summary>Mirrors PROCESSOR_POWER_INFORMATION (one entry per logical processor).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessorPowerInformation
    {
        public uint Number;
        /// <summary>Base clock in MHz (not the turbo clock).</summary>
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    /// <summary>Fills <paramref name="outputBuffer"/>; returns an NTSTATUS (0 = success).</summary>
    [LibraryImport("powrprof.dll")]
    public static partial uint CallNtPowerInformation(
        int informationLevel,
        nint inputBuffer,
        uint inputBufferLength,
        [Out] ProcessorPowerInformation[] outputBuffer,
        uint outputBufferLength);
}
