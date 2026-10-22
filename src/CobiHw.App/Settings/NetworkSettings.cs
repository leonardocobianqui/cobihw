namespace CobiHw.App.Settings;

/// <summary>Network tab: ping, monitored devices and the device shown in the widget.</summary>
public sealed record NetworkSettings
{
    public const string GooglePingHost = "8.8.8.8";
    public const string CloudflarePingHost = "1.1.1.1";

    /// <summary>Off by default: with ping off, the app makes no automatic connections.</summary>
    public bool PingEnabled { get; set; }
    /// <summary>IP address or host name to ping.</summary>
    public string PingHost { get; set; } = GooglePingHost;
    /// <summary>Devices the user checked or unchecked; the rest follow the default.</summary>
    public NetworkDeviceChoices Devices { get; set; } = NetworkDeviceChoices.Empty;
    /// <summary>Device shown in the widget; null = sum of physical devices.</summary>
    public NetworkDeviceRef? DefaultDevice { get; set; }
}

/// <summary>Saved reference to a network device; the name shows if the device is gone.</summary>
/// <param name="Id">Interface GUID.</param>
/// <param name="Name">Name when it was chosen.</param>
public sealed record NetworkDeviceRef(Guid Id, string Name);

/// <summary>A device the user checked or unchecked.</summary>
/// <param name="Id">Interface GUID.</param>
/// <param name="Monitored">True = checked.</param>
public sealed record NetworkDeviceChoice(Guid Id, bool Monitored);

/// <summary>Only the user's changes are saved; devices without a choice follow the default (visible = checked).</summary>
public sealed record NetworkDeviceChoices
{
    public static readonly NetworkDeviceChoices Empty = new([]);

    /// <summary>Keeps the last choice per device, sorted, so equal choices compare equal.</summary>
    public NetworkDeviceChoices(IReadOnlyList<NetworkDeviceChoice> overrides)
    {
        Overrides = overrides
            .GroupBy(choice => choice.Id)
            .Select(group => group.Last())
            .OrderBy(choice => choice.Id)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<NetworkDeviceChoice> Overrides { get; }

    /// <summary>The user's choice, or checked by default unless Windows hides the device.</summary>
    public bool IsMonitored(Guid id, bool isInternal) =>
        Overrides.FirstOrDefault(choice => choice.Id == id)?.Monitored ?? !isInternal;

    /// <summary>Compares the choices one by one.</summary>
    public bool Equals(NetworkDeviceChoices? other) => other is not null && Overrides.SequenceEqual(other.Overrides);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (NetworkDeviceChoice choice in Overrides)
        {
            hash.Add(choice);
        }

        return hash.ToHashCode();
    }
}
