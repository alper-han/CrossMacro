namespace CrossMacro.Mcp.Services;

public interface IMcpAuditStore
{
    public void Record(McpAuditEntry entry);

    public IReadOnlyList<McpAuditEntry> Snapshot();
}
