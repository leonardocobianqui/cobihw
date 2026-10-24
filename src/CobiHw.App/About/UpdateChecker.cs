using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CobiHw.App.About;

/// <summary>Latest published release.</summary>
/// <param name="Version">Release version.</param>
/// <param name="PageUrl">Release page on GitHub.</param>
/// <param name="Files">Names of the files attached to the release.</param>
public sealed record ReleaseInfo(Version Version, string PageUrl, IReadOnlyList<string> Files)
{
    /// <summary>True when the release has a package for that processor architecture, e.g. "...-win-x86.zip".</summary>
    /// <param name="architecture">Architecture of the running app.</param>
    public bool HasPackageFor(Architecture architecture) =>
        Files.Any(name => name.EndsWith(UpdateChecker.PackageSuffix(architecture), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Reads the latest release from the GitHub API.</summary>
/// <param name="http">Shared HTTP client.</param>
public sealed class UpdateChecker(HttpClient http)
{
    // Only GitHub pages are opened from the API answer.
    private const string GitHubHost = "github.com";

    /// <summary>Fetches the latest release.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The release, or null when none is published (HTTP 404).</returns>
    public async Task<ReleaseInfo?> LatestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AppInfo.LatestReleaseApiUrl);
        // Headers recommended by the GitHub REST API docs.
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return ParseRelease(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <summary>Reads the version, page and file names from the API answer.</summary>
    /// <param name="json">Answer of the releases API.</param>
    /// <exception cref="FormatException">The tag isn't a version.</exception>
    public static ReleaseInfo ParseRelease(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        string? page = root.TryGetProperty("html_url", out JsonElement url) ? url.GetString() : null;
        List<string> files = root.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array
            ? [.. assets.EnumerateArray().Select(asset => asset.TryGetProperty("name", out JsonElement name) ? name.GetString() : null).OfType<string>()]
            : [];

        Version version = ParseVersion(tag) ?? throw new FormatException($"Unexpected release tag: {tag}");
        return new ReleaseInfo(version, IsGitHubPage(page) ? page! : AppInfo.ReleasesUrl, files);
    }

    /// <summary>End of the package names for an architecture, as publish.ps1 names them: "-win-x64.zip", "-win-x86.zip".</summary>
    /// <param name="architecture">Processor architecture.</param>
    public static string PackageSuffix(Architecture architecture) =>
        $"-win-{architecture.ToString().ToLowerInvariant()}.zip";

    /// <summary>Reads a tag such as "v1.2.3".</summary>
    /// <param name="tag">Release tag.</param>
    /// <returns>The version, or null when the tag isn't one.</returns>
    public static Version? ParseVersion(string tag) =>
        Version.TryParse(tag.Trim().TrimStart('v', 'V'), out Version? version) ? AppInfo.Normalize(version) : null;

    /// <summary>True when <paramref name="latest"/> is newer than <paramref name="current"/>.</summary>
    /// <param name="latest">Latest release.</param>
    /// <param name="current">Running version.</param>
    public static bool IsNewer(Version latest, Version current) => AppInfo.Normalize(latest) > AppInfo.Normalize(current);

    /// <summary>True when <paramref name="latest"/> is newer and has a package the running app can use.</summary>
    /// <param name="latest">Latest release.</param>
    /// <param name="current">Running version.</param>
    /// <param name="architecture">Architecture of the running app.</param>
    public static bool IsUpdate(ReleaseInfo latest, Version current, Architecture architecture) =>
        IsNewer(latest.Version, current) && latest.HasPackageFor(architecture);

    // Rejects other hosts, so a tampered answer can't open an arbitrary site.
    private static bool IsGitHubPage(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, GitHubHost, StringComparison.OrdinalIgnoreCase);
}
