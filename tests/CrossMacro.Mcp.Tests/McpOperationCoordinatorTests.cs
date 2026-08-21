namespace CrossMacro.Mcp.Tests;

public sealed class McpOperationCoordinatorTests
{
    [Fact]
    public async Task StartAsync_CompletesWithARedactedOutcome()
    {
        using var coordinator = new McpOperationCoordinator();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource<CliCommandExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        var start = coordinator.Start(
            McpAutomationOperationKind.Play,
            async cancellationToken =>
            {
                started.SetResult();
                return await complete.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            },
            CancellationToken.None);

        var running = Assert.IsType<McpAutomationOperation>(start.Operation);
        Assert.Equal(McpAutomationOperationState.Running, running.State);
        await started.Task;
        complete.SetResult(CliCommandExecutionResult.Fail(
            CliExitCode.RuntimeError,
            "Playback failed.",
            ["native backend detail should not leak"],
            ["sensitive warning should not leak"],
            data: new { Sensitive = "value" }));

        var completed = await WaitForCompletionAsync(coordinator, running.OperationId);

        Assert.Equal(McpAutomationOperationState.Failed, completed.State);
        var outcome = Assert.IsType<McpToolOutcome>(completed.Outcome);
        Assert.Equal("Playback failed.", outcome.Message);
        Assert.Empty(outcome.Warnings);
        Assert.Equal("Playback failed.", Assert.Single(outcome.Errors).Message);
    }

    [Fact]
    public async Task StartAsync_RejectsConcurrentWorkUntilTheActiveOperationCompletes()
    {
        using var coordinator = new McpOperationCoordinator();
        var completeFirst = new TaskCompletionSource<CliCommandExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = coordinator.Start(
            McpAutomationOperationKind.Run,
            cancellationToken => completeFirst.Task.WaitAsync(cancellationToken),
            CancellationToken.None);

        var blocked = coordinator.Start(
            McpAutomationOperationKind.Record,
            _ => Task.FromResult(CliCommandExecutionResult.Ok("Recording completed.")),
            CancellationToken.None);

        Assert.True(first.Started);
        Assert.False(blocked.Started);
        Assert.Equal("runtime_error", Assert.Single(blocked.Error!.Errors).Code);
        completeFirst.SetResult(CliCommandExecutionResult.Ok("Run script execution complete."));
        var firstOperation = Assert.IsType<McpAutomationOperation>(first.Operation);
        _ = await WaitForCompletionAsync(coordinator, firstOperation.OperationId);

        var next = coordinator.Start(
            McpAutomationOperationKind.Record,
            _ => Task.FromResult(CliCommandExecutionResult.Ok("Recording completed.")),
            CancellationToken.None);

        Assert.True(next.Started);
        var nextOperation = Assert.IsType<McpAutomationOperation>(next.Operation);
        Assert.Equal(McpAutomationOperationState.Succeeded, (await WaitForCompletionAsync(coordinator, nextOperation.OperationId)).State);
    }

