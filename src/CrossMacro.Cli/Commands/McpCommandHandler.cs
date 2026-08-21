namespace CrossMacro.Cli.Commands;

/// <summary>
/// Delegates the stdio session to the MCP outer adapter registered by the
/// executable composition root.
/// </summary>
public sealed class McpCommandHandler(IMcpServer mcpServer) : CliCommandHandlerBase<McpCliOptions>
{
    private readonly IMcpServer _mcpServer = mcpServer;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(
        McpCliOptions options,
        CancellationToken cancellationToken)
    {
        await _mcpServer.RunAsync(cancellationToken, options.Restricted).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok("MCP server stopped.");
    }
}
