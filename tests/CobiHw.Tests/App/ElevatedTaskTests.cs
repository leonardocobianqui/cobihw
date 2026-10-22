using CobiHw.App;

namespace CobiHw.Tests.App;

/// <summary>Command line and matching of the scheduled task that starts the app elevated.</summary>
[Collection(AppState.Name)]
public sealed class ElevatedTaskTests
{
    private const string Exe = @"C:\Users\João\Projetos\cobihw\publish\portable\cobihw.exe";
    private const string SettingsFile = @"C:\Users\João\Projetos\cobihw\publish\portable\cobihw.settings.json";

    private static string TaskXml(string command, string arguments) =>
        "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><Actions Context=\"Author\"><Exec><Command>"
        + command + "</Command><Arguments>" + arguments + "</Arguments></Exec></Actions></Task>";

    [Fact]
    public void SettingsPathFrom_ReadsTheArgumentAfterSettings()
    {
        Assert.Equal(@"C:\a b\cobihw.settings.json", ElevatedTask.SettingsPathFrom(["--settings", @"C:\a b\cobihw.settings.json"]));
        Assert.Null(ElevatedTask.SettingsPathFrom([]));
        Assert.Null(ElevatedTask.SettingsPathFrom(["--settings"]));
    }

    [Fact]
    public void RegisterArguments_QuoteThePath() =>
        Assert.Equal(@"--register-task --settings ""C:\a b\x.json""", ElevatedTask.RegisterArguments(@"C:\a b\x.json"));

    [Fact]
    public void Matches_SameExeAndSettings_IgnoringCaseLikeWindows() =>
        Assert.True(ElevatedTask.Matches(TaskXml(Exe, "--settings \"" + SettingsFile + "\""), Exe.ToUpperInvariant(), SettingsFile));

    [Fact]
    public void Matches_TaskOfAnotherCopy_DoesNotMatch() =>
        Assert.False(ElevatedTask.Matches(
            TaskXml(@"C:\Users\cobia\OneDrive\Desktop\portable\cobihw.exe", @"--settings ""C:\Users\cobia\OneDrive\Desktop\portable\cobihw.settings.json"""),
            Exe,
            SettingsFile));

    [Fact]
    public void Matches_SameExeWithAnotherSettingsFile_DoesNotMatch() =>
        Assert.False(ElevatedTask.Matches(TaskXml(Exe, @"--settings ""D:\outro.json"""), Exe, SettingsFile));

    [Fact]
    public void Matches_TaskWithoutAction_DoesNotMatch() =>
        Assert.False(ElevatedTask.Matches("<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\" />", Exe, SettingsFile));
}
