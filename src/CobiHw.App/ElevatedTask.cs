using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;

namespace CobiHw.App;

internal static class ElevatedTask
{
    public const string RegisterArgument = "--register-task";
    public const string SettingsArgument = "--settings";

    private const string TaskName = "cobihw";

    private static readonly XNamespace Schema = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public static bool Register(string executablePath, string settingsPath)
    {
        string definitionPath = Path.Combine(Path.GetTempPath(), $"cobihw-task-{Guid.NewGuid():N}.xml");

        try
        {
            File.WriteAllText(definitionPath, Definition(executablePath, settingsPath), Encoding.Unicode);
            return RunSchtasks("/Create", "/TN", TaskName, "/XML", definitionPath, "/F");
        }
        finally
        {
            File.Delete(definitionPath);
        }
    }

    public static bool Run() => RunSchtasks("/Run", "/TN", TaskName);

    public static bool Delete() => RunSchtasks("/Delete", "/TN", TaskName, "/F");

    public static string RegisterArguments(string settingsPath) =>
        $"{RegisterArgument} {SettingsArgument} \"{settingsPath}\"";

    public static string? SettingsPathFrom(string[] arguments)
    {
        int index = Array.IndexOf(arguments, SettingsArgument);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }

    private static string Definition(string executablePath, string settingsPath)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string user = identity.User?.Value ?? identity.Name;

        var task = new XElement(Schema + "Task",
            new XAttribute("version", "1.2"),
            new XElement(Schema + "RegistrationInfo",
                new XElement(Schema + "Description", "cobihw: abre o widget com permissão de administrador para medir o FPS.")),
            new XElement(Schema + "Principals",
                new XElement(Schema + "Principal",
                    new XAttribute("id", "Author"),
                    new XElement(Schema + "UserId", user),
                    new XElement(Schema + "LogonType", "InteractiveToken"),
                    new XElement(Schema + "RunLevel", "HighestAvailable"))),
            new XElement(Schema + "Settings",
                new XElement(Schema + "MultipleInstancesPolicy", "Parallel"),
                new XElement(Schema + "DisallowStartIfOnBatteries", "false"),
                new XElement(Schema + "StopIfGoingOnBatteries", "false"),
                new XElement(Schema + "ExecutionTimeLimit", "PT0S"),
                new XElement(Schema + "Priority", "5")),
            new XElement(Schema + "Actions",
                new XAttribute("Context", "Author"),
                new XElement(Schema + "Exec",
                    new XElement(Schema + "Command", executablePath),
                    new XElement(Schema + "Arguments", $"{SettingsArgument} \"{settingsPath}\""),
                    new XElement(Schema + "WorkingDirectory", Path.GetDirectoryName(executablePath)))));

        return new XDeclaration("1.0", "UTF-16", null) + Environment.NewLine + task;
    }

    private static bool RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("O schtasks.exe não iniciou.");
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
