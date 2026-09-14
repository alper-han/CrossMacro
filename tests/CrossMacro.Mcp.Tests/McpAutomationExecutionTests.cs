using Microsoft.Extensions.Time.Testing;

namespace CrossMacro.Mcp.Tests;

public sealed class McpAutomationExecutionTests
{
    [Fact]
    public async Task Deadline_UsesInjectedClockAndMapsTimeout()
    {
        var clock = new FakeTimeProvider();
        var macros = new TestMacroExecutionService
        {
            ExecutionHandler = async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                throw new InvalidOperationException("Expected cancellation");
            },
        };
        using var operations = new McpOperationCoordinator();
        var execution = new McpAutomationExecution(macros, new TestRunScriptExecutionService(), new TestRecordExecutionService(), operations, clock);
        var pending = execution.PlayAsync(new MacroExecutionRequest { MacroFilePath = "macro" }, 30, CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(pending.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));
        var result = await pending;

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.RuntimeError, result.ExitCode);
        Assert.Equal("Automation operation timed out.", result.Message);
    }

    [Fact]
    public async Task CallerCancellation_RemainsCancellationInsteadOfTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        var macros = new TestMacroExecutionService
        {
            ExecutionHandler = async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                throw new InvalidOperationException("Expected cancellation");
            },
        };
        using var operations = new McpOperationCoordinator();
        var execution = new McpAutomationExecution(macros, new TestRunScriptExecutionService(), new TestRecordExecutionService(), operations, new FakeTimeProvider());
        var pending = execution.PlayAsync(new MacroExecutionRequest { MacroFilePath = "macro" }, 30, cancellation.Token);

        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public void CompatibilityPolicy_KeepsCliUnlimitedFiniteAndTypedZeroInvalid()
    {
        Assert.Equal(McpAutomationPolicy.DefaultTimeoutSeconds, McpAutomationPolicy.FromCliTimeout(0));
        Assert.False(McpAutomationPolicy.TryGetSeconds(0, "timeoutSeconds", McpAutomationPolicy.DefaultTimeoutSeconds, allowZero: false, out _, out _));
        Assert.True(McpAutomationPolicy.TryGetSeconds(value: null, "timeoutSeconds", McpAutomationPolicy.DefaultTimeoutSeconds, allowZero: false, out var timeout, out _));
        Assert.Equal(McpAutomationPolicy.DefaultTimeoutSeconds, timeout);
    }
}
