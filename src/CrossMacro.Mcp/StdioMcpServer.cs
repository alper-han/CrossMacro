namespace CrossMacro.Mcp;

/// <summary>
/// Runs one local MCP stdio session. The SDK owns stdout; callers must route
/// all diagnostics through stderr or file logging before creating this server.
/// </summary>
public sealed class StdioMcpServer(
    IMcpCapabilityPolicy capabilityPolicy,
    McpRequestGuard requestGuard,
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
    McpScreenTools screenTools) : IMcpServer
{
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;
    private readonly McpRequestGuard _requestGuard = requestGuard;
    private readonly McpRuntimeTools _runtimeTools = runtimeTools;
    private readonly McpSettingsTools _settingsTools = settingsTools;
    private readonly McpProfileTools _profileTools = profileTools;
    private readonly McpTextExpansionTools _textExpansionTools = textExpansionTools;
    private readonly McpTaskTools _taskTools = taskTools;
    private readonly McpAutomationTools _automationTools = automationTools;
    private readonly McpCommandTools _commandTools = commandTools;
    private readonly McpMacroTools _macroTools = macroTools;
    private readonly McpClipboardTools _clipboardTools = clipboardTools;
    private readonly McpWindowTools _windowTools = windowTools;
    private readonly McpScreenTools _screenTools = screenTools;

    public async Task RunAsync(CancellationToken cancellationToken, bool restricted = false)
    {
        _capabilityPolicy.SetRestricted(restricted);
        var services = new ServiceCollection();
        _ = services.AddSingleton(_runtimeTools);
        _ = services.AddSingleton(_settingsTools);
        _ = services.AddSingleton(_profileTools);
        _ = services.AddSingleton(_textExpansionTools);
        _ = services.AddSingleton(_taskTools);
        _ = services.AddSingleton(_automationTools);
        _ = services.AddSingleton(_commandTools);
        _ = services.AddSingleton(_macroTools);
        _ = services.AddSingleton(_clipboardTools);
        _ = services.AddSingleton(_windowTools);
        _ = services.AddSingleton(_screenTools);
        _ = services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "CrossMacro",
                    Title = "CrossMacro MCP",
                    Version = GetVersion(),
                    Description = "Local desktop automation tools for CrossMacro.",
                };
            })
            .WithStdioServerTransport()
            .WithRequestFilters(filters => filters.AddCallToolFilter(next =>
                (context, token) => _requestGuard.InvokeAsync(
                    context.Params.Name,
                    () => next(context, token),
                    token,
                    context.Params.Arguments)))
            .WithCrossMacroTools(
                _runtimeTools,
                _settingsTools,
                _profileTools,
                _textExpansionTools,
                _taskTools,
                _automationTools,
                _commandTools,
                _macroTools,
                _clipboardTools,
                _windowTools,
                _screenTools);

        var provider = services.BuildServiceProvider(validateScopes: true);
        await using (provider.ConfigureAwait(false))
        {
            await provider.GetRequiredService<McpServer>().RunAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string GetVersion() => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
}
