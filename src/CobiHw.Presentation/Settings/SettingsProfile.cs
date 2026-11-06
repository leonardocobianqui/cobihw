namespace CobiHw.Presentation.Settings;

/// <summary>Who the settings belong to: one profile per computer and user in the same file.</summary>
/// <param name="Computer">Computer name.</param>
/// <param name="UserId">Stable id of the user account (the SID on Windows); survives renaming the user.</param>
/// <param name="UserName">User name, for display only.</param>
public sealed record ProfileIdentity(string Computer, string UserId, string UserName)
{
    /// <summary>True when a stored profile belongs to this computer and user.</summary>
    public bool Matches(string? computer, string? userId) =>
        string.Equals(computer, Computer, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(userId, UserId, StringComparison.Ordinal);
}

/// <summary>Another profile found in the file, as listed in the General tab.</summary>
/// <param name="Computer">Computer name.</param>
/// <param name="UserName">User name.</param>
/// <param name="LastUsed">Last time that profile was saved, if known.</param>
public sealed record StoredProfile(string Computer, string UserName, DateTimeOffset? LastUsed);

/// <summary>Whether settings can be saved.</summary>
public enum SettingsAccess
{
    Writable,
    /// <summary>Another program is holding the file; changes stay in memory.</summary>
    FileInUse,
    /// <summary>The app folder is read-only (e.g. Program Files); nothing is saved this session.</summary>
    FolderNotWritable,
}
