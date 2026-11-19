namespace CrossMacro.Cli.Tests;

public sealed class McpCliRuntimeTests
{
    private static readonly string[] McpArgs = ["mcp"];

    [Fact]
    public async Task RunAsync_WhenMcpDoesNotUseTheGuiRuntimeLock_ContinuesIntoMcpComposition()
    {
        using var console = new ConsoleCapture();
        var compositionStarted = false;

        var exitCode = await CliGuiRuntime.RunAsync(
            McpArgs,
            (_, _) =>
            {
                compositionStarted = true;
                throw new InvalidOperationException("Stop after proving MCP composition started.");
            },
            startGui: static () => throw new InvalidOperationException("GUI must not start for MCP."),
            getVersionString: static () => "test",
            tryAcquireSingleInstanceGuard: static () => null,
            bootstrapCallbacks: CliBootstrapCallbacks.NoOp);

        Assert.Equal((int)ExecutionOutcomeCode.RuntimeError, exitCode);
        Assert.True(compositionStarted);
        Assert.Equal(string.Empty, console.StandardOutput);
        Assert.DoesNotContain("Another CrossMacro runtime instance is already running.", console.StandardError, StringComparison.Ordinal);
    }
}
