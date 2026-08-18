namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>Sends ordered simulation batches and surfaces daemon failures.</summary>
internal sealed class IpcSimulationChannel(IpcTransport transport)
{
    private static readonly TimeSpan SimulationBatchAcknowledgementGracePeriod = TimeSpan.FromSeconds(2);

    private readonly IpcTransport _transport = transport;
    private readonly Lock _pendingBatchesLock = new();
    private readonly Dictionary<int, PendingSimulationBatch> _pendingBatches = [];
    private int _nextRequestId;

    private sealed record PendingSimulationBatch(TaskCompletionSource<bool> Completion, int EventCount);

    public void SimulateEvent(ushort type, ushort code, int value) =>
        SimulateEventBatch([new InputSimulationStep(type, code, value)]);

    public void SimulateEvents(ReadOnlySpan<(ushort Type, ushort Code, int Value)> events)
    {
        if (!_transport.IsConnected)
        {
            throw CreateConnectionException();
        }

        var steps = new InputSimulationStep[events.Length];
        for (var index = 0; index < events.Length; index++)
        {
            var (type, code, value) = events[index];
            steps[index] = new InputSimulationStep(type, code, value);
        }

        SimulateEventBatch(steps);
    }

    public void SimulateEventBatch(ReadOnlySpan<InputSimulationStep> steps)
    {
        if (steps.IsEmpty)
        {
            return;
        }

        ValidateBatch(steps.Length);
        if (!_transport.IsConnected)
        {
            throw CreateConnectionException();
        }

        var requestId = Interlocked.Increment(ref _nextRequestId);
        var pending = RegisterPendingBatch(requestId, steps.Length);
        try
        {
            SendBatch(requestId, steps);
            WaitForAcknowledgement(pending.Completion);
        }
        finally
        {
            RemovePendingBatch(requestId);
        }
    }

    public async Task SimulateEventBatchAsync(
        IReadOnlyList<InputSimulationStep> steps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count is 0)
        {
            return;
        }

        ValidateBatch(steps.Count);
        if (!_transport.IsConnected)
        {
            throw CreateConnectionException();
        }

        var requestId = Interlocked.Increment(ref _nextRequestId);
        var pending = RegisterPendingBatch(requestId, steps.Count);
        try
        {
            await SendBatchAsync(requestId, steps, cancellationToken).ConfigureAwait(false);
            await WaitForAcknowledgementAsync(pending.Completion, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RemovePendingBatch(requestId);
        }
    }

    public void HandleBatchCompletedMessage(int requestId, int eventCount)
    {
        PendingSimulationBatch? pending;
        lock (_pendingBatchesLock)
        {
            _ = _pendingBatches.TryGetValue(requestId, out pending);
        }

        if (pending is null)
        {
            Log.Warning("[IpcSimulationChannel] Received an acknowledgement for an unknown or expired simulation batch: RequestId={RequestId}", requestId);
            return;
        }

        if (eventCount != pending.EventCount)
        {
            var exception = new IpcClientException(
                IpcClientFailureReason.IntegrityMismatch,
                $"Daemon acknowledgement event-count mismatch for simulation batch {requestId.ToString(CultureInfo.InvariantCulture)}. ExpectedEvents={pending.EventCount.ToString(CultureInfo.InvariantCulture)}, ActualEvents={eventCount.ToString(CultureInfo.InvariantCulture)}.");
            if (pending.Completion.TrySetException(exception))
            {
                Log.LogError(
                    "[IpcSimulationChannel] Daemon acknowledgement event-count mismatch: RequestId={RequestId}, ExpectedEvents={ExpectedEvents}, ActualEvents={ActualEvents}",
                    requestId,
                    pending.EventCount,
                    eventCount);
            }

            return;
        }

        if (!pending.Completion.TrySetResult(true))
        {
            Log.Warning("[IpcSimulationChannel] Received a duplicate simulation batch acknowledgement: RequestId={RequestId}", requestId);
        }
    }

    public void HandleBatchFailedMessage(int requestId, string message)
    {
        PendingSimulationBatch? pending;
        lock (_pendingBatchesLock)
        {
            _ = _pendingBatches.TryGetValue(requestId, out pending);
        }

        var exception = new IpcClientException(
            IpcClientFailureReason.SimulationRejected,
            $"Simulation batch failed: {message}");
        if (pending?.Completion.TrySetException(exception) is true)
        {
            Log.LogError(
                "[IpcSimulationChannel] Daemon rejected simulation batch: RequestId={RequestId}, Message={Message}",
                requestId,
                message);
        }
        else
        {
            Log.Warning("[IpcSimulationChannel] Daemon rejected an unknown or expired simulation batch: RequestId={RequestId}, Message={Message}", requestId, message);
        }
    }

