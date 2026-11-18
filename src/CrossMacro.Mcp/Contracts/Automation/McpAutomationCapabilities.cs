namespace CrossMacro.Mcp.Contracts.Automation;

/// <summary>
/// The capabilities each automation operation requires, regardless of whether it is started through
/// <c>automation.start</c> or <c>command.execute</c>.
/// </summary>
public static class McpAutomationCapabilities
{
    private static readonly McpCapability[] Play = [McpCapability.MacroRead, McpCapability.InputAutomation];
    private static readonly McpCapability[] Run = [McpCapability.CommandExecute, McpCapability.InputAutomation];
    private static readonly McpCapability[] Record = [McpCapability.Recording, McpCapability.FileWrite];

    public static IReadOnlyList<McpCapability> For(McpAutomationOperationKind kind) => kind switch
    {
        McpAutomationOperationKind.Play => Play,
        McpAutomationOperationKind.Run => Run,
        McpAutomationOperationKind.Record => Record,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown automation operation kind."),
    };
}
