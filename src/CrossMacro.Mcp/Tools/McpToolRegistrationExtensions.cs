namespace CrossMacro.Mcp.Tools;

internal static class McpToolRegistrationExtensions
{
    public static IMcpServerBuilder WithCrossMacroTools(
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
        McpScreenTools screenTools)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithTools<McpRuntimeTools>(runtimeTools, McpJsonContext.Default.Options)
            .WithTools<McpSettingsTools>(settingsTools, McpJsonContext.Default.Options)
            .WithTools<McpProfileTools>(profileTools, McpJsonContext.Default.Options)
            .WithTools<McpTextExpansionTools>(textExpansionTools, McpJsonContext.Default.Options)
            .WithTools<McpTaskTools>(taskTools, McpJsonContext.Default.Options)
            .WithTools<McpAutomationTools>(automationTools, McpJsonContext.Default.Options)
            .WithTools<McpCommandTools>(commandTools, McpJsonContext.Default.Options)
            .WithTools<McpMacroTools>(macroTools, McpJsonContext.Default.Options)
            .WithTools<McpClipboardTools>(clipboardTools, McpJsonContext.Default.Options)
            .WithTools<McpWindowTools>(windowTools, McpJsonContext.Default.Options)
            .WithTools<McpScreenTools>(screenTools, McpJsonContext.Default.Options);
    }
}
