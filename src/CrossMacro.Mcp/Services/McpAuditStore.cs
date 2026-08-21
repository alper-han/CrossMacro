namespace CrossMacro.Mcp.Services;

public sealed class McpAuditStore : IMcpAuditStore
{
    public const int MaximumEntries = 256;

    private readonly Lock _gate = new();
    private readonly Queue<McpAuditEntry> _entries = new();

    public void Record(McpAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > MaximumEntries)
            {
                _ = _entries.Dequeue();
            }
        }
    }

    public IReadOnlyList<McpAuditEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }
}
