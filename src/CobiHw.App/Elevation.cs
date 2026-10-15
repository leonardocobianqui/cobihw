using System.Security.Principal;
using CobiHw.App.Interop;

namespace CobiHw.App;

internal static class Elevation
{
    public static bool IsElevated { get; } = CheckElevated();

    public static bool CanElevate => IsElevated || IsLimitedAdministrator();

    private static bool CheckElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool IsLimitedAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();

        return Advapi32.GetTokenInformation(identity.Token, Advapi32.TokenElevationTypeClass, out int elevationType, sizeof(int), out _) &&
               elevationType == Advapi32.TokenElevationTypeLimited;
    }
}
