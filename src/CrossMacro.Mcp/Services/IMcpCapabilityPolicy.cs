namespace CrossMacro.Mcp.Services;

public interface IMcpCapabilityPolicy
{
    public bool IsRestricted { get; }

    public bool IsAllowed(McpCapability capability);

    public bool IsAnyAllowed(params McpCapability[] capabilities);

    public McpToolOutcome Require(McpCapability capability);

    public void SetRestricted(bool restricted);
}
