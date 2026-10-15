namespace CobiHw.App.Settings;

public enum MetricItem
{
    Cpu,
    Gpu,
    Memory,
    Disk,
    Network,
    Fps,
}

public sealed record MetricOrder
{
    public static readonly IReadOnlyList<MetricItem> Orderable = [.. Enum.GetValues<MetricItem>().Where(item => item != MetricItem.Fps)];

    public static readonly MetricOrder Default = new(Orderable);

    public MetricOrder(IReadOnlyList<MetricItem> items)
    {
        List<MetricItem> valid = items.Where(Orderable.Contains).Distinct().ToList();
        valid.AddRange(Orderable.Where(item => !valid.Contains(item)));
        Items = valid.AsReadOnly();
    }

    public IReadOnlyList<MetricItem> Items { get; }

    public bool Equals(MetricOrder? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (MetricItem item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
