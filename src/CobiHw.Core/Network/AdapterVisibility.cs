namespace CobiHw.Core.Network;

/// <summary>Whether Windows hides an adapter from its own network settings.</summary>
/// <param name="Id">Interface GUID.</param>
/// <param name="Hidden">True for adapters Windows doesn't show (filters, virtual miniports).</param>
public sealed record VisibleAdapter(Guid Id, bool Hidden);

/// <summary>
/// Caches which adapters Windows shows, refreshing only when an unknown adapter appears.
/// </summary>
/// <param name="query">Reads the adapter list; returns null when the information isn't available.</param>
/// <remarks>The WMI query is much slower than the interface table, so it isn't run on every refresh.</remarks>
public sealed class AdapterVisibility(Func<IReadOnlyList<VisibleAdapter>?> query)
{
    private readonly Lock _gate = new();
    private readonly HashSet<Guid> _known = [];
    private readonly HashSet<Guid> _shown = [];
    private bool _available;

    /// <summary>Adapters Windows shows, among <paramref name="ids"/>.</summary>
    /// <returns>The visible set, or <see langword="null"/> when Windows couldn't be asked.</returns>
    public IReadOnlySet<Guid>? Shown(IReadOnlyCollection<Guid> ids)
    {
        lock (_gate)
        {
            if (_known.Count == 0 || !_known.IsSupersetOf(ids))
            {
                Load(ids);
            }

            return _available ? new HashSet<Guid>(_shown) : null;
        }
    }

    private void Load(IReadOnlyCollection<Guid> ids)
    {
        IReadOnlyList<VisibleAdapter>? adapters = query();

        _known.Clear();
        _shown.Clear();
        _known.UnionWith(ids);
        _available = adapters is not null;

        foreach (VisibleAdapter adapter in adapters ?? [])
        {
            _known.Add(adapter.Id);

            if (!adapter.Hidden)
            {
                _shown.Add(adapter.Id);
            }
        }
    }
}
