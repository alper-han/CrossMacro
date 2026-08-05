namespace CrossMacro.Daemon.Services;

/// <summary>
/// Decodes and executes session commands without owning the handshake, read loop,
/// or teardown lifecycle. Wire opcodes and response ordering intentionally remain
/// identical to the original session implementation.
/// </summary>
internal sealed class SessionCommandDispatcher
{
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
        var requestId = _session.Reader.ReadInt32();
        var captureMouse = _session.Reader.ReadBoolean();
        var captureKb = _session.Reader.ReadBoolean();
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

                _session.Writer.Write((byte)IpcOpCode.CaptureStarted);
                _session.Writer.Write(requestId);

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
                _session.Writer.Write((byte)IpcOpCode.CaptureStartFailed);
                _session.Writer.Write(requestId);
                _session.Writer.Write(result.ErrorMessage ?? "Failed to start capture.");
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
        var width = _session.Reader.ReadInt32();
        var height = _session.Reader.ReadInt32();
        await _virtualDevice.ConfigureAsync(width, height, token).ConfigureAwait(false);
    }

    private async Task HandleSimulateEventCommandAsync(uint uid, int pid, CancellationToken token)
    {
        var type = _session.Reader.ReadUInt16();
        var code = _session.Reader.ReadUInt16();
        var value = _session.Reader.ReadInt32();
        await _virtualDevice.SendEventAsync(type, code, value, token).ConfigureAwait(false);
        _security.LogSimulation(uid, pid, type, code, value);
    }

    private async Task HandleSimulateEventBatchCommandAsync(uint uid, int pid, CancellationToken token)
    {
        var requestId = _session.Reader.ReadInt32();
        try
        {
            var events = ReadSimulationBatchEvents();
            await _virtualDevice.SendEventsAsync(events, token).ConfigureAwait(false);

            using (await _session.WriterGate.EnterAsync(token).ConfigureAwait(false))
            {
                _session.Writer.Write((byte)IpcOpCode.SimulationBatchCompleted);
                _session.Writer.Write(requestId);
            }

            await _session.Stream.FlushAsync(token).ConfigureAwait(false);

            foreach (var inputEvent in events)
            {
                _security.LogSimulation(uid, pid, inputEvent.Type, inputEvent.Code, inputEvent.Value);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[SessionHandler] Simulation batch failed");
            using (await _session.WriterGate.EnterAsync(token).ConfigureAwait(false))
            {
                _session.Writer.Write((byte)IpcOpCode.SimulationBatchFailed);
                _session.Writer.Write(requestId);
                _session.Writer.Write(ex.Message);
            }

            await _session.Stream.FlushAsync(token).ConfigureAwait(false);
        }
    }

    private IpcSimulationRequest[] ReadSimulationBatchEvents()
    {
        var eventCount = _session.Reader.ReadInt32();
        if (eventCount is <= 0 or > IpcProtocol.MaxSimulationBatchEvents)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture,
                    $"Simulation batch event count {eventCount} is outside the allowed range 1-{IpcProtocol.MaxSimulationBatchEvents}."));
        }

        var events = new IpcSimulationRequest[eventCount];
        var totalDelayMs = 0;
        for (var i = 0; i < events.Length; i++)
        {
            var inputEvent = new IpcSimulationRequest
            {
                Type = _session.Reader.ReadUInt16(),
                Code = _session.Reader.ReadUInt16(),
                Value = _session.Reader.ReadInt32(),
                DelayAfterMs = _session.Reader.ReadInt32(),
            };

            if (inputEvent.DelayAfterMs is < 0 or > IpcProtocol.MaxSimulationBatchDelayMs)
            {
                throw new InvalidDataException(
                    string.Create(CultureInfo.InvariantCulture,
                        $"Simulation batch delay {inputEvent.DelayAfterMs}ms is outside the allowed range 0-{IpcProtocol.MaxSimulationBatchDelayMs}ms."));
            }

            totalDelayMs += inputEvent.DelayAfterMs;
            if (totalDelayMs > IpcProtocol.MaxSimulationBatchTotalDelayMs)
            {
                throw new InvalidDataException(
                    string.Create(CultureInfo.InvariantCulture,
                        $"Simulation batch total delay {totalDelayMs}ms exceeds the allowed maximum of {IpcProtocol.MaxSimulationBatchTotalDelayMs}ms."));
            }

            events[i] = inputEvent;
        }

        return events;
    }
}
