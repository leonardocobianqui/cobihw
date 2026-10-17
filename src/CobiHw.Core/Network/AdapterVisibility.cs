namespace CobiHw.Core.Network;

public sealed record VisibleAdapter(Guid Id, bool Hidden);

public sealed class AdapterVisibility(Func<IReadOnlyList<VisibleAdapter>?> query)
{
    private readonly Lock _gate = new();
    private readonly HashSet<Guid> _known = [];
    private readonly HashSet<Guid> _shown = [];
    private bool _available;

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
