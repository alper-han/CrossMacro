namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Owns capture-session state: consumer subscriptions, the pending start registry, command
/// replay after reconnects, failure rollback and deferred reconciliation.
/// Lock hierarchy: command gate, then state transition lock.
/// Never waits on the transport write gate while holding the state transition lock; commands are
/// prepared under the lock and sent after it is released.
/// </summary>
internal sealed class IpcCaptureController(
    IpcTransport transport,
    Action throwIfDisposed,
    Action<string> raiseErrorSafely,
    Action<string> raiseErrorDeferred) : IDisposable
{
    private readonly IpcTransport _transport = transport;
    private readonly Action _throwIfDisposed = throwIfDisposed;
    private readonly Action<string> _raiseErrorSafely = raiseErrorSafely;
    private readonly Action<string> _raiseErrorDeferred = raiseErrorDeferred;

    private readonly IpcCaptureState _state = new();
    private readonly Lock _deferredReconcileLock = new();
    private readonly HashSet<Task> _deferredReconcileTasks = [];

    internal SemaphoreSlim CommandGate { get; } = new(1, 1);
    internal PendingCaptureStartRegistry PendingCaptureStarts => _state.PendingCaptureStarts;

    /// <summary>
    /// Waits for the command gate on the live session's token. A drop that cancels that token while this
    /// caller waits only retires the session: the wait continues on its successor (or the reconnect token), so
    /// the command is still applied to the capture state instead of failing with a cancellation the caller
    /// cannot act on. Disposal and a disabled reconnect still cancel the wait.
    /// </summary>
    public void EnterCommandGate()
    {
        while (true)
        {
            var token = _transport.SessionOrReconnectToken;
            try
            {
                CommandGate.Wait(token);
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested
                && !_transport.IsDisposeRequested
                && _transport.SessionOrReconnectToken != token)
            {
                // The session that supplied the token was dropped; wait again on the current one.
            }
        }
    }

    public async Task EnterCommandGateAsync(CancellationToken token)
    {
        await CommandGate.WaitAsync(token).ConfigureAwait(false);
    }

    public void ExitCommandGate()
    {
        _ = CommandGate.Release();
    }

    public void PublishAfterCommands(Action publish)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await EnterCommandGateAsync(_transport.DisposeToken).ConfigureAwait(false);
                ExitCommandGate();
                // User callbacks execute after the command barrier, with no controller lock held.
                if (!_transport.IsDisposed) { publish(); }
            }
            catch (OperationCanceledException) when (_transport.IsDisposed)
            {
                // Shutdown cancels publication waiting behind an active command.
            }
            catch (ObjectDisposedException)
            {
                // Disposal may release the command gate before this queued publication runs.
            }
        }, CancellationToken.None);
    }

    public void StartCapture(string consumerId, bool mouse, bool keyboard)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            throw new ArgumentException("Consumer id cannot be null or whitespace.", nameof(consumerId));
        }

        EnterCommandGate();
        try
        {
            _throwIfDisposed();
            var (commandToSend, pendingStart, shouldSend) = _state.PrepareCaptureCommandUnderLock(consumerId, mouse, keyboard, _transport.IsConnected);

            if (shouldSend)
            {
                DispatchCaptureCommand(commandToSend, pendingStart);
            }
        }
        finally
        {
            ExitCommandGate();
        }
    }

    private void DispatchCaptureCommand(CaptureCommand commandToSend, PendingCaptureStartRegistration? pendingStart)
    {
        try
        {
            if (!SendCaptureCommand(commandToSend, requestId: pendingStart?.RequestId ?? 0))
            {
                _state.MarkTransportStopped();

                PendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _state.MarkTransportStopped();

            PendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
            throw;
        }
    }

    private async Task DispatchCaptureCommandAsync(
        CaptureCommand command,
        PendingCaptureStartRegistration? pendingStart,
        CancellationToken token)
    {
        try
        {
            if (!await SendCaptureCommandAsync(command, pendingStart?.RequestId ?? 0, command.Type is CaptureCommandType.Start, token).ConfigureAwait(false))
            {
                _state.MarkTransportStopped();

                PendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _state.MarkTransportStopped();

            PendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
            throw;
        }
    }

    public async Task StartCaptureAsync(string consumerId, bool mouse, bool keyboard, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            throw new ArgumentException("Consumer id cannot be null or whitespace.", nameof(consumerId));
        }

        _throwIfDisposed();
        token.ThrowIfCancellationRequested();

        PreviousSubscription? previous = null;
        while (true)
        {
            var iteration = await ExecuteStartCaptureIterationAsync(consumerId, mouse, keyboard, previous, token).ConfigureAwait(false);
            previous = iteration.Plan.Previous;
            if (iteration.ShouldStop)
            {
                return;
            }

            var outcome = await WaitForCaptureStartAsync(iteration.Plan, consumerId, mouse, keyboard, token).ConfigureAwait(false);
            if (outcome is PendingStartOutcome.NothingToWaitFor)
            {
                return;
            }
        }
    }

    private async Task<StartCaptureIteration> ExecuteStartCaptureIterationAsync(
        string consumerId,
        bool mouse,
        bool keyboard,
        PreviousSubscription? previous,
        CancellationToken token)
    {
        await EnterCommandGateAsync(token).ConfigureAwait(false);
        try
        {
            _throwIfDisposed();
            var plan = _state.PrepareStartCaptureUnderLock(consumerId, mouse, keyboard, previous);
            if (plan.Command.Type is CaptureCommandType.None)
            {
                return new StartCaptureIteration(plan, ShouldStop: false);
            }

            await SendCaptureCommandOrRestoreAsync(
                plan.Command,
                plan.PendingStart?.RequestId ?? 0,
                consumerId,
                plan.Previous,
                token).ConfigureAwait(false);
            return new StartCaptureIteration(plan, ShouldStop: plan.Command.Type is CaptureCommandType.Stop);
        }
        finally
        {
            ExitCommandGate();
        }
    }

    /// <summary>
    /// Waits for the start this request issued or joined. After an acknowledgement the loop in
    /// <see cref="StartCaptureAsync"/> re-validates the required command; a failure of a start
    /// shared with other consumers is retried only while this consumer's subscription is unchanged.
    /// </summary>
    private async Task<PendingStartOutcome> WaitForCaptureStartAsync(
        StartCapturePlan plan,
        string consumerId,
        bool mouse,
        bool keyboard,
        CancellationToken token)
    {
        if (plan.WaitTask.IsCompleted)
        {
            return PendingStartOutcome.NothingToWaitFor;
        }

        try
        {
            await plan.WaitTask.WaitAsync(token).ConfigureAwait(false);
            return PendingStartOutcome.Acknowledged;
        }
        catch (Exception ex) when (_state.ShouldRetrySharedPendingStartFailure(
            ex,
            plan.JoinedExistingPendingStart,
            consumerId,
            mouse,
            keyboard))
        {
            return PendingStartOutcome.RetryAfterSharedFailure;
        }
    }

    private async Task SendCaptureCommandOrRestoreAsync(
        CaptureCommand command,
        int requestId,
        string consumerId,
        PreviousSubscription previous,
        CancellationToken token)
    {
        try
        {
            _ = await SendCaptureCommandAsync(command, requestId, command.Type is CaptureCommandType.Start, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _state.RestoreSubscriptionAfterSendFailure(consumerId, previous);

            PendingCaptureStarts.ClearCurrent(requestId);
            throw;
        }
    }

    public void StopCapture(string consumerId)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            return;
        }

        EnterCommandGate();
        try
        {
            _throwIfDisposed();
            var (commandToSend, pendingStart, sendAbortStop) = _state.PrepareCaptureStopCommandUnderLock(consumerId);

            if (sendAbortStop)
            {
                // Keep the shared socket alive. StopCapture is queued after the stale start and
                // tears daemon capture down once that delayed start completes.
                _ = SendCaptureCommand(new CaptureCommand(CaptureCommandType.Stop));
            }

            if (commandToSend.Type is not CaptureCommandType.None)
            {
                DispatchCaptureCommand(commandToSend, pendingStart);
            }
        }
        finally
        {
            ExitCommandGate();
        }
    }

    public async Task StopCaptureAsync(string consumerId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            return;
        }

        await EnterCommandGateAsync(token).ConfigureAwait(false);
        try
        {
            _throwIfDisposed();
            var (commandToSend, pendingStart, sendAbortStop) = _state.PrepareCaptureStopCommandUnderLock(consumerId);

            if (sendAbortStop)
            {
                // Keep the shared socket alive. StopCapture is queued after the stale start and
                // tears daemon capture down once that delayed start completes.
                _ = await SendCaptureCommandAsync(
                    new CaptureCommand(CaptureCommandType.Stop),
                    requestId: 0,
                    throwOnFailure: false,
                    CancellationToken.None).ConfigureAwait(false);
            }

            if (commandToSend.Type is not CaptureCommandType.None)
            {
                await DispatchCaptureCommandAsync(commandToSend, pendingStart, token).ConfigureAwait(false);
            }
        }
        finally
        {
            ExitCommandGate();
        }
    }

    private bool SendCaptureCommand(CaptureCommand command, int requestId = 0, bool throwOnFailure = false)
    {
        switch (command.Type)
        {
            case CaptureCommandType.Start:
                Log.Debug(
                    "[IpcClient] TX: StartCapture RequestId={RequestId} Mouse={Mouse} Keyboard={Keyboard}",
                    requestId,
                    command.CaptureMouse,
                    command.CaptureKeyboard);
                return _transport.Send(IpcOpCode.StartCapture, w =>
                {
                    IpcMessageCodec.WriteCaptureStartPayload(w, requestId, command.CaptureMouse, command.CaptureKeyboard);
                }, throwOnFailure);
            case CaptureCommandType.Stop:
                Log.Debug("[IpcClient] TX: StopCapture");
                return _transport.Send(IpcOpCode.StopCapture, throwOnFailure: throwOnFailure);
            default:
                return false;
        }
    }

    private async Task<bool> SendCaptureCommandAsync(
        CaptureCommand command,
        int requestId,
        bool throwOnFailure,
        CancellationToken token)
    {
        return command.Type switch
        {
            CaptureCommandType.Start => await _transport.SendAsync(
                IpcOpCode.StartCapture,
                writer =>
                {
                    IpcMessageCodec.WriteCaptureStartPayload(writer, requestId, command.CaptureMouse, command.CaptureKeyboard);
                },
                throwOnFailure,
                token).ConfigureAwait(false),
            CaptureCommandType.Stop => await _transport.SendAsync(
                IpcOpCode.StopCapture,
                throwOnFailure: throwOnFailure,
                cancellationToken: token).ConfigureAwait(false),
            CaptureCommandType.None => false,
            _ => false,
        };
    }

    public async Task ReplayAfterConnectAsync(CancellationToken token)
    {
        await CommandGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _throwIfDisposed();
            var (command, pendingStart) = _state.PrepareReplay();

            if (command.Type is CaptureCommandType.None)
            {
                return;
            }

            try
            {
                _ = await SendCaptureCommandAsync(command, pendingStart?.RequestId ?? 0, command.Type is CaptureCommandType.Start, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _state.MarkTransportStopped();

                PendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
                throw;
            }
        }
        finally
        {
            _ = CommandGate.Release();
        }
    }

    /// <summary>Transport dropped: mark stopped, fail the pending start and notify.</summary>
    public void OnTransportDropped(bool deferErrorNotifications)
    {
        _state.MarkTransportStopped();

        var failedPendingStart = PendingCaptureStarts.TryFailCurrent(
            new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                "Daemon connection was lost during capture startup."),
            out var notifyOnFailure);
        if (notifyOnFailure && failedPendingStart)
        {
            if (deferErrorNotifications)
            {
                _raiseErrorDeferred("Daemon connection was lost during capture startup.");
            }
            else
            {
                _raiseErrorSafely("Daemon connection was lost during capture startup.");
            }
        }
    }

    /// <summary>Read loop failed for the live session.</summary>
    public void OnReadLoopFailure(Exception exception)
    {
        _state.MarkTransportStopped();

        var failedPendingStart = PendingCaptureStarts.TryFailCurrent(
            new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                "Daemon connection was lost during capture startup.",
                exception),
            out var notifyOnFailure);
        if (notifyOnFailure || !failedPendingStart)
        {
            _raiseErrorSafely("Connection lost: " + exception.Message);
        }
    }

    /// <summary>A live-session send failed; runs before the transport drops the connection.</summary>
    public void OnSendFailure(IpcOpCode opcode, Exception exception)
    {
        _state.MarkTransportStopped();

        var failedPendingStart = PendingCaptureStarts.TryFailCurrent(
            new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                $"Failed to send IPC command '{opcode}'.",
                exception),
            out var notifyOnFailure);
        if (notifyOnFailure || !failedPendingStart)
        {
            _raiseErrorDeferred($"IPC send failed ({opcode}): {exception.Message}");
        }
    }

    public void OnCleanupSubscriptions(bool clearSubscriptions)
    {
        _state.CleanupSubscriptions(clearSubscriptions);
    }

    public void HandleCaptureStartedMessage(int startedRequestId)
    {
        Log.Debug("[IpcClient] RX: CaptureStarted RequestId={RequestId}", startedRequestId);
        if (PendingCaptureStarts.TryComplete(startedRequestId, out var completedStart))
        {
            _ = completedStart.Completion.TrySetResult(true);
            _ = StartDeferredCaptureReconcileAsync();
            return;
        }

        Log.Debug("[IpcClient] Ignoring stale CaptureStarted for RequestId={RequestId}", startedRequestId);
    }

    public void HandleCaptureStartFailedMessage(int failedRequestId, string failureMessage)
    {
        var failureException = new InvalidOperationException(failureMessage);
        Log.Warning(
            "[IpcClient] RX: CaptureStartFailed RequestId={RequestId} Message={Message}",
            failedRequestId,
            failureMessage);

        var hasFailedPendingStart = PendingCaptureStarts.TryFail(
            failedRequestId,
            out var failureContext);
        if (!hasFailedPendingStart)
        {
            Log.Debug("[IpcClient] Ignoring stale CaptureStartFailed for RequestId={RequestId}", failedRequestId);
            return;
        }

        bool shouldReconcile = _state.RollbackFailedParticipants(failureContext);

        if (shouldReconcile && !TryReconcileCaptureStateNow())
        {
            _ = StartDeferredCaptureReconcileAsync();
        }

        if (failureContext.NotifyOnFailure)
        {
            try
            {
                _raiseErrorSafely(failureMessage);
            }
            finally
            {
                _ = failureContext.Completion.TrySetException(failureException);
            }
            return;
        }

        _ = failureContext.Completion.TrySetException(failureException);
    }

    private bool TryReconcileCaptureStateNow()
    {
        if (_transport.IsDisposed || !CommandGate.Wait(0, _transport.SessionOrReconnectToken))
        {
            return false;
        }

        try
        {
            _throwIfDisposed();
            return TryDispatchReconcileCommandUnderGate();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[IpcClient] Immediate capture reconcile failed");
            return true;
        }
        finally
        {
            ExitCommandGate();
        }
    }

    public Task StartDeferredCaptureReconcileAsync()
    {
        if (!_transport.TryGetLiveToken(out var transportToken))
        {
            return Task.CompletedTask;
        }

        var reconcileTask = Task.Run(async () =>
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(transportToken, _transport.DisposeToken);
            try
            {
                await EnterCommandGateAsync(linkedCts.Token).ConfigureAwait(false);
                try
                {
                    _throwIfDisposed();
                    _ = TryDispatchReconcileCommandUnderGate();
                }
                finally
                {
                    ExitCommandGate();
                }
            }
            catch (OperationCanceledException)
            {
                // expected when the reconciliation task is cancelled during shutdown.
            }
            catch (ObjectDisposedException)
            {
                // expected when captures are torn down concurrently and already disposed.
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[IpcClient] Failed to reconcile capture state");
            }
        }, CancellationToken.None);

        lock (_deferredReconcileLock)
        {
            _ = _deferredReconcileTasks.Add(reconcileTask);
        }

        _ = reconcileTask.ContinueWith(
            completedTask =>
            {
                _ = completedTask.Exception;
                lock (_deferredReconcileLock)
                {
                    _ = _deferredReconcileTasks.Remove(completedTask);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return reconcileTask;
    }

    private bool TryDispatchReconcileCommandUnderGate()
    {
        var (deferredCommand, deferredPendingStart) = _state.PrepareReconcile();
        if (deferredCommand.Type is CaptureCommandType.None) { return true; }

        try
        {
            _ = SendCaptureCommand(
                deferredCommand,
                requestId: deferredPendingStart?.RequestId ?? 0,
                throwOnFailure: deferredCommand.Type is CaptureCommandType.Start);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _state.MarkTransportStopped();

            PendingCaptureStarts.ClearCurrent(deferredPendingStart?.RequestId ?? 0);
            throw;
        }

        return true;
    }

    /// <summary>Awaits deferred reconcile tasks during disposal (skipping the current task).</summary>
    public async Task WaitForDeferredReconcilesAsync()
    {
        Task[] deferredTasks;
        lock (_deferredReconcileLock)
        {
            deferredTasks = [.. _deferredReconcileTasks];
        }

        foreach (var deferredTask in deferredTasks)
        {
            if (deferredTask.Id == Task.CurrentId)
            {
                continue;
            }

            try
            {
                await deferredTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Deferred reconciliation observes expected cancellation and failures itself.
            }
        }
    }

    public void Dispose()
    {
        CommandGate.Dispose();
    }
}
