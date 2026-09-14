namespace CrossMacro.Daemon.Services.Sessions;

/// <summary>
/// Decodes and executes session commands without owning the handshake, read loop,
/// or teardown lifecycle. Wire opcodes and response ordering intentionally remain
/// identical to the original session implementation.
/// </summary>
internal sealed class SessionCommandDispatcher
{
    private const int SlowSimulationBatchThresholdMilliseconds = 50;
    private readonly DaemonProtocolSession _session;
    private readonly ISecurityService _security;
    private readonly IVirtualDeviceManager _virtualDevice;
    private readonly IInputCaptureManager _inputCapture;

    internal SessionCommandDispatcher(
        DaemonProtocolSession session,
        ISecurityService security,
        IVirtualDeviceManager virtualDevice,
        IInputCaptureManager inputCapture)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _virtualDevice = virtualDevice ?? throw new ArgumentNullException(nameof(virtualDevice));
        _inputCapture = inputCapture ?? throw new ArgumentNullException(nameof(inputCapture));
    }

    internal async Task DispatchAsync(IpcOpCode opcode, uint uid, int pid, CancellationToken token)
    {
        switch (opcode)
        {
            case IpcOpCode.StartCapture:
                await HandleStartCaptureCommandAsync(uid, pid, token).ConfigureAwait(false);
                break;
            case IpcOpCode.StopCapture:
                await HandleStopCaptureCommandAsync(uid, pid, token).ConfigureAwait(false);
                break;
            case IpcOpCode.ConfigureResolution:
                await HandleConfigureResolutionCommandAsync(token).ConfigureAwait(false);
                break;
            case IpcOpCode.SimulateEvent:
                await HandleSimulateEventCommandAsync(uid, pid, token).ConfigureAwait(false);
                break;
            case IpcOpCode.SimulateEventBatch:
                await HandleSimulateEventBatchCommandAsync(uid, pid, token).ConfigureAwait(false);
                break;
            default:
                throw new InvalidDataException($"Unknown OpCode: {opcode}");
        }
    }

    private async Task HandleStartCaptureCommandAsync(uint uid, int pid, CancellationToken token)
    {
        var (requestId, captureMouse, captureKb) = IpcMessageCodec.ReadCaptureStartPayload(_session.Reader);
        _security.LogCaptureStart(uid, pid, captureMouse, captureKb);

        var requestGeneration = _session.CaptureForwarding.BeginPendingGeneration();

        CaptureStartResult result;
        try
        {
            result = _inputCapture.StartCapture(
                captureMouse,
                captureKb,
                _session.CaptureForwarding.CreateEventForwarder(requestGeneration, _session));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[SessionHandler] Capture manager threw during StartCapture");
            result = CaptureStartResult.Failed(
                "Failed to start capture due to internal error: " + ex.Message);
        }

        await _session.CaptureForwarding.DrainAsync(token).ConfigureAwait(false);

        using (await _session.WriterGate.EnterAsync(token).ConfigureAwait(false))
        {
            if (result.Success)
            {
                var activation = _session.CaptureForwarding.ActivateGeneration(requestGeneration);

                IpcMessageCodec.WriteCaptureStarted(_session.Writer, requestId);

                if (activation.DroppedPendingCaptureEvents > 0)
                {
                    Log.Warning(
                        "[SessionHandler] Dropped {DroppedCount} pending capture event(s) while waiting for startup acknowledgement (Generation={Generation})",
                        activation.DroppedPendingCaptureEvents,
                        requestGeneration);
                }

                if (activation.HasBufferedEvents)
                {
                    while (activation.BufferedEvents is { Count: > 0 } bufferedEvents)
                    {
                        var bufferedEvent = bufferedEvents.Dequeue();
                        _session.WriteInputEvent(bufferedEvent);
                    }
                }
            }
            else
            {
                IpcMessageCodec.WriteRequestFailure(_session.Writer, IpcOpCode.CaptureStartFailed,
                    requestId, result.ErrorMessage ?? "Failed to start capture.");
                _session.CaptureForwarding.ResetAfterFailedStart(requestGeneration);
            }
        }

        await _session.Stream.FlushAsync(token).ConfigureAwait(false);
    }

    private async Task HandleStopCaptureCommandAsync(uint uid, int pid, CancellationToken token)
    {
        _security.LogCaptureStop(uid, pid);
        using (await _session.WriterGate.EnterAsync(token).ConfigureAwait(false))
        {
            _session.CaptureForwarding.Stop();
        }

        _inputCapture.StopCapture();
    }

    private async Task HandleConfigureResolutionCommandAsync(CancellationToken token)
    {
        var (width, height) = IpcMessageCodec.ReadResolutionPayload(_session.Reader);
        await _virtualDevice.ConfigureAsync(width, height, token).ConfigureAwait(false);
    }

    private async Task HandleSimulateEventCommandAsync(uint uid, int pid, CancellationToken token)
    {
        var (type, code, value) = IpcMessageCodec.ReadSingleSimulationPayload(_session.Reader);
        await _virtualDevice.SendEventAsync(type, code, value, token).ConfigureAwait(false);
        _security.LogSimulation(uid, pid, type, code, value);
    }

    private async Task HandleSimulateEventBatchCommandAsync(uint uid, int pid, CancellationToken token)
    {
        var requestId = IpcMessageCodec.ReadRequestId(_session.Reader);
        var batch = IpcMessageCodec.ReadSimulationBatch(_session.Reader);
        if (!batch.Success)
        {
            var errorMessage = batch.ErrorMessage ?? "Failed to decode simulation batch.";
            await WriteSimulationBatchFailureAsync(requestId, errorMessage, token).ConfigureAwait(false);
            if (!batch.HasCompleteFrame)
            {
                throw new InvalidDataException(errorMessage);
            }

            return;
        }

        var events = batch.Events;
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await _virtualDevice.SendEventsAsync(events, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[SessionHandler] Simulation batch failed");
            await WriteSimulationBatchFailureAsync(requestId, ex.Message, token).ConfigureAwait(false);
            return;
        }

        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        using (await _session.WriterGate.EnterAsync(token).ConfigureAwait(false))
        {
            IpcMessageCodec.WriteSimulationBatchCompleted(_session.Writer, requestId, events.Count);
        }

        await _session.Stream.FlushAsync(token).ConfigureAwait(false);
        foreach (var inputEvent in events)
        {
            _security.LogSimulation(uid, pid, inputEvent.Type, inputEvent.Code, inputEvent.Value);
        }

        if (elapsedMilliseconds > SlowSimulationBatchThresholdMilliseconds)
        {
            Log.Warning(
                "[SessionHandler] Simulation batch acknowledgement was slow: RequestId={RequestId}, Events={EventCount}, ElapsedMs={ElapsedMs:F2}",
                requestId,
                events.Count,
                elapsedMilliseconds);
        }
    }

    private async Task WriteSimulationBatchFailureAsync(int requestId, string errorMessage, CancellationToken token)
    {
        using (await _session.WriterGate.EnterAsync(token).ConfigureAwait(false))
        {
            IpcMessageCodec.WriteRequestFailure(_session.Writer, IpcOpCode.SimulationBatchFailed, requestId, errorMessage);
        }

        await _session.Stream.FlushAsync(token).ConfigureAwait(false);
    }
}
