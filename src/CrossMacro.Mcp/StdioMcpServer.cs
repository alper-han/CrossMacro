namespace CrossMacro.Mcp;

/// <summary>
/// Runs one local MCP stdio session. The SDK owns stdout; callers must route
/// all diagnostics through stderr or file logging before creating this server.
/// </summary>
public sealed class StdioMcpServer(
    CrossMacroMcpTools tools,
    IMcpCapabilityPolicy capabilityPolicy,
    McpRequestGuard requestGuard) : IMcpServer
{
    private readonly CrossMacroMcpTools _tools = tools;
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;
    private readonly McpRequestGuard _requestGuard = requestGuard;

    public async Task RunAsync(CancellationToken cancellationToken, bool restricted = false)
    {
        _capabilityPolicy.SetRestricted(restricted);
        var services = new ServiceCollection();
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
                    token)))
            .WithTools(_tools, McpJsonContext.Default.Options);

        var provider = services.BuildServiceProvider(validateScopes: true);
        await using (provider.ConfigureAwait(false))
        {
            await provider.GetRequiredService<McpServer>().RunAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string GetVersion() => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
}
