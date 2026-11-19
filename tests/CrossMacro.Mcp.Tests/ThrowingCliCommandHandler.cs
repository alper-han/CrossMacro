namespace CrossMacro.Mcp.Tests;

internal sealed class ThrowingCliCommandHandler(string detail) : ICliCommandHandler
{
    private readonly string _detail = detail;

    public Task<OperationResult> ExecuteAsync(CommandOptions options, CancellationToken cancellationToken) =>
        Task.FromException<OperationResult>(new InvalidOperationException(_detail));
}
