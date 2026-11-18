namespace CrossMacro.Cli.Tests;

public sealed class McpCliRuntimeTests
{
    [Fact]
    public async Task RunAsync_WhenMcpDoesNotUseTheGuiRuntimeLock_ContinuesIntoMcpComposition()
    {
        using var console = new ConsoleCapture();
        var compositionStarted = false;

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
        Assert.Equal(string.Empty, console.StandardOutput);
        Assert.DoesNotContain("Another CrossMacro runtime instance is already running.", console.StandardError, StringComparison.Ordinal);
    }
}
