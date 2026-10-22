using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;

namespace CobiHw.App.About;

/// <summary>Result of the update check.</summary>
public enum UpdateState
{
    /// <summary>Not checked yet.</summary>
    NotChecked,
    /// <summary>Checking now.</summary>
    Checking,
    /// <summary>This is the latest version.</summary>
    UpToDate,
    /// <summary>A newer version exists.</summary>
    Available,
    /// <summary>No release published yet.</summary>
    NoRelease,
    /// <summary>The check failed.</summary>
    Failed,
}

/// <summary>State of the About window: version, links and the update check.</summary>
public sealed partial class AboutViewModel : ObservableObject, IDisposable
{
    private readonly Func<CancellationToken, Task<ReleaseInfo?>> _latestRelease;
    private readonly Action<string> _open;
    // Cancels a running update check when the window closes.
    private readonly CancellationTokenSource _closing = new();
    private ReleaseInfo? _latest;

    /// <summary>Creates the state.</summary>
    /// <param name="latestRelease">Fetches the latest release.</param>
    /// <param name="open">Opens a link or file.</param>
    public AboutViewModel(Func<CancellationToken, Task<ReleaseInfo?>> latestRelease, Action<string> open)
    {
        _latestRelease = latestRelease;
        _open = open;
        Localizer.Instance.CultureChanged += OnCultureChanged;
    }

    /// <summary>Current state of the update check.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateStatus), nameof(HasUpdateStatus), nameof(HasDownload))]
    private UpdateState _updateState;

    /// <summary>Version and release date.</summary>
    public string VersionText => AppInfo.ReleaseDate is DateOnly date
        ? Localizer.Format(Strings.Format_AboutVersion, AppInfo.VersionText, date.ToString("d", Localizer.Instance.Culture))
        : Localizer.Format(Strings.Format_AboutVersionOnly, AppInfo.VersionText);

    /// <summary>Copyright line.</summary>
    public string CopyrightText => Localizer.Format(Strings.Format_AboutCopyright, AppInfo.Copyright);

    /// <summary>Text of the update check result.</summary>
    public string UpdateStatus => UpdateState switch
    {
        UpdateState.Checking => Strings.About_Checking,
        UpdateState.UpToDate => Strings.About_UpToDate,
        UpdateState.Available when _latest is not null => Localizer.Format(Strings.Format_AboutUpdateAvailable, _latest.Version.ToString(3)),
        UpdateState.NoRelease => Strings.About_NoRelease,
        UpdateState.Failed => Strings.About_CheckFailed,
        _ => string.Empty,
    };

    /// <summary>Shows the update status line.</summary>
    public bool HasUpdateStatus => UpdateState != UpdateState.NotChecked;

    /// <summary>Shows the Download button.</summary>
    public bool HasDownload => UpdateState == UpdateState.Available;

    /// <summary>Cancels the update check and stops listening for language changes.</summary>
    public void Dispose()
    {
        Localizer.Instance.CultureChanged -= OnCultureChanged;
        _closing.Cancel();
        _closing.Dispose();
    }

    /// <summary>Checks GitHub for a newer release, only when the user asks.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task CheckUpdatesAsync()
    {
        UpdateState = UpdateState.Checking;

        try
        {
            _latest = await _latestRelease(_closing.Token);
            UpdateState = _latest is null ? UpdateState.NoRelease
                : UpdateChecker.IsNewer(_latest.Version, AppInfo.Version) ? UpdateState.Available
                : UpdateState.UpToDate;
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            UpdateState = UpdateState.Failed;
        }
    }

    /// <summary>Opens the website.</summary>
    [RelayCommand]
    private void OpenWebsite() => _open(AppInfo.WebsiteUrl);

    /// <summary>Opens the GitHub repository.</summary>
    [RelayCommand]
    private void OpenRepository() => _open(AppInfo.RepositoryUrl);

    /// <summary>Opens the page of the newer release.</summary>
    [RelayCommand]
    private void OpenDownload() => _open(_latest?.PageUrl ?? AppInfo.ReleasesUrl);

    /// <summary>Opens the license, preferring the file next to the .exe.</summary>
    [RelayCommand]
    private void OpenLicense() => _open(AppInfo.LocalOrOnline(AppInfo.LicenseFile, AppInfo.LicenseUrl));

    /// <summary>Opens the third-party licenses, preferring the file next to the .exe.</summary>
    [RelayCommand]
    private void OpenNotices() => _open(AppInfo.LocalOrOnline(AppInfo.NoticesFile, AppInfo.NoticesUrl));

    // An empty property name refreshes every binding.
    private void OnCultureChanged(object? sender, EventArgs e) => OnPropertyChanged(new PropertyChangedEventArgs(string.Empty));
}
