namespace CrossMacro.Cli.Commands;

/// <summary>
/// Delegates the stdio session to the MCP outer adapter registered by the
/// executable composition root.
/// </summary>
public sealed class McpCommandHandler(IMcpServer mcpServer) : CliCommandHandlerBase<McpOptions>
{
    private readonly IMcpServer _mcpServer = mcpServer;

    protected override async Task<OperationResult> ExecuteAsync(
        McpOptions options,
        CancellationToken cancellationToken)
    {
        await _mcpServer.RunAsync(cancellationToken, options.Restricted).ConfigureAwait(false);
        return OperationResult.Ok("MCP server stopped.");
    }
}
