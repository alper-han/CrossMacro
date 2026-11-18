namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Lock-protected set of currently pressed inputs. The read-only snapshot is built lazily and only when
/// the set changed since the last read, so press/release on the playback hot path never allocates.
/// </summary>
internal sealed class PressedSet<T> where T : notnull
{
    private readonly HashSet<T> _items = [];
    private readonly Lock _lock = new();
    private ReadOnlyCollection<T>? _snapshot;

    public bool IsAny
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _items.Count > 0;
            }
        }
    }

    public IReadOnlyCollection<T> Snapshot
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _snapshot ??= new ReadOnlyCollection<T>([.. _items]);
            }
        }
    }

    public void Add(T item)
    {
        using (_lock.EnterScope())
        {
            if (_items.Add(item))
            {
                _snapshot = null;
            }
        }
    }

    public void Remove(T item)
    {
        using (_lock.EnterScope())
        {
            if (_items.Remove(item))
            {
                _snapshot = null;
            }
        }
    }

    public void Clear()
    {
        using (_lock.EnterScope())
        {
            if (_items.Count > 0)
            {
                _items.Clear();
                _snapshot = null;
            }
        }
    }

    /// <summary>Atomically empties the set and returns what it contained.</summary>
    public T[] TakeAll()
    {
        using (_lock.EnterScope())
        {
            if (_items.Count is 0)
            {
                return [];
            }

            T[] items = [.. _items];
            _items.Clear();
            _snapshot = null;
            return items;
        }
    }
}
