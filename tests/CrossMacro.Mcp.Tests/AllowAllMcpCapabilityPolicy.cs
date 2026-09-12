namespace CrossMacro.Mcp.Tests;

internal sealed class AllowAllMcpCapabilityPolicy : IMcpCapabilityPolicy
    {
        public bool IsRestricted => false;

        public bool IsAllowed(McpCapability capability) => true;

        public bool IsAnyAllowed(params McpCapability[] capabilities) => true;

        public McpToolOutcome Require(McpCapability capability) => McpToolOutcomeMapper.Success(string.Empty);

        public void SetRestricted(bool restricted)
        {
        }
    }
