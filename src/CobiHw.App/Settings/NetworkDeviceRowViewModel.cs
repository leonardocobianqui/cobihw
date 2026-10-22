using CommunityToolkit.Mvvm.ComponentModel;
using CobiHw.App.Resources;
using CobiHw.Core.Network;

namespace CobiHw.App.Settings;

/// <summary>One device row in the Network tab.</summary>
public sealed partial class NetworkDeviceRowViewModel : ObservableObject
{
    public NetworkDeviceRowViewModel(NetworkDeviceInfo device) => Device = device;

    /// <summary>Device details.</summary>
    public NetworkDeviceInfo Device { get; }

    public Guid Id => Device.Id;

    public string Name => Device.Name;

    public bool IsInternal => Device.IsInternal;

    /// <summary>Second line: description, kind and "disconnected" when it applies.</summary>
    public string Detail => string.Join(" · ", new[]
    {
        Device.Description,
        DisplayFormat.DeviceKind(Device.Kind),
        Device.IsConnected ? null : Strings.Status_Disconnected,
    }.Where(part => !string.IsNullOrEmpty(part)));

    // Checkbox: list the device in "Network devices" and allow it as default.
    [ObservableProperty] private bool _isMonitored;

    // False while "show hidden devices" is off and Windows hides this one.
    [ObservableProperty] private bool _isShown = true;

    /// <summary>Updates the texts after a language change.</summary>
    public void RefreshTexts() => OnPropertyChanged(nameof(Detail));
}

/// <summary>One option of the "default device" box.</summary>
/// <param name="Id">Interface GUID; null = sum of devices.</param>
/// <param name="Name">Device name to save.</param>
/// <param name="Label">Text shown in the box.</param>
public sealed record NetworkDefaultOption(Guid? Id, string Name, string Label)
{
    /// <summary>Reference to save; null for the sum.</summary>
    public NetworkDeviceRef? ToReference() => Id is Guid id ? new NetworkDeviceRef(id, Name) : null;
}
