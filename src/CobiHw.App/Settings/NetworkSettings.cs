namespace CobiHw.App.Settings;

public sealed record NetworkSettings
{
    public const string GooglePingHost = "8.8.8.8";
    public const string CloudflarePingHost = "1.1.1.1";

    public string PingHost { get; set; } = GooglePingHost;
}
