namespace CrossMacro.Platform.MacOS.Services;

internal sealed class MacOSWindowRegistry(
    Func<IntPtr, IntPtr> retain,
    Func<IntPtr, bool> release,
    Func<IntPtr, IntPtr, bool> elementsEqual) : IDisposable
{
    private readonly Lock _lock = new();
    private readonly Func<IntPtr, IntPtr> _retain = retain ?? throw new ArgumentNullException(nameof(retain));
    private readonly Func<IntPtr, bool> _release = release ?? throw new ArgumentNullException(nameof(release));
    private readonly Func<IntPtr, IntPtr, bool> _elementsEqual = elementsEqual ?? throw new ArgumentNullException(nameof(elementsEqual));
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly string _addressSuffixPrefix = string.Concat(
        ".",
        Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
        "-");
    private long _nextId;
    private bool _disposed;

    public string Register(IntPtr element, MacOSWindowAddress fallbackAddress)
    {
        if (element == IntPtr.Zero || fallbackAddress.Pid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(element));
        }

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (var pair in _entries)
            {
                if (pair.Value.Pid == fallbackAddress.Pid
                    && _elementsEqual(pair.Value.Handle.Value, element))
                {
                    return pair.Key;
                }
            }

            var retained = _retain(element);
            if (retained == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to retain the macOS Accessibility window element.");
            }

            var address = string.Create(
                CultureInfo.InvariantCulture,
                $"{fallbackAddress.Format()}{_addressSuffixPrefix}{checked(++_nextId)}");
            _entries.Add(
                address,
                new Entry(fallbackAddress.Pid, new MacOSCfSafeHandle(retained, _release)));
            return address;
        }
    }

    public bool TryUse(
        string address,
        Func<IntPtr, int, bool> operation,
        out bool operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        operationResult = false;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        lock (_lock)
        {
            if (_disposed || !_entries.TryGetValue(address, out var entry))
            {
                return false;
            }

            operationResult = operation(entry.Handle.Value, entry.Pid);
            return true;
        }
    }

    public void Remove(string address)
    {
        lock (_lock)
        {
            if (_entries.Remove(address, out var entry))
            {
                entry.Handle.Dispose();
            }
        }
    }

    public bool WasIssuedByThisRegistry(string address) =>
        !string.IsNullOrWhiteSpace(address)
        && address.Contains(_addressSuffixPrefix, StringComparison.Ordinal);

    public void PruneExcept(IReadOnlySet<string> activeAddresses)
    {
        ArgumentNullException.ThrowIfNull(activeAddresses);
        lock (_lock)
        {
            var staleAddresses = _entries.Keys
                .Where(address => !activeAddresses.Contains(address))
                .ToArray();
            foreach (var address in staleAddresses)
            {
                var entry = _entries[address];
                _ = _entries.Remove(address);
                entry.Handle.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var entry in _entries.Values)
            {
                entry.Handle.Dispose();
            }

            _entries.Clear();
        }
    }

    private sealed record Entry(int Pid, MacOSCfSafeHandle Handle);
}
