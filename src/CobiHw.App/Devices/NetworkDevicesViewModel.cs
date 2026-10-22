using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CobiHw.App.Localization;
using CobiHw.App.Resources;
using CobiHw.App.Settings;
using CobiHw.Core;
using CobiHw.Core.Network;

namespace CobiHw.App.Devices;

/// <summary>State of the network devices window: one tab per monitored device, plus the public IP lookup.</summary>
public sealed partial class NetworkDevicesViewModel : ObservableObject, IDisposable
{
    private readonly Func<IReadOnlyList<NetworkDeviceInfo>> _listDevices;
    private readonly Func<Guid, WifiStatus> _readWifi;
    private readonly Func<CancellationToken, Task<string?>> _fetchPublicAddress;
    private NetworkSettings _settings;
    private NetworkSnapshot? _traffic;
    private IReadOnlyList<NetworkDeviceInfo> _devices = [];
    // Only the latest reload is applied when several overlap.
    private int _loadVersion;
    // Skips a Wi-Fi read while the previous one is still running.
    private bool _readingWifi;
    private bool _disposed;

    /// <summary>Creates the state; devices load with <see cref="ReloadAsync"/>.</summary>
    /// <param name="settings">Network settings, which decide the monitored devices.</param>
    /// <param name="listDevices">Lists the network devices.</param>
    /// <param name="readWifi">Reads the Wi-Fi details of a device.</param>
    /// <param name="fetchPublicAddress">Looks up the public IP address.</param>
    public NetworkDevicesViewModel(
        NetworkSettings settings,
        Func<IReadOnlyList<NetworkDeviceInfo>> listDevices,
        Func<Guid, WifiStatus> readWifi,
        Func<CancellationToken, Task<string?>> fetchPublicAddress)
    {
        _settings = settings;
        _listDevices = listDevices;
        _readWifi = readWifi;
        _fetchPublicAddress = fetchPublicAddress;
        Localizer.Instance.CultureChanged += OnCultureChanged;
    }

    /// <summary>One tab per monitored device.</summary>
    public ObservableCollection<NetworkDeviceTabViewModel> Tabs { get; } = [];

    /// <summary>True when there is a device to show.</summary>
    public bool HasTabs => Tabs.Count > 0;

    /// <summary>True when loading finished without devices.</summary>
    public bool HasNoTabs => !IsLoading && Tabs.Count == 0;

    /// <summary>True until the first list arrives.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTabs))]
    private bool _isLoading = true;

    /// <summary>Selected device.</summary>
    [ObservableProperty] private NetworkDeviceTabViewModel? _selectedTab;

    /// <summary>Public IP, or the lookup status.</summary>
    [ObservableProperty] private string _publicAddressText = DisplayFormat.Unavailable;

    /// <summary>Lists the devices again on a background thread.</summary>
    public async Task ReloadAsync()
    {
        int version = ++_loadVersion;
        IReadOnlyList<NetworkDeviceInfo> devices = await Task.Run(_listDevices);

        if (_disposed || version != _loadVersion)
        {
            return;
        }

        _devices = devices;
        IsLoading = false;
        RebuildTabs();
        await RefreshWifiAsync();
    }

    /// <summary>Shows new traffic data and refreshes the Wi-Fi details.</summary>
    /// <param name="traffic">Latest network reading.</param>
    public async Task UpdateAsync(NetworkSnapshot traffic)
    {
        _traffic = traffic;

        foreach (NetworkDeviceTabViewModel tab in Tabs)
        {
            tab.UpdateTraffic(traffic.Find(tab.Id));
        }

        await RefreshWifiAsync();
    }

    /// <summary>Applies new network settings; tabs may be added or removed.</summary>
    /// <param name="settings">New network settings.</param>
    public void UpdateSettings(NetworkSettings settings)
    {
        _settings = settings;
        RebuildTabs();
    }

    /// <summary>Stops updates and cancels a running public IP lookup.</summary>
    public void Dispose()
    {
        _disposed = true;
        Localizer.Instance.CultureChanged -= OnCultureChanged;
        DiscoverPublicAddressCommand.Cancel();
    }

    /// <summary>Looks up the public IP only when asked, since it contacts an outside service.</summary>
    [RelayCommand]
    private async Task DiscoverPublicAddressAsync(CancellationToken cancellationToken)
    {
        PublicAddressText = Strings.PublicIp_Searching;

        try
        {
            PublicAddressText = await _fetchPublicAddress(cancellationToken) ?? DisplayFormat.Unavailable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PublicAddressText = DisplayFormat.Unavailable;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            PublicAddressText = Localizer.Format(Strings.Format_PublicIpError, exception.Message);
        }
    }

    // Updates the tabs in place, so the selection survives.
    private void RebuildTabs()
    {
        List<NetworkDeviceInfo> monitored = [.. _devices.Where(device => _settings.Devices.IsMonitored(device.Id, device.IsInternal))];

        for (int index = Tabs.Count - 1; index >= 0; index--)
        {
            if (monitored.All(device => device.Id != Tabs[index].Id))
            {
                Tabs.RemoveAt(index);
            }
        }

        for (int index = 0; index < monitored.Count; index++)
        {
            NetworkDeviceInfo device = monitored[index];
            NetworkDeviceTabViewModel? tab = Tabs.FirstOrDefault(existing => existing.Id == device.Id);

            if (tab is null)
            {
                tab = new NetworkDeviceTabViewModel(device.Id);
                Tabs.Insert(index, tab);
            }
            else if (Tabs.IndexOf(tab) != index)
            {
                Tabs.Move(Tabs.IndexOf(tab), index);
            }

            tab.Update(device);
            tab.UpdateTraffic(_traffic?.Find(device.Id));
        }

        if (SelectedTab is null || !Tabs.Contains(SelectedTab))
        {
            SelectedTab = Tabs.FirstOrDefault();
        }

        OnPropertyChanged(nameof(HasTabs));
        OnPropertyChanged(nameof(HasNoTabs));
    }

    // Reads Wi-Fi on a background thread; results for tabs removed meanwhile are dropped.
    private async Task RefreshWifiAsync()
    {
        List<NetworkDeviceTabViewModel> tabs = [.. Tabs.Where(tab => tab.NeedsWifiReading)];

        if (_readingWifi || tabs.Count == 0)
        {
            return;
        }

        _readingWifi = true;

        try
        {
            List<Guid> ids = [.. tabs.Select(tab => tab.Id)];
            WifiStatus[] statuses = await Task.Run(() => ids.Select(_readWifi).ToArray());

            for (int index = 0; index < tabs.Count; index++)
            {
                if (!_disposed && Tabs.Contains(tabs[index]) && tabs[index].NeedsWifiReading)
                {
                    tabs[index].UpdateWifi(statuses[index]);
                }
            }
        }
        finally
        {
            _readingWifi = false;
        }
    }

    // A translated status message is cleared; a real IP address stays.
    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (NetworkDeviceTabViewModel tab in Tabs)
        {
            tab.RefreshTexts();
        }

        if (!DiscoverPublicAddressCommand.IsRunning && PublicAddressText.Length > 0 && !System.Net.IPAddress.TryParse(PublicAddressText, out _))
        {
            PublicAddressText = DisplayFormat.Unavailable;
        }
    }
}
