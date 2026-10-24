using System.Runtime.InteropServices;
using CobiHw.App;
using CobiHw.App.About;

namespace CobiHw.Tests.App.About;

/// <summary>Version info, the GitHub release check and the About window.</summary>
[Collection(AppState.Name)]
public sealed class AboutTests : LocalizedTest
{
    private const string NewReleasePage = "https://github.com/leonardocobianqui/cobihw/releases/tag/v9.1.0";

    private readonly List<string> _opened = [];

    // Package of the 9.1.0 release for the architecture running the tests (x64 or x86).
    private static readonly string[] PackagesForThisApp = ["cobihw-9.1.0-portable" + UpdateChecker.PackageSuffix(RuntimeInformation.ProcessArchitecture)];

    private AboutViewModel About(Func<CancellationToken, Task<ReleaseInfo?>> latestRelease) => new(latestRelease, _opened.Add);

    [Theory]
    [InlineData("v1.2.0")]
    [InlineData("1.2")]
    public void ParseVersion_ReadsTheTag(string tag) => Assert.Equal(new Version(1, 2, 0), UpdateChecker.ParseVersion(tag));

    [Fact]
    public void ParseVersion_NotAVersion_IsNull() => Assert.Null(UpdateChecker.ParseVersion("abc"));

    [Fact]
    public void IsNewer_IgnoresTheFourthPart()
    {
        // GitHub tags have three parts; the assembly version has four.
        Assert.False(UpdateChecker.IsNewer(new Version(1, 0, 0), new Version(1, 0, 0, 0)));
        Assert.True(UpdateChecker.IsNewer(new Version(1, 0, 1), new Version(1, 0, 0, 0)));
    }

    [Fact]
    public void ParseRelease_ReadsVersionPageAndFiles()
    {
        ReleaseInfo release = UpdateChecker.ParseRelease(
            "{\"tag_name\":\"v1.1.0\",\"html_url\":\"https://github.com/leonardocobianqui/cobihw/releases/tag/v1.1.0\","
            + "\"assets\":[{\"name\":\"cobihw-1.1.0-portable-win-x86.zip\"},{\"name\":\"SHA256SUMS.txt\"}]}");

        Assert.Equal(new Version(1, 1, 0), release.Version);
        Assert.Equal("https://github.com/leonardocobianqui/cobihw/releases/tag/v1.1.0", release.PageUrl);
        Assert.Equal(new[] { "cobihw-1.1.0-portable-win-x86.zip", "SHA256SUMS.txt" }, release.Files);
    }

    [Fact]
    public void ParseRelease_WithoutFiles_HasAnEmptyList() =>
        Assert.Empty(UpdateChecker.ParseRelease("{\"tag_name\":\"v1.1.0\"}").Files);

    [Theory]
    [InlineData(Architecture.X64, "-win-x64.zip")]
    [InlineData(Architecture.X86, "-win-x86.zip")]
    public void PackageSuffix_MatchesThePublishScriptNames(Architecture architecture, string suffix) =>
        Assert.Equal(suffix, UpdateChecker.PackageSuffix(architecture));

    [Fact]
    public void HasPackageFor_LooksOnlyAtThatArchitecture()
    {
        var release = new ReleaseInfo(new Version(1, 1, 0), AppInfo.ReleasesUrl, ["cobihw-1.1.0-portable-win-x64.zip", "cobihw-1.1.0-framework-dependent-win-x64.zip", "SHA256SUMS.txt"]);

        Assert.True(release.HasPackageFor(Architecture.X64));
        Assert.False(release.HasPackageFor(Architecture.X86));
    }

    [Fact]
    public void IsUpdate_NeedsANewerVersionAndAPackage()
    {
        var withX86 = new ReleaseInfo(new Version(1, 1, 0), AppInfo.ReleasesUrl, ["cobihw-1.1.0-portable-win-x86.zip"]);

        Assert.True(UpdateChecker.IsUpdate(withX86, new Version(1, 0, 0), Architecture.X86));
        Assert.False(UpdateChecker.IsUpdate(withX86, new Version(1, 0, 0), Architecture.X64));
        Assert.False(UpdateChecker.IsUpdate(withX86, new Version(1, 1, 0), Architecture.X86));
    }

    [Theory]
    [InlineData("https://exemplo-falso.com/baixe")]
    [InlineData("http://github.com/x")] // Without TLS.
    public void ParseRelease_PageOutsideGitHub_IsReplacedByTheOfficialReleasesPage(string page) =>
        Assert.Equal(AppInfo.ReleasesUrl, UpdateChecker.ParseRelease("{\"tag_name\":\"v9.0.0\",\"html_url\":\"" + page + "\"}").PageUrl);

    [Fact]
    public void ParseRelease_TagThatIsNotAVersion_Throws() =>
        Assert.Throws<FormatException>(() => UpdateChecker.ParseRelease("{\"tag_name\":\"nightly\"}"));

    [Fact]
    public void ReleaseDate_OnlyYearMonthDay()
    {
        Assert.Equal(new DateOnly(2026, 10, 19), AppInfo.ParseReleaseDate("2026-10-19"));
        Assert.Null(AppInfo.ParseReleaseDate("19/10/2026"));
        Assert.Null(AppInfo.ParseReleaseDate(null));
    }

