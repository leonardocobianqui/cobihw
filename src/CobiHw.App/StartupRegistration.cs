using System.IO;
using Microsoft.Win32;

namespace CobiHw.App;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "cobihw";

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string;
    }

    public static void Enable()
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, CommandLine());
    }

    public static void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static void RepairIfMoved()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (key?.GetValue(ValueName) is string registered && !File.Exists(registered.Trim('"')))
        {
            key.SetValue(ValueName, CommandLine());
        }
    }

    private static string CommandLine()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("O caminho do executável não está disponível.");

        return $"\"{executablePath}\"";
    }
}