    public void FailAllPending(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        List<PendingSimulationBatch> pending;
        lock (_pendingBatchesLock)
        {
            pending = [.. _pendingBatches.Values];
            _pendingBatches.Clear();
        }

        foreach (var batch in pending)
        {
            _ = batch.Completion.TrySetException(exception);
        }
    }

    private PendingSimulationBatch RegisterPendingBatch(int requestId, int eventCount)
    {
        var pending = new PendingSimulationBatch(
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            eventCount);
        lock (_pendingBatchesLock)
        {
            _pendingBatches.Add(requestId, pending);
        }

        return pending;
    }

    private void SendBatch(int requestId, ReadOnlySpan<InputSimulationStep> steps)
    {
        var stepBuffer = steps.ToArray();
        var (sendFailure, sessionGeneration) = _transport.WriteFrames(writer =>
        {
            writer.Write((byte)IpcOpCode.SimulateEventBatch);
            writer.Write(requestId);
            writer.Write(stepBuffer.Length);
            foreach (var step in stepBuffer)
            {
                writer.Write(step.Type);
                writer.Write(step.Code);
                writer.Write(step.Value);
                writer.Write(step.DelayAfterMicroseconds);
            }

            writer.Flush();
        });

        if (sendFailure is not null)
        {
            RemovePendingBatch(requestId);
            _transport.HandleSendFailure(sendFailure, IpcOpCode.SimulateEventBatch, throwOnFailure: true, sessionGeneration: sessionGeneration);
        }
    }

    private async Task SendBatchAsync(
        int requestId,
        IReadOnlyList<InputSimulationStep> steps,
        CancellationToken cancellationToken)
    {
        var (sendFailure, sessionGeneration) = await _transport.WriteFramesAsync(writer =>
        {
            writer.Write((byte)IpcOpCode.SimulateEventBatch);
            writer.Write(requestId);
            writer.Write(steps.Count);
            foreach (var step in steps)
            {
                writer.Write(step.Type);
                writer.Write(step.Code);
                writer.Write(step.Value);
                writer.Write(step.DelayAfterMicroseconds);
            }

            writer.Flush();
        }, cancellationToken).ConfigureAwait(false);

        if (sendFailure is not null)
        {
            RemovePendingBatch(requestId);
            _transport.HandleSendFailure(
                sendFailure,
                IpcOpCode.SimulateEventBatch,
                throwOnFailure: true,
                sessionGeneration: sessionGeneration);
        }
    }

    private static void WaitForAcknowledgement(TaskCompletionSource<bool> completion)
    {
        var timeout = GetAcknowledgementTimeout();
        if (Task.WhenAny(completion.Task, Task.Delay(timeout, TimeProvider.System, CancellationToken.None)).GetAwaiter().GetResult() != completion.Task)
        {
            throw CreateTimeoutException(timeout);
        }

        _ = completion.Task.GetAwaiter().GetResult();
    }

    private async Task WaitForAcknowledgementAsync(
        TaskCompletionSource<bool> completion,
        CancellationToken cancellationToken)
    {
        var timeout = GetAcknowledgementTimeout();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _transport.SessionTokenOrNone);
        timeoutCts.CancelAfter(timeout);

        try
        {
            _ = await completion.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_transport.IsSessionCancellationRequested)
        {
            throw CreateTimeoutException(timeout);
        }
    }

    private void RemovePendingBatch(int requestId)
    {
        lock (_pendingBatchesLock)
        {
            _ = _pendingBatches.Remove(requestId);
        }
    }

    private static void ValidateBatch(int eventCount)
    {
        if (eventCount is <= 0 or > IpcProtocol.MaxSimulationBatchEvents)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventCount),
                $"Simulation batch contains {eventCount.ToString(CultureInfo.InvariantCulture)} events, exceeding the allowed range 1-{IpcProtocol.MaxSimulationBatchEvents.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static IpcClientException CreateConnectionException() =>
        new(
            IpcClientFailureReason.ConnectFailed,
            "Failed to send simulation batch because the daemon connection is not available.");

    private static TimeSpan GetAcknowledgementTimeout() =>
        TimeSpan.FromMicroseconds(IpcProtocol.MaxSimulationBatchTotalDelayMicroseconds) +
        SimulationBatchAcknowledgementGracePeriod;

    private static IpcClientException CreateTimeoutException(TimeSpan timeout) =>
        new(
            IpcClientFailureReason.Timeout,
            $"Timed out waiting for simulation batch acknowledgement after {timeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}ms.");
}
