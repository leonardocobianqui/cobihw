using System.Security.Principal;
using CobiHw.App.Interop;

namespace CobiHw.App;

/// <summary>Whether the app runs as administrator, or could with a UAC prompt.</summary>
internal static class Elevation
{
    /// <summary>True when running as administrator.</summary>
    public static bool IsElevated { get; } = CheckElevated();

    /// <summary>True when a UAC prompt can grant admin rights.</summary>
    public static bool CanElevate => IsElevated || IsLimitedAdministrator();

    private static bool CheckElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // An administrator running without elevation.
    private static bool IsLimitedAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();

        return Advapi32.GetTokenInformation(identity.Token, Advapi32.TokenElevationTypeClass, out int elevationType, sizeof(int), out _) &&
               elevationType == Advapi32.TokenElevationTypeLimited;
    }
}
