using CobiHw.App.Linux.Interop;
using CobiHw.Presentation.Settings;

namespace CobiHw.App.Linux;

/// <summary>The settings profile of the Linux user running the app.</summary>
internal static class LinuxProfile
{
    /// <summary>Host name, user id and user name. The uid survives renaming the user, like the SID on Windows; the name is for display only.</summary>
    public static ProfileIdentity Current() =>
        new(Environment.MachineName, $"uid:{LibC.GetEffectiveUserId()}", Environment.UserName);

    /// <summary>Where the settings file lives: ~/.config/cobihw, or under $XDG_CONFIG_HOME when it's set.</summary>
    /// <remarks>On Linux, .NET maps ApplicationData to the XDG config folder.</remarks>
    public static string SettingsFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify), "cobihw");
}
