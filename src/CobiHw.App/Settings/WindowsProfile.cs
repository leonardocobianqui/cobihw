using System.Security.Principal;
using CobiHw.Presentation.Settings;

namespace CobiHw.App.Settings;

/// <summary>The settings profile of the Windows user running the app.</summary>
internal static class WindowsProfile
{
    /// <summary>Computer name, user SID and user name. The SID survives renaming the user; the name is for display only.</summary>
    public static ProfileIdentity Current()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new ProfileIdentity(Environment.MachineName, identity.User?.Value ?? identity.Name, Environment.UserName);
    }
}
