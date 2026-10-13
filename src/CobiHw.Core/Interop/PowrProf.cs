using System.Runtime.InteropServices;

namespace CobiHw.Core.Interop;

internal static partial class PowrProf
{
    public const int ProcessorInformationLevel = 11;
    public const uint StatusSuccess = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    [LibraryImport("powrprof.dll")]
    public static partial uint CallNtPowerInformation(
        int informationLevel,
        nint inputBuffer,
        uint inputBufferLength,
        [Out] ProcessorPowerInformation[] outputBuffer,
        uint outputBufferLength);
}
