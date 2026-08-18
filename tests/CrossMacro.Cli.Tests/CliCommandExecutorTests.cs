
namespace CrossMacro.Cli.Tests;

public sealed class CliCommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenResolverReturnsHandler_ExecutesHandlerAndWritesOutput()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            LoggerSetup.Initialize("Fatal");
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var options = new SettingsGetCliOptions();
            var handler = Substitute.For<ICliCommandHandler>();
            _ = handler.ExecuteAsync(options, Arg.Any<CancellationToken>())
                .Returns(CliCommandExecutionResult.Ok("resolved handler ran"));

            var resolver = Substitute.For<ICliCommandHandlerResolver>();
            _ = resolver.Resolve(options).Returns(handler);

            var executor = new CliCommandExecutor(resolver);

            var exitCode = await executor.ExecuteAsync(options, CancellationToken.None);

            Assert.Equal((int)CliExitCode.Success, exitCode);
            Assert.Contains("Status: ok", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("Message: resolved handler ran", stdout.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
            _ = await handler.Received(1).ExecuteAsync(options, Arg.Any<CancellationToken>());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenResolverReturnsNull_ReturnsInvalidArgumentsAndWritesFallbackMessage()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            LoggerSetup.Initialize("Fatal");
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var options = new UnknownCliOptions();
            var resolver = Substitute.For<ICliCommandHandlerResolver>();
            _ = resolver.Resolve(options).Returns((ICliCommandHandler?)null);

            var executor = new CliCommandExecutor(resolver);

            var exitCode = await executor.ExecuteAsync(options, CancellationToken.None);

            Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
            Assert.Equal(string.Empty, stdout.ToString());
            Assert.Contains("Status: error", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("Code: 2", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("No handler registered for command options type: UnknownCliOptions", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record UnknownCliOptions() : CliCommandOptions(JsonOutput: false);
}
