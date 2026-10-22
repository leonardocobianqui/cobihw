using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CobiHw.App;

/// <summary>Name, version, links and diagnostic info of the app.</summary>
internal static class AppInfo
{
    /// <summary>App name.</summary>
    public const string Name = "cobihw";
    /// <summary>Project website.</summary>
    public const string WebsiteUrl = "https://cobihw.com";
    /// <summary>Source code on GitHub.</summary>
    public const string RepositoryUrl = "https://github.com/leonardocobianqui/cobihw";
    /// <summary>Download page with every release.</summary>
    public const string ReleasesUrl = RepositoryUrl + "/releases";
    /// <summary>GitHub API that returns the latest release; used by the update check.</summary>
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/leonardocobianqui/cobihw/releases/latest";
    /// <summary>License file shipped next to the .exe.</summary>
    public const string LicenseFile = "LICENSE.txt";
    /// <summary>Third-party licenses shipped next to the .exe.</summary>
    public const string NoticesFile = "THIRD-PARTY-NOTICES.txt";
    /// <summary>Online license, used when the local file is missing.</summary>
    public const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSE";
    /// <summary>Online third-party licenses, used when the local file is missing.</summary>
    public const string NoticesUrl = RepositoryUrl + "/blob/main/THIRD-PARTY-NOTICES.txt";

    private static readonly Assembly Assembly = typeof(AppInfo).Assembly;

    /// <summary>Version from the assembly, always with three parts (major.minor.patch).</summary>
    public static Version Version { get; } = Normalize(Assembly.GetName().Version ?? new Version(0, 0, 0));

    /// <summary>Version as "1.2.3".</summary>
    public static string VersionText => Version.ToString(3);

    /// <summary>Release date set in Directory.Build.props, or null when missing.</summary>
    public static DateOnly? ReleaseDate { get; } = ReadReleaseDate();

    /// <summary>Copyright line from the assembly.</summary>
    public static string Copyright { get; } = Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

    /// <summary>First line of error reports: version, release date, Windows version and architecture.</summary>
    public static string DiagnosticHeader =>
        $"{Name} {VersionText} ({ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "?"}) · Windows {Environment.OSVersion.Version} · {RuntimeInformation.OSArchitecture}";

    /// <summary>Drops the fourth part and turns a missing patch (-1) into 0, so versions compare correctly.</summary>
    /// <param name="version">Version to normalize.</param>
    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    /// <summary>Reads a "yyyy-MM-dd" date; null when the text is missing or invalid.</summary>
    /// <param name="text">Date text.</param>
    public static DateOnly? ParseReleaseDate(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date) ? date : null;

    /// <summary>Path of a file next to the .exe, or the online address when the file is missing.</summary>
    /// <param name="fileName">File name next to the .exe.</param>
    /// <param name="onlineUrl">Address used when the file is missing.</param>
    public static string LocalOrOnline(string fileName, string onlineUrl)
    {
        string path = Path.Combine(AppContext.BaseDirectory, fileName);
        return File.Exists(path) ? path : onlineUrl;
    }

    private static DateOnly? ReadReleaseDate() =>
        ParseReleaseDate(Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(attribute => attribute.Key == "ReleaseDate")?.Value);
}
