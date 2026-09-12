namespace CrossMacro.Mcp.Tests;

internal sealed class ThrowingCliCommandHandler(string detail) : ICliCommandHandler
{
    private readonly string _detail = detail;

    public bool CanHandle(CliCommandOptions options) => options is DoctorCliOptions;

    public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken) =>
        Task.FromException<CliCommandExecutionResult>(new InvalidOperationException(_detail));
}