    [Fact]
    public void DiagnosticHeader_HasNameVersionWindowsAndBothArchitectures()
    {
        Assert.StartsWith("cobihw " + AppInfo.VersionText, AppInfo.DiagnosticHeader, StringComparison.Ordinal);
        Assert.Contains($"Windows {Environment.OSVersion.Version} {RuntimeInformation.OSArchitecture}", AppInfo.DiagnosticHeader, StringComparison.Ordinal);
        Assert.EndsWith($"· app {RuntimeInformation.ProcessArchitecture}", AppInfo.DiagnosticHeader, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalOrOnline_PrefersTheFileNextToTheExe()
    {
        string fileName = "cobihw-test-" + Guid.NewGuid().ToString("N") + ".txt";
        string path = Path.Combine(AppContext.BaseDirectory, fileName);

        Assert.Equal("https://example.com/online", AppInfo.LocalOrOnline(fileName, "https://example.com/online"));

        File.WriteAllText(path, "MIT");
        try
        {
            Assert.Equal(path, AppInfo.LocalOrOnline(fileName, "https://example.com/online"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NewVersion_IsAnnounced_AndDownloadOpensItsPage()
    {
        using AboutViewModel about = About(_ => Task.FromResult<ReleaseInfo?>(new(new Version(9, 1, 0), NewReleasePage, PackagesForThisApp)));
        Assert.False(about.HasUpdateStatus);
        Assert.Equal("", about.UpdateStatus);

        await about.CheckUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(UpdateState.Available, about.UpdateState);
        Assert.Equal("A versão 9.1.0 está disponível.", about.UpdateStatus);
        Assert.True(about.HasDownload);

        about.OpenDownloadCommand.Execute(null);
        Assert.Equal(NewReleasePage, _opened[^1]);
    }

    [Fact]
    public async Task NewVersion_WithoutAPackageForThisArchitecture_IsNotOffered()
    {
        Architecture other = RuntimeInformation.ProcessArchitecture == Architecture.X86 ? Architecture.X64 : Architecture.X86;
        using AboutViewModel about = About(_ => Task.FromResult<ReleaseInfo?>(new(new Version(9, 1, 0), NewReleasePage, ["cobihw-9.1.0-portable" + UpdateChecker.PackageSuffix(other)])));

        await about.CheckUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(UpdateState.UpToDate, about.UpdateState);
        Assert.False(about.HasDownload);
    }

    [Fact]
    public async Task SameVersion_IsUpToDate_WithoutDownload()
    {
        using AboutViewModel about = About(_ => Task.FromResult<ReleaseInfo?>(new(AppInfo.Version, AppInfo.ReleasesUrl, PackagesForThisApp)));

        await about.CheckUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("Você está na versão mais recente.", about.UpdateStatus);
        Assert.False(about.HasDownload);
    }

    [Fact]
    public async Task NoRelease_PublishedYet()
    {
        using AboutViewModel about = About(_ => Task.FromResult<ReleaseInfo?>(null));

        await about.CheckUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(UpdateState.NoRelease, about.UpdateState);
    }

    [Fact]
    public async Task Offline_ShowsAFailure_AndTheTextFollowsTheLanguage()
    {
        using AboutViewModel about = About(_ => Task.FromException<ReleaseInfo?>(new HttpRequestException("offline")));

        await about.CheckUpdatesCommand.ExecuteAsync(null);
        Assert.Equal("Não foi possível verificar agora. Tente de novo mais tarde.", about.UpdateStatus);

        SetLanguage("en-US");
        Assert.Equal("Could not check right now. Try again later.", about.UpdateStatus);
    }

    [Fact]
    public async Task Timeout_IsAFailureToo()
    {
        using AboutViewModel about = About(_ => Task.FromException<ReleaseInfo?>(new TaskCanceledException("timeout")));

        await about.CheckUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(UpdateState.Failed, about.UpdateState);
    }

    [Fact]
    public void Links_OpenTheLicenseNoticesSiteAndRepository()
    {
        using AboutViewModel about = About(_ => Task.FromResult<ReleaseInfo?>(null));

        about.OpenLicenseCommand.Execute(null);
        about.OpenNoticesCommand.Execute(null);
        about.OpenWebsiteCommand.Execute(null);
        about.OpenRepositoryCommand.Execute(null);

        Assert.Equal(
            new[]
            {
                AppInfo.LocalOrOnline(AppInfo.LicenseFile, AppInfo.LicenseUrl),
                AppInfo.LocalOrOnline(AppInfo.NoticesFile, AppInfo.NoticesUrl),
                "https://cobihw.com",
                "https://github.com/leonardocobianqui/cobihw",
            },
            _opened);
    }

    [Fact]
    public void Copyright_MentionsTheLicense()
    {
        using AboutViewModel about = About(_ => Task.FromResult<ReleaseInfo?>(null));

        Assert.EndsWith("· Licença MIT", about.CopyrightText, StringComparison.Ordinal);
    }
}
