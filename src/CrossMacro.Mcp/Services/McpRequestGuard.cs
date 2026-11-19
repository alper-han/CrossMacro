using CrossMacro.Core.Logging;
using CrossMacro.Mcp.Contracts.Operations;

namespace CrossMacro.Mcp.Services;

/// <summary>
/// Rejects effectful tool calls whose catalog capabilities are not enabled before the tool body runs.
/// </summary>
public sealed class McpRequestGuard(IMcpCapabilityPolicy capabilityPolicy)
{
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;

    public ValueTask<CallToolResult> InvokeAsync(
        string toolName,
        Func<ValueTask<CallToolResult>> next,
        IDictionary<string, JsonElement>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(next);

        var definition = CrossMacroMcpToolCatalog.V1.FirstOrDefault(
            tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal));
        if (definition is null)
        {
            Log.Warning("MCP call rejected: the requested tool is not registered.");
            return ValueTask.FromResult(CreateErrorResult(McpToolOutcomeMapper.ToolNotAllowed()));
        }

        var requirements = GetEffectiveCapabilityRequirements(definition, arguments);
        if (definition.Access is McpToolAccess.Effectful
            && requirements.Capabilities.Count > 0
            && !IsCapabilityAllowed(requirements))
        {
            Log.Warning("MCP call to {ToolName} denied: required capability is not enabled.", definition.Name);
            return ValueTask.FromResult(CreateErrorResult(GetDeniedCapability(requirements)));
        }

        return next();
    }

    private bool IsCapabilityAllowed((IReadOnlyList<McpCapability> Capabilities, McpCapabilityRequirement Requirement) requirements) =>
        requirements.Requirement is McpCapabilityRequirement.Any
            ? _capabilityPolicy.IsAnyAllowed([.. requirements.Capabilities])
            : requirements.Capabilities.All(_capabilityPolicy.IsAllowed);

    private McpToolOutcome GetDeniedCapability((IReadOnlyList<McpCapability> Capabilities, McpCapabilityRequirement Requirement) requirements)
    {
        var deniedCapability = requirements.Requirement is McpCapabilityRequirement.Any
            ? requirements.Capabilities[0]
            : requirements.Capabilities.First(capability => !_capabilityPolicy.IsAllowed(capability));
        return _capabilityPolicy.Require(deniedCapability);
    }

    private static (IReadOnlyList<McpCapability> Capabilities, McpCapabilityRequirement Requirement) GetEffectiveCapabilityRequirements(
        McpToolDefinition definition,
        IDictionary<string, JsonElement>? arguments)
    {
        if (definition.Name is not "automation.start")
        {
            return (definition.Capabilities, definition.CapabilityRequirement);
        }

        if (arguments is null)
        {
            return ([], McpCapabilityRequirement.All);
        }

        KeyValuePair<string, JsonElement> kindArgument = arguments.FirstOrDefault(argument =>
            string.Equals(argument.Key, "kind", StringComparison.OrdinalIgnoreCase));
        if (kindArgument.Key is null || kindArgument.Value.ValueKind is not JsonValueKind.String)
        {
            return ([], McpCapabilityRequirement.All);
        }

        var kind = kindArgument.Value.GetString()?.Trim();
        var operation = definition.OperationCapabilities.FirstOrDefault(candidate =>
            string.Equals(candidate.Operation, kind, StringComparison.OrdinalIgnoreCase));
        return operation is null
            ? ([], McpCapabilityRequirement.All)
            : (operation.Capabilities, McpCapabilityRequirement.All);
    }

    private static CallToolResult CreateErrorResult(McpToolOutcome outcome) => new()
    {
        Content = [new TextContentBlock { Text = outcome.Message }],
        IsError = true,
    };
}
