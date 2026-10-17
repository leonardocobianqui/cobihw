namespace CobiHw.App.Settings;

public sealed record NetworkSettings
{
    public const string GooglePingHost = "8.8.8.8";
    public const string CloudflarePingHost = "1.1.1.1";

    public string PingHost { get; set; } = GooglePingHost;
    public NetworkDeviceChoices Devices { get; set; } = NetworkDeviceChoices.Empty;
    public NetworkDeviceRef? DefaultDevice { get; set; }
}

public sealed record NetworkDeviceRef(Guid Id, string Name);

public sealed record NetworkDeviceChoice(Guid Id, bool Monitored);

public sealed record NetworkDeviceChoices
{
    public static readonly NetworkDeviceChoices Empty = new([]);

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

    public bool IsMonitored(Guid id, bool isInternal) =>
        Overrides.FirstOrDefault(choice => choice.Id == id)?.Monitored ?? !isInternal;

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
