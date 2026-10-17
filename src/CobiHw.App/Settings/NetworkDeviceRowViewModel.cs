using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Resources;
using CobiHw.Core.Network;

namespace CobiHw.App.Settings;

public sealed partial class NetworkDeviceRowViewModel : ObservableObject
{
    public NetworkDeviceRowViewModel(NetworkDeviceInfo device) => Device = device;

    public NetworkDeviceInfo Device { get; }

    public Guid Id => Device.Id;

    public string Name => Device.Name;

    public bool IsInternal => Device.IsInternal;

    public string Detail => string.Join(" · ", new[]
    {
        Device.Description,
        DisplayFormat.DeviceKind(Device.Kind),
        Device.IsConnected ? null : Strings.Status_Disconnected,
    }.Where(part => !string.IsNullOrEmpty(part)));

    [ObservableProperty] private bool _isMonitored;

    [ObservableProperty] private bool _isShown = true;

    public void RefreshTexts() => OnPropertyChanged(nameof(Detail));
}

public sealed record NetworkDefaultOption(Guid? Id, string Name, string Label)
{
    public NetworkDeviceRef? ToReference() => Id is Guid id ? new NetworkDeviceRef(id, Name) : null;
}
