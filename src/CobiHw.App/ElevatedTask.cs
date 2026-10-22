using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using CobiHw.App.Resources;

namespace CobiHw.App;

/// <summary>
/// Scheduled task that starts the app as administrator without a UAC prompt; the FPS reading needs it.
/// One task per Windows user, created for a specific .exe and settings file.
/// </summary>
internal static class ElevatedTask
{
    /// <summary>Runs the app only to register the task, then exits.</summary>
    public const string RegisterArgument = "--register-task";
    /// <summary>Followed by the settings file the app must use.</summary>
    public const string SettingsArgument = "--settings";

    // Name used by older versions, before it included the user SID.
    private const string LegacyTaskName = "cobihw";
    // COM Task Scheduler, used only to read the task.
    private const string TaskServiceProgId = "Schedule.Service";

    private static readonly XNamespace Schema = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    /// <summary>Creates or replaces the task through schtasks.exe; needs admin rights.</summary>
    /// <param name="executablePath">.exe the task starts.</param>
    /// <param name="settingsPath">Settings file passed to the app.</param>
    /// <returns>True when the task was created.</returns>
    public static bool Register(string executablePath, string settingsPath)
    {
        string definitionPath = Path.Combine(Path.GetTempPath(), $"cobihw-task-{Guid.NewGuid():N}.xml");

        try
        {
            File.WriteAllText(definitionPath, Definition(executablePath, settingsPath), Encoding.Unicode);

            if (!RunSchtasks("/Create", "/TN", TaskName, "/XML", definitionPath, "/F"))
            {
                return false;
            }

            RunSchtasks("/Delete", "/TN", LegacyTaskName, "/F");
            return true;
        }
        finally
        {
            File.Delete(definitionPath);
        }
    }

    /// <summary>Starts the task, which launches the app elevated.</summary>
    public static bool Run() => RunSchtasks("/Run", "/TN", TaskName);

    /// <summary>Deletes the task.</summary>
    public static bool Delete() => RunSchtasks("/Delete", "/TN", TaskName, "/F");

    /// <summary>True when the task exists for this user.</summary>
    public static bool Exists() => ReadDefinition() is not null;

    /// <summary>True when the task starts this .exe with this settings file.</summary>
    /// <param name="executablePath">Expected .exe.</param>
    /// <param name="settingsPath">Expected settings file.</param>
    public static bool PointsTo(string executablePath, string settingsPath) =>
        ReadDefinition() is string xml && Matches(xml, executablePath, settingsPath);

    /// <summary>Compares a task definition with the expected .exe and arguments, ignoring case like Windows paths.</summary>
    /// <param name="xml">Task definition XML.</param>
    /// <param name="executablePath">Expected .exe.</param>
    /// <param name="settingsPath">Expected settings file.</param>
    public static bool Matches(string xml, string executablePath, string settingsPath)
    {
        XElement? exec = XDocument.Parse(xml).Root?.Element(Schema + "Actions")?.Element(Schema + "Exec");

        return exec is not null &&
               string.Equals((string?)exec.Element(Schema + "Command"), executablePath, StringComparison.OrdinalIgnoreCase) &&
               string.Equals((string?)exec.Element(Schema + "Arguments"), TaskArguments(settingsPath), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Command line of the elevated helper that registers the task.</summary>
    /// <param name="settingsPath">Settings file the task will use.</param>
    public static string RegisterArguments(string settingsPath) =>
        $"{RegisterArgument} {SettingsArgument} \"{settingsPath}\"";

    /// <summary>Settings file passed with <see cref="SettingsArgument"/>, or null.</summary>
    /// <param name="arguments">Command-line arguments.</param>
    public static string? SettingsPathFrom(string[] arguments)
    {
        int index = Array.IndexOf(arguments, SettingsArgument);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }

    // Named by user SID, so accounts on the same PC don't overwrite each other's task.
    private static string TaskName => $"cobihw-{CurrentUser()}";

    private static string TaskArguments(string settingsPath) => $"{SettingsArgument} \"{settingsPath}\"";

    private static string CurrentUser()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? identity.Name;
    }

    // Reads the task through COM; null when it doesn't exist or can't be read.
    private static string? ReadDefinition()
    {
        if (Type.GetTypeFromProgID(TaskServiceProgId) is not Type serviceType)
        {
            return null;
        }

        try
        {
            dynamic service = Activator.CreateInstance(serviceType)!;
            service.Connect();
            dynamic task = service.GetFolder(@"\").GetTask(TaskName);
            return (string)task.Xml;
        }
        catch (Exception exception) when (exception is COMException or FileNotFoundException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Task Scheduler XML: highest privileges for the logged-on user, runs on battery, no time limit.
    private static string Definition(string executablePath, string settingsPath)
    {
        string user = CurrentUser();

        var task = new XElement(Schema + "Task",
            new XAttribute("version", "1.2"),
            new XElement(Schema + "RegistrationInfo",
                new XElement(Schema + "Description", Strings.Task_Description)),
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
                    new XElement(Schema + "Arguments", TaskArguments(settingsPath)),
                    new XElement(Schema + "WorkingDirectory", Path.GetDirectoryName(executablePath)))));

        return new XDeclaration("1.0", "UTF-16", null) + Environment.NewLine + task;
    }

    // Runs schtasks.exe without a window and waits for it.
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
            ?? throw new InvalidOperationException("schtasks.exe did not start.");
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
