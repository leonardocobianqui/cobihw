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

public sealed partial class NetworkDevicesViewModel : ObservableObject, IDisposable
{
    private readonly Func<IReadOnlyList<NetworkDeviceInfo>> _listDevices;
    private readonly Func<Guid, WifiStatus> _readWifi;
    private readonly Func<CancellationToken, Task<string?>> _fetchPublicAddress;
    private NetworkSettings _settings;
    private NetworkSnapshot? _traffic;
    private IReadOnlyList<NetworkDeviceInfo> _devices = [];
    private int _loadVersion;
    private bool _readingWifi;
    private bool _disposed;

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

    public ObservableCollection<NetworkDeviceTabViewModel> Tabs { get; } = [];

    public bool HasTabs => Tabs.Count > 0;

    public bool HasNoTabs => !IsLoading && Tabs.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTabs))]
    private bool _isLoading = true;

    [ObservableProperty] private NetworkDeviceTabViewModel? _selectedTab;

    [ObservableProperty] private string _publicAddressText = DisplayFormat.Unavailable;

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

    public async Task UpdateAsync(NetworkSnapshot traffic)
    {
        _traffic = traffic;

        foreach (NetworkDeviceTabViewModel tab in Tabs)
        {
            tab.UpdateTraffic(traffic.Find(tab.Id));
        }

        await RefreshWifiAsync();
    }

    public void UpdateSettings(NetworkSettings settings)
    {
        _settings = settings;
        RebuildTabs();
    }

    public void Dispose()
    {
        _disposed = true;
        Localizer.Instance.CultureChanged -= OnCultureChanged;
        DiscoverPublicAddressCommand.Cancel();
    }

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
