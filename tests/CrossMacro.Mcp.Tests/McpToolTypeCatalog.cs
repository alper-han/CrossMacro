namespace CrossMacro.Mcp.Tests;

internal static class McpToolTypeCatalog
{
    public static IReadOnlyList<Type> All { get; } =
    [
        typeof(McpRuntimeTools),
        typeof(McpSettingsTools),
        typeof(McpProfileTools),
        typeof(McpTextExpansionTools),
        typeof(McpTaskTools),
        typeof(McpAutomationTools),
        typeof(McpCommandTools),
        typeof(McpMacroTools),
        typeof(McpClipboardTools),
        typeof(McpWindowTools),
        typeof(McpScreenTools),
    ];

    public static IMcpServerBuilder WithCrossMacroToolsForTests(
        this IMcpServerBuilder builder,
        McpRuntimeTools runtimeTools,
        McpSettingsTools settingsTools,
        McpProfileTools profileTools,
        McpTextExpansionTools textExpansionTools,
        McpTaskTools taskTools,
        McpAutomationTools automationTools,
        McpCommandTools commandTools,
        McpMacroTools macroTools,
        McpClipboardTools clipboardTools,
        McpWindowTools windowTools,
        McpScreenTools screenTools) =>
        builder.WithCrossMacroTools(
            runtimeTools,
            settingsTools,
            profileTools,
            textExpansionTools,
            taskTools,
            automationTools,
            commandTools,
            macroTools,
            clipboardTools,
            windowTools,
            screenTools);
}
