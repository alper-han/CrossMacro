namespace CrossMacro.Cli.Tests;

public sealed class McpCliRuntimeTests
{
    [Fact]
    public async Task RunAsync_WhenMcpDoesNotUseTheGuiRuntimeLock_ContinuesIntoMcpComposition()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var compositionStarted = false;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await CliGuiRuntime.RunAsync(
                ["mcp"],
                static _ => { },
                (_, _) =>
                {
                    compositionStarted = true;
                    throw new InvalidOperationException("Stop after proving MCP composition started.");
                },
                startGui: static () => throw new InvalidOperationException("GUI must not start for MCP."),
                getVersionString: static () => "test",
                tryAcquireSingleInstanceGuard: static () => null,
                bootstrapCallbacks: CliBootstrapCallbacks.NoOp);

            Assert.Equal((int)CliExitCode.RuntimeError, exitCode);
            Assert.True(compositionStarted);
            Assert.Equal(string.Empty, stdout.ToString());
            Assert.DoesNotContain("Another CrossMacro runtime instance is already running.", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