    [Fact]
    public async Task StopAsync_CancelsTheActiveOperationAndIsIdempotent()
    {
        using var coordinator = new McpOperationCoordinator();
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = coordinator.Start(
            McpAutomationOperationKind.Record,
            async cancellationToken =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                    return CliCommandExecutionResult.Ok("Recording completed.");
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.SetResult();
                    throw;
                }
            },
            CancellationToken.None);
        var activeOperation = Assert.IsType<McpAutomationOperation>(start.Operation);
        var operationId = activeOperation.OperationId;

        var firstStop = coordinator.StopOperation(operationId);
        var repeatedStop = coordinator.StopOperation(operationId);

        Assert.True(firstStop.Found);
        Assert.True(firstStop.CancellationInitiated);
        Assert.True(Assert.IsType<McpAutomationOperation>(firstStop.Operation).CancellationRequested);
        Assert.True(repeatedStop.Found);
        Assert.False(repeatedStop.CancellationInitiated);
        await cancellationObserved.Task;
        var completed = await WaitForCompletionAsync(coordinator, operationId);
        Assert.Equal(McpAutomationOperationState.Cancelled, completed.State);
        Assert.True(completed.CancellationRequested);
        Assert.Equal("cancelled", Assert.Single(Assert.IsType<McpToolOutcome>(completed.Outcome).Errors).Code);
    }

    [Fact]
    public async Task StopAsync_PreventsACancelledOperationFromPublishingAStaleSuccess()
    {
        using var coordinator = new McpOperationCoordinator();
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = coordinator.Start(
            McpAutomationOperationKind.Run,
            async cancellationToken =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.SetResult();
                }

                return CliCommandExecutionResult.Ok("Run script execution complete.");
            },
            CancellationToken.None);
        var operationId = Assert.IsType<McpAutomationOperation>(start.Operation).OperationId;

        _ = coordinator.StopOperation(operationId);

        await cancellationObserved.Task;
        var completed = await WaitForCompletionAsync(coordinator, operationId);
        Assert.Equal(McpAutomationOperationState.Cancelled, completed.State);
        Assert.Equal("cancelled", Assert.Single(Assert.IsType<McpToolOutcome>(completed.Outcome).Errors).Code);
    }

    [Fact]
    public async Task StartAsync_WhenTheWorkThrows_RetainsARedactedFailure()
    {
        using var coordinator = new McpOperationCoordinator();
        var start = coordinator.Start(
            McpAutomationOperationKind.Play,
            _ => Task.FromException<CliCommandExecutionResult>(new InvalidOperationException("backend detail should not leak")),
            CancellationToken.None);
        var operationId = Assert.IsType<McpAutomationOperation>(start.Operation).OperationId;

        var completed = await WaitForCompletionAsync(coordinator, operationId);

        var outcome = Assert.IsType<McpToolOutcome>(completed.Outcome);
        Assert.Equal(McpAutomationOperationState.Failed, completed.State);
        Assert.Equal("Automation operation failed.", outcome.Message);
        Assert.DoesNotContain("backend detail should not leak", outcome.Message, StringComparison.Ordinal);
        Assert.Equal("runtime_error", Assert.Single(outcome.Errors).Code);
    }

    [Fact]
    public async Task Dispose_CancelsTheActiveOperationAndClearsSnapshots()
    {
        var coordinator = new McpOperationCoordinator();
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = coordinator.Start(
            McpAutomationOperationKind.Play,
            async cancellationToken =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                    return CliCommandExecutionResult.Ok("Playback complete.");
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.SetResult();
                    throw;
                }
            },
            CancellationToken.None);

        coordinator.Dispose();

        await cancellationObserved.Task;
        var activeOperation = Assert.IsType<McpAutomationOperation>(start.Operation);
        Assert.Null(coordinator.GetOperation(activeOperation.OperationId));
        Assert.Null(coordinator.GetActive());
    }

    [Fact]
    public void StartAsync_WhenRequestIsAlreadyCancelled_DoesNotReserveTheOperationSlot()
    {
        using var coordinator = new McpOperationCoordinator();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() => coordinator.Start(
            McpAutomationOperationKind.Play,
            _ => Task.FromResult(CliCommandExecutionResult.Ok("Playback complete.")),
            cancellation.Token));

        Assert.Null(coordinator.GetActive());
    }

    [Fact]
    public async Task StartAsync_EvictsTheOldestCompletedOperationAfterTheRetentionLimit()
    {
        using var coordinator = new McpOperationCoordinator();
        string? firstOperationId = null;
        string? lastOperationId = null;

        for (var index = 0; index <= McpOperationCoordinator.MaximumRetainedCompletedOperations; index++)
        {
            var start = coordinator.Start(
                McpAutomationOperationKind.Run,
                _ => Task.FromResult(CliCommandExecutionResult.Ok("Run script execution complete.")),
                CancellationToken.None);
            var operation = Assert.IsType<McpAutomationOperation>(start.Operation);
            firstOperationId ??= operation.OperationId;
            lastOperationId = operation.OperationId;
            _ = await WaitForCompletionAsync(coordinator, operation.OperationId);
        }

        Assert.NotNull(firstOperationId);
        Assert.NotNull(lastOperationId);
        Assert.Null(coordinator.GetOperation(firstOperationId));
        Assert.NotNull(coordinator.GetOperation(lastOperationId));
    }

    private static async Task<McpAutomationOperation> WaitForCompletionAsync(
        IMcpOperationCoordinator coordinator,
        string operationId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var operation = coordinator.GetOperation(operationId);
            if (operation is { State: not McpAutomationOperationState.Running })
            {
                return operation;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
        }

        throw new TimeoutException("The operation did not complete in the expected time.");
    }
}
