
namespace CrossMacro.Cli.Tests;

public sealed class CliCommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenResolverReturnsHandler_ExecutesHandlerAndWritesOutput()
    {
        using var console = new ConsoleCapture();
        var options = new SettingsGetOptions();
        var handler = Substitute.For<ICliCommandHandler>();
        _ = handler.ExecuteAsync(options, Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("resolved handler ran"));
        var resolver = Substitute.For<ICliCommandHandlerResolver>();
        _ = resolver.Resolve(options).Returns(handler);

        var exitCode = await new CliCommandExecutor(resolver).ExecuteAsync(options, CancellationToken.None);

        Assert.Equal((int)ExecutionOutcomeCode.Success, exitCode);
        Assert.Contains("Message: resolved handler ran", console.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResolverReturnsNull_ReturnsInvalidArgumentsAndWritesFallbackMessage()
    {
        using var console = new ConsoleCapture();
        var options = new UnknownOptions();
        var resolver = Substitute.For<ICliCommandHandlerResolver>();
        _ = resolver.Resolve(options).Returns((ICliCommandHandler?)null);

        var exitCode = await new CliCommandExecutor(resolver).ExecuteAsync(options, CancellationToken.None);

        Assert.Equal((int)ExecutionOutcomeCode.InvalidArguments, exitCode);
        Assert.Equal(string.Empty, console.StandardOutput);
        Assert.Contains("Code: 2", console.StandardError, StringComparison.Ordinal);
        Assert.Contains("No handler registered for command options type: UnknownOptions", console.StandardError, StringComparison.Ordinal);
    }

    private sealed record UnknownOptions() : CommandOptions(JsonOutput: false);
}
