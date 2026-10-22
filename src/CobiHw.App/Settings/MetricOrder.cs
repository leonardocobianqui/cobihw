namespace CobiHw.App.Settings;

/// <summary>Items the widget can show.</summary>
/// <remarks>Saved by name, so new items can be inserted anywhere without breaking saved orders.</remarks>
public enum MetricItem
{
    Cpu,
    Gpu,
    Memory,
    Disk,
    Network,
    Battery,
    Fps,
}

/// <summary>Order of the widget items; always complete and without duplicates.</summary>
public sealed record MetricOrder
{
    /// <summary>Items the user can reorder; FPS is always first, so it isn't one of them.</summary>
    public static readonly IReadOnlyList<MetricItem> Orderable = [.. Enum.GetValues<MetricItem>().Where(item => item != MetricItem.Fps)];

    /// <summary>CPU, GPU, RAM, disk, network, battery.</summary>
    public static readonly MetricOrder Default = new(Orderable);

    /// <summary>Builds an order, dropping unknown and duplicate items and appending missing ones (e.g. items added in a newer version).</summary>
    public MetricOrder(IReadOnlyList<MetricItem> items)
    {
        List<MetricItem> valid = items.Where(Orderable.Contains).Distinct().ToList();
        valid.AddRange(Orderable.Where(item => !valid.Contains(item)));
        Items = valid.AsReadOnly();
    }

    /// <summary>Items in display order.</summary>
    public IReadOnlyList<MetricItem> Items { get; }

    /// <summary>Compares the item sequence (records compare lists by reference otherwise).</summary>
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
