namespace CrossMacro.Mcp.Tests;

internal sealed class ThrowingCliCommandHandler(string detail) : ICliCommandHandler
{
    private readonly string _detail = detail;

    public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken) =>
        Task.FromException<CliCommandExecutionResult>(new InvalidOperationException(_detail));
}
