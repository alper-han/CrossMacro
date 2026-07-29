namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Owns input-simulation traffic: single events, raw multi-event writes and acknowledged
/// simulation batches with their pending-request registry.
/// </summary>
internal sealed class IpcSimulationChannel(IpcTransport transport)
{
    private static readonly TimeSpan SimulationBatchAckGracePeriod = TimeSpan.FromSeconds(2);

    private readonly IpcTransport _transport = transport;
    private readonly Lock _simulationBatchLock = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _pendingSimulationBatches = [];
    private int _nextSimulationBatchRequestId;

    public void SimulateEvent(ushort type, ushort code, int value)
    {
        Log.Debug("[IpcClient] TX: SimulateEvent Type={Type} Code={Code} Value={Value}", type, code, value);
        _ = _transport.Send(IpcOpCode.SimulateEvent, w =>
        {
            w.Write(type);
            w.Write(code);
            w.Write(value);
        }, throwOnFailure: true);
    }

    public void SimulateEvents(ReadOnlySpan<(ushort Type, ushort Code, int Value)> events)
    {
        if (!_transport.IsConnected)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                "Failed to send simulated events because the daemon connection is not available.");
        }

        var eventBuffer = events.ToArray();
        var (sendFailure, sessionGeneration) = _transport.WriteFrames(writer =>
        {
            foreach (var (type, code, value) in eventBuffer)
            {
                writer.Write((byte)IpcOpCode.SimulateEvent);
                writer.Write(type);
                writer.Write(code);
                writer.Write(value);
            }
        });

        if (sendFailure is not null)
        {
            _transport.HandleSendFailure(sendFailure, IpcOpCode.SimulateEvent, throwOnFailure: true, sessionGeneration: sessionGeneration);
        }
    }

    public void SimulateEventBatch(ReadOnlySpan<InputSimulationStep> steps)
    {
        if (steps.IsEmpty)
        {
            return;
        }

        if (steps.Length > IpcProtocol.MaxSimulationBatchEvents)
        {
            throw new ArgumentOutOfRangeException(
                nameof(steps),
                $"Simulation batch contains {steps.Length.ToString(CultureInfo.InvariantCulture)} events, exceeding the maximum of {IpcProtocol.MaxSimulationBatchEvents.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (!_transport.IsConnected)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                "Failed to send simulation batch because the daemon connection is not available.");
        }

        var requestId = Interlocked.Increment(ref _nextSimulationBatchRequestId);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_simulationBatchLock)
        {
            _pendingSimulationBatches[requestId] = completion;
        }

        try
        {
            SendSimulationBatchPayload(requestId, steps);
            WaitForSimulationBatchAcknowledgement(completion);
        }
        finally
        {
            RemovePendingSimulationBatch(requestId);
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

        if (steps.Count > IpcProtocol.MaxSimulationBatchEvents)
        {
            throw new ArgumentOutOfRangeException(nameof(steps));
        }

        if (!_transport.IsConnected)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                "Failed to send simulation batch because the daemon connection is not available.");
        }

        var requestId = Interlocked.Increment(ref _nextSimulationBatchRequestId);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_simulationBatchLock)
        {
            _pendingSimulationBatches[requestId] = completion;
        }

        try
        {
            await SendSimulationBatchPayloadAsync(requestId, steps, cancellationToken).ConfigureAwait(false);
            await WaitForSimulationBatchAcknowledgementAsync(completion, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RemovePendingSimulationBatch(requestId);
        }
    }

    private void SendSimulationBatchPayload(int requestId, ReadOnlySpan<InputSimulationStep> steps)
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
                writer.Write(step.DelayAfterMs);
            }
        });

        if (sendFailure is not null)
        {
            RemovePendingSimulationBatch(requestId);
            _transport.HandleSendFailure(sendFailure, IpcOpCode.SimulateEventBatch, throwOnFailure: true, sessionGeneration: sessionGeneration);
        }
    }

    private async Task SendSimulationBatchPayloadAsync(
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
                writer.Write(step.DelayAfterMs);
            }

            writer.Flush();
        }, cancellationToken).ConfigureAwait(false);

        if (sendFailure is not null)
        {
            RemovePendingSimulationBatch(requestId);
            _transport.HandleSendFailure(
                sendFailure,
                IpcOpCode.SimulateEventBatch,
                throwOnFailure: true,
                sessionGeneration: sessionGeneration);
        }
    }

    private static void WaitForSimulationBatchAcknowledgement(TaskCompletionSource<bool> completion)
    {
        var acknowledgementTimeout = TimeSpan.FromMilliseconds(IpcProtocol.MaxSimulationBatchTotalDelayMs) +
            SimulationBatchAckGracePeriod;
        var timeoutTask = Task.Delay(acknowledgementTimeout, TimeProvider.System, CancellationToken.None);
        if (Task.WhenAny(completion.Task, timeoutTask).GetAwaiter().GetResult() != completion.Task)
        {
            throw new IpcClientException(
                IpcClientFailureReason.Timeout,
                $"Timed out waiting for simulation batch acknowledgement after {acknowledgementTimeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}ms.");
        }

        _ = completion.Task.GetAwaiter().GetResult();
    }

    private async Task WaitForSimulationBatchAcknowledgementAsync(
        TaskCompletionSource<bool> completion,
        CancellationToken cancellationToken)
    {
        var acknowledgementTimeout = TimeSpan.FromMilliseconds(IpcProtocol.MaxSimulationBatchTotalDelayMs) +
            SimulationBatchAckGracePeriod;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _transport.SessionTokenOrNone);
        timeoutCts.CancelAfter(acknowledgementTimeout);

        try
        {
            _ = await completion.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_transport.IsSessionCancellationRequested)
        {
            throw new IpcClientException(
                IpcClientFailureReason.Timeout,
                $"Timed out waiting for simulation batch acknowledgement after {acknowledgementTimeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}ms.");
        }
    }

    public void HandleBatchCompletedMessage(int requestId)
    {
        TaskCompletionSource<bool>? completion;
        lock (_simulationBatchLock)
        {
            _ = _pendingSimulationBatches.TryGetValue(requestId, out completion);
        }

        _ = (completion?.TrySetResult(true));
    }

    public void HandleBatchFailedMessage(int requestId, string message)
    {
        TaskCompletionSource<bool>? completion;
        lock (_simulationBatchLock)
        {
            _ = _pendingSimulationBatches.TryGetValue(requestId, out completion);
        }

        _ = completion?.TrySetException(new IpcClientException(
            IpcClientFailureReason.ConnectFailed,
            $"Simulation batch failed: {message}"));
    }

    private void RemovePendingSimulationBatch(int requestId)
    {
        lock (_simulationBatchLock)
        {
            _ = _pendingSimulationBatches.Remove(requestId);
        }
    }

    public void FailAllPending(Exception exception)
    {
        List<TaskCompletionSource<bool>> pending;
        lock (_simulationBatchLock)
        {
            pending = [.. _pendingSimulationBatches.Values];
            _pendingSimulationBatches.Clear();
        }

        foreach (var completion in pending)
        {
            _ = completion.TrySetException(exception);
        }
    }
}
