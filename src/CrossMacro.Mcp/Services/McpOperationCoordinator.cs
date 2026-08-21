namespace CrossMacro.Mcp.Services;

/// <summary>
/// Keeps automation lifetime separate from individual MCP requests. Only one
/// workload can run at once, and completed snapshots retain no raw CLI data.
/// </summary>
public sealed class McpOperationCoordinator : IMcpOperationCoordinator
{
    public const int MaximumRetainedCompletedOperations = 32;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, OperationEntry> _completed = new(StringComparer.Ordinal);
    private readonly Queue<string> _completedOrder = new();
    private OperationEntry? _active;
    private bool _disposed;

    public McpAutomationOperationStartResult Start(
        McpAutomationOperationKind kind,
        Func<CancellationToken, Task<CliCommandExecutionResult>> executeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active is not null)
            {
                return new McpAutomationOperationStartResult(
                    Operation: null,
                    Error: McpToolOutcomeMapper.RuntimeError("Another automation operation is already active."));
            }

            var entry = new OperationEntry(
                operationId: Guid.NewGuid().ToString("N"),
                kind,
                DateTimeOffset.UtcNow,
                executeAsync,
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
            _active = entry;
            _ = Task.Run(() => RunAsync(entry), CancellationToken.None);
            return new McpAutomationOperationStartResult(CreateSnapshot(entry), Error: null);
        }
    }

    public McpAutomationOperation? GetOperation(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return null;
            }

            if (_active is { } active && string.Equals(active.OperationId, operationId, StringComparison.Ordinal))
            {
                return CreateSnapshot(active);
            }

            return _completed.TryGetValue(operationId, out var completed)
                ? CreateSnapshot(completed)
                : null;
        }
    }

    public McpAutomationOperation? GetActive()
    {
        lock (_gate)
        {
            return _disposed || _active is null ? null : CreateSnapshot(_active);
        }
    }

    public McpAutomationOperationStopResult StopOperation(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return new McpAutomationOperationStopResult(Operation: null, CancellationInitiated: false);
        }

        CancellationTokenSource? cancellation = null;
        McpAutomationOperation? operation;
        var cancellationInitiated = false;
        lock (_gate)
        {
            if (_disposed)
            {
                return new McpAutomationOperationStopResult(Operation: null, CancellationInitiated: false);
            }

            if (_active is { } active && string.Equals(active.OperationId, operationId, StringComparison.Ordinal))
            {
                if (!active.CancellationRequested)
                {
                    active.CancellationRequested = true;
                    cancellation = active.Cancellation;
                    cancellationInitiated = true;
                }

                operation = CreateSnapshot(active);
            }
            else
            {
                operation = _completed.TryGetValue(operationId, out var completed)
                    ? CreateSnapshot(completed)
                    : null;
            }
        }

        cancellation?.Cancel();

        return new McpAutomationOperationStopResult(operation, cancellationInitiated);
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation = null;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var completed in _completed.Values)
            {
                completed.Cancellation.Dispose();
            }

            _completed.Clear();
            _completedOrder.Clear();
            if (_active is { } active)
            {
                active.DiscardOnCompletion = true;
                active.CancellationRequested = true;
                cancellation = active.Cancellation;
                _active = null;
            }
        }

        cancellation?.Cancel();
    }

    private async Task RunAsync(OperationEntry entry)
    {
        McpToolOutcome outcome;
        try
        {
            var result = await entry.ExecuteAsync(entry.Cancellation.Token).ConfigureAwait(false);
            outcome = IsCancellationRequested(entry)
                ? McpToolOutcomeMapper.Cancelled("Automation operation cancelled.")
                : RedactOutcome(result);
        }
        catch (OperationCanceledException)
        {
            outcome = McpToolOutcomeMapper.Cancelled("Automation operation cancelled.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException && !IsCancellationRequested(entry))
        {
            outcome = McpToolOutcomeMapper.RuntimeError("Automation operation failed.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            outcome = McpToolOutcomeMapper.Cancelled("Automation operation cancelled.");
        }

        Complete(entry, outcome);
    }

    private void Complete(OperationEntry entry, McpToolOutcome outcome)
    {
        lock (_gate)
        {
            if (_disposed || entry.DiscardOnCompletion || !ReferenceEquals(_active, entry))
            {
                entry.Cancellation.Dispose();
                return;
            }

            entry.Outcome = outcome;
            entry.CompletedAt = DateTimeOffset.UtcNow;
            entry.State = ResolveCompletedState(outcome);
            _active = null;
            _completed.Add(entry.OperationId, entry);
            _completedOrder.Enqueue(entry.OperationId);
            while (_completedOrder.Count > MaximumRetainedCompletedOperations)
            {
                var expiredOperationId = _completedOrder.Dequeue();
                if (_completed.Remove(expiredOperationId, out var expired))
                {
                    expired.Cancellation.Dispose();
                }
            }
        }
    }

    private bool IsCancellationRequested(OperationEntry entry)
    {
        lock (_gate)
        {
            return entry.CancellationRequested;
        }
    }

    private static McpToolOutcome RedactOutcome(CliCommandExecutionResult result)
    {
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        return new McpToolOutcome(
            outcome.Success,
            outcome.ExitCode,
            outcome.Message,
            Warnings: [],
            outcome.Errors);
    }

    private static McpAutomationOperationState ResolveCompletedState(McpToolOutcome outcome)
    {
        if (outcome.Success)
        {
            return McpAutomationOperationState.Succeeded;
        }

        return outcome.ExitCode is (int)CliExitCode.Cancelled
            ? McpAutomationOperationState.Cancelled
            : McpAutomationOperationState.Failed;
    }

    private static McpAutomationOperation CreateSnapshot(OperationEntry entry) => new(
        entry.OperationId,
        entry.Kind,
        entry.State,
        entry.StartedAt,
        entry.CompletedAt,
        entry.CancellationRequested,
        entry.Outcome);

    private sealed class OperationEntry(
        string operationId,
        McpAutomationOperationKind kind,
        DateTimeOffset startedAt,
        Func<CancellationToken, Task<CliCommandExecutionResult>> executeAsync,
        CancellationTokenSource cancellation)
    {
        public string OperationId { get; } = operationId;

        public McpAutomationOperationKind Kind { get; } = kind;

        public DateTimeOffset StartedAt { get; } = startedAt;

        public Func<CancellationToken, Task<CliCommandExecutionResult>> ExecuteAsync { get; } = executeAsync;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        public McpAutomationOperationState State { get; set; } = McpAutomationOperationState.Running;

        public DateTimeOffset? CompletedAt { get; set; }

        public bool CancellationRequested { get; set; }

        public bool DiscardOnCompletion { get; set; }

        public McpToolOutcome? Outcome { get; set; }
    }
}
