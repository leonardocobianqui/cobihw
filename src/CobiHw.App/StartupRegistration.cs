using System.IO;
using Microsoft.Win32;

namespace CobiHw.App;

/// <summary>"Start with Windows" entry in the current user's Run registry key.</summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "cobihw";

    /// <summary>True when the entry exists.</summary>
    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>Creates the entry pointing to this .exe.</summary>
    public static void Enable()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, CommandLine());
    }

    /// <summary>Removes the entry.</summary>
    public static void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Points the entry to this .exe again when the app folder was moved.</summary>
    public static void RepairIfMoved()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (key?.GetValue(ValueName) is string registered && !File.Exists(registered.Trim('"')))
        {
            key.SetValue(ValueName, CommandLine());
        }
    }

    // Quoted, since the path may contain spaces.
    private static string CommandLine()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The executable path is not available.");

        return $"\"{executablePath}\"";
    }
}
