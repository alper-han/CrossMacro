namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal static class PortalPipeWireConnectionRegistry
{
    private sealed class Entry(PortalPipeWireConnection connection)
    {
        public PortalPipeWireConnection Connection { get; } = connection;
        public int References { get; set; } = 1;
    }

    private static readonly Lock Gate = new();
    private static readonly Dictionary<SafeFileHandle, Entry> Connections = new(ReferenceEqualityComparer.Instance);

    public static PortalPipeWireConnectionLease Acquire(SafeFileHandle remote)
    {
        ArgumentNullException.ThrowIfNull(remote);
        if (remote.IsClosed)
        {
            throw new ArgumentException("PipeWire remote handle is closed.", nameof(remote));
        }

        lock (Gate)
        {
            if (Connections.TryGetValue(remote, out var existing))
            {
                existing.References++;
                return new PortalPipeWireConnectionLease(existing.Connection, () => Release(remote, existing.Connection));
            }

            var connection = new PortalPipeWireConnection(remote);
            Connections.Add(remote, new Entry(connection));
            return new PortalPipeWireConnectionLease(connection, () => Release(remote, connection));
        }
    }

    private static void Release(SafeFileHandle remote, PortalPipeWireConnection connection)
    {
        lock (Gate)
        {
            if (!Connections.TryGetValue(remote, out var entry) || !ReferenceEquals(entry.Connection, connection))
            {
                return;
            }

            entry.References--;
            if (entry.References is 0 && Connections.Remove(remote))
            {
                connection.Dispose();
            }
        }
    }
}
