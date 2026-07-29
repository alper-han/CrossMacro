namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Owns capture-session state: consumer subscriptions, the pending start registry, command
/// replay after reconnects, failure rollback and deferred reconciliation.
/// Lock hierarchy (outer to inner): <c>CommandGate</c> → <c>_captureLock</c>.
/// Never waits on the transport write gate while holding <c>_captureLock</c>; commands are
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

    private readonly CaptureSubscriptionCoordinator _captureCoordinator = new();
    private readonly Lock _captureLock = new();
    private readonly Lock _deferredReconcileLock = new();
    private readonly HashSet<Task> _deferredReconcileTasks = [];

    internal SemaphoreSlim CommandGate { get; } = new(1, 1);
    internal PendingCaptureStartRegistry PendingCaptureStarts { get; } = new();

    public void EnterCommandGate()
    {
        CommandGate.Wait(_transport.SessionOrReconnectToken);
    }

    public async Task EnterCommandGateAsync(CancellationToken token)
    {
        await CommandGate.WaitAsync(token).ConfigureAwait(false);
    }

    public void ExitCommandGate()
    {
        _ = CommandGate.Release();
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
            var (commandToSend, pendingStart, shouldSend) = PrepareCaptureCommandUnderLock(consumerId, mouse, keyboard);

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

    private (CaptureCommand Command, PendingCaptureStartRegistration? PendingStart, bool ShouldSend) PrepareCaptureCommandUnderLock(
        string consumerId, bool mouse, bool keyboard)
    {
        lock (_captureLock)
        {
            _captureCoordinator.SetSubscription(consumerId, mouse, keyboard);

            if (PendingCaptureStarts.TryGetPendingTaskAsync() is { IsCompleted: false })
            {
                PendingCaptureStarts.RequestFailureNotification();
                return (default, null, false);
            }

            var command = _captureCoordinator.GetRequiredCommand();
            if (command.Type is CaptureCommandType.None)
            {
                return (default, null, false);
            }

            if (command.Type is CaptureCommandType.Start && !_transport.IsConnected)
            {
                return (default, null, false);
            }

            PendingCaptureStartRegistration? pendingStart = null;
            if (command.Type is CaptureCommandType.Start)
            {
                var previousTransportCommand = _captureCoordinator.GetTransportCommand();
                pendingStart = PendingCaptureStarts.Begin(
                    command,
                    notifyOnFailure: true,
                    forceReconcileOnFailure: true,
                    previousTransportCommand: previousTransportCommand);
            }

            _captureCoordinator.MarkCommandIssued(command);
            return (command, pendingStart, true);
        }
    }

    private void DispatchCaptureCommand(CaptureCommand commandToSend, PendingCaptureStartRegistration? pendingStart)
    {
        try
        {
            if (!SendCaptureCommand(commandToSend, requestId: pendingStart?.RequestId ?? 0))
            {
                lock (_captureLock)
                {
                    _captureCoordinator.MarkTransportStopped();
                }

                PendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            lock (_captureLock)
            {
                _captureCoordinator.MarkTransportStopped();
            }

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
                lock (_captureLock)
                {
                    _captureCoordinator.MarkTransportStopped();
                }

                PendingCaptureStarts.ClearCurrent(pendingStart?.RequestId ?? 0);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            lock (_captureLock)
            {
                _captureCoordinator.MarkTransportStopped();
            }

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

        var subscriptionRegistered = false;
        bool hadPreviousSubscription = false;
        bool previousCaptureMouse = false;
        bool previousCaptureKeyboard = false;

        while (true)
        {
            var iteration = await ExecuteStartCaptureIterationAsync(
                consumerId,
                mouse,
                keyboard,
                subscriptionRegistered,
                hadPreviousSubscription,
                previousCaptureMouse,
                previousCaptureKeyboard,
                token).ConfigureAwait(false);
            var waitTask = iteration.WaitTask;
            var joinedExistingPendingStart = iteration.JoinedExistingPendingStart;
            subscriptionRegistered = iteration.SubscriptionRegistered;
            hadPreviousSubscription = iteration.HadPreviousSubscription;
            previousCaptureMouse = iteration.PreviousCaptureMouse;
            previousCaptureKeyboard = iteration.PreviousCaptureKeyboard;
            if (iteration.ShouldStop)
            {
                return;
            }

            if (!await WaitForCaptureStartAsync(
                    waitTask,
                    joinedExistingPendingStart,
                    consumerId,
                    mouse,
                    keyboard,
                    token).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task<(
        Task WaitTask,
        bool JoinedExistingPendingStart,
        bool SubscriptionRegistered,
        bool HadPreviousSubscription,
        bool PreviousCaptureMouse,
        bool PreviousCaptureKeyboard,
        bool ShouldStop)> ExecuteStartCaptureIterationAsync(
        string consumerId,
        bool mouse,
        bool keyboard,
        bool subscriptionRegistered,
        bool hadPreviousSubscription,
        bool previousCaptureMouse,
        bool previousCaptureKeyboard,
        CancellationToken token)
    {
        await EnterCommandGateAsync(token).ConfigureAwait(false);
        try
        {
            _throwIfDisposed();
            Task waitTask;
            PendingCaptureStartRegistration? pendingStart;
            CaptureCommand command;
            bool joinedExistingPendingStart;
            lock (_captureLock)
            {
                (waitTask, pendingStart, command, joinedExistingPendingStart, subscriptionRegistered,
                    hadPreviousSubscription, previousCaptureMouse, previousCaptureKeyboard) = PrepareStartCaptureUnderLock(
                        consumerId,
                        mouse,
                        keyboard,
                        subscriptionRegistered,
                        hadPreviousSubscription,
                        previousCaptureMouse,
                        previousCaptureKeyboard);
            }

            var shouldStop = await DispatchPreparedCaptureCommandAsync(
                command,
                pendingStart,
                consumerId,
                hadPreviousSubscription,
                previousCaptureMouse,
                previousCaptureKeyboard,
                token).ConfigureAwait(false);
            return (waitTask, joinedExistingPendingStart, subscriptionRegistered, hadPreviousSubscription,
                previousCaptureMouse, previousCaptureKeyboard, shouldStop);
        }
        finally
        {
            ExitCommandGate();
        }
    }

    private async Task<bool> DispatchPreparedCaptureCommandAsync(
        CaptureCommand command,
        PendingCaptureStartRegistration? pendingStart,
        string consumerId,
        bool hadPreviousSubscription,
        bool previousCaptureMouse,
        bool previousCaptureKeyboard,
        CancellationToken token)
    {
        if (command.Type is CaptureCommandType.None)
        {
            return false;
        }

        await SendCaptureCommandOrRestoreAsync(
            command,
            pendingStart?.RequestId ?? 0,
            consumerId,
            hadPreviousSubscription,
            previousCaptureMouse,
            previousCaptureKeyboard,
            token).ConfigureAwait(false);
        return command.Type is CaptureCommandType.Stop;
    }

    private async Task<bool> WaitForCaptureStartAsync(
        Task waitTask,
        bool joinedExistingPendingStart,
        string consumerId,
        bool mouse,
        bool keyboard,
        CancellationToken token)
    {
        if (waitTask.IsCompleted)
        {
            return false;
        }

        try
        {
            await waitTask.WaitAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ShouldRetrySharedPendingStartFailure(
            ex,
            joinedExistingPendingStart,
            consumerId,
            mouse,
            keyboard))
        {
            return true;
        }

        return true;
    }

    private async Task SendCaptureCommandOrRestoreAsync(
        CaptureCommand command,
        int requestId,
        string consumerId,
        bool hadPreviousSubscription,
        bool previousCaptureMouse,
        bool previousCaptureKeyboard,
        CancellationToken token)
    {
        try
        {
            _ = await SendCaptureCommandAsync(command, requestId, command.Type is CaptureCommandType.Start, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            lock (_captureLock)
            {
                _ = RestoreSubscription_NoLock(consumerId, hadPreviousSubscription, previousCaptureMouse, previousCaptureKeyboard);
                _captureCoordinator.MarkTransportStopped();
            }

            PendingCaptureStarts.ClearCurrent(requestId);
            throw;
        }
    }

    private (
        Task WaitTask,
        PendingCaptureStartRegistration? PendingStart,
        CaptureCommand Command,
        bool JoinedExistingPendingStart,
        bool SubscriptionRegistered,
        bool HadPreviousSubscription,
        bool PreviousCaptureMouse,
        bool PreviousCaptureKeyboard) PrepareStartCaptureUnderLock(
        string consumerId,
        bool mouse,
        bool keyboard,
        bool subscriptionRegistered,
        bool hadPreviousSubscription,
        bool previousCaptureMouse,
        bool previousCaptureKeyboard)
    {
        PendingCaptureStartRegistration? pendingStart = null;
        CaptureCommand commandToSend = default;
        var waitTask = PendingCaptureStarts.TryGetPendingTaskAsync();
        var joinedExistingPendingStart = false;

        if (!subscriptionRegistered)
        {
            hadPreviousSubscription = _captureCoordinator.TryGetSubscription(consumerId, out previousCaptureMouse, out previousCaptureKeyboard);
            _captureCoordinator.SetSubscription(consumerId, mouse, keyboard);
            subscriptionRegistered = true;
        }

        if (waitTask.IsCompleted)
        {
            var command = _captureCoordinator.GetRequiredCommand();
            if (command.Type is CaptureCommandType.None)
            {
                return (waitTask, null, default, false, subscriptionRegistered, hadPreviousSubscription, previousCaptureMouse, previousCaptureKeyboard);
            }

            if (command.Type is CaptureCommandType.Start)
            {
                pendingStart = PendingCaptureStarts.Begin(
                    command,
                    notifyOnFailure: false,
                    forceReconcileOnFailure: false,
                    previousTransportCommand: _captureCoordinator.GetTransportCommand(),
                    originConsumerId: consumerId,
                    originHadPreviousSubscription: hadPreviousSubscription,
                    originCaptureMouse: previousCaptureMouse,
                    originCaptureKeyboard: previousCaptureKeyboard);
                waitTask = pendingStart.Value.Completion.Task;
            }

            _captureCoordinator.MarkCommandIssued(command);
            commandToSend = command;
        }
        else
        {
            PendingCaptureStarts.RegisterAsyncParticipant(consumerId, hadPreviousSubscription, previousCaptureMouse, previousCaptureKeyboard);
            joinedExistingPendingStart = true;
        }

        return (waitTask, pendingStart, commandToSend, joinedExistingPendingStart, subscriptionRegistered, hadPreviousSubscription, previousCaptureMouse, previousCaptureKeyboard);
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
            var (commandToSend, pendingStart, sendAbortStop) = PrepareCaptureStopCommandUnderLock(consumerId);

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
            var (commandToSend, pendingStart, sendAbortStop) = PrepareCaptureStopCommandUnderLock(consumerId);

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

    private (CaptureCommand Command, PendingCaptureStartRegistration? PendingStart, bool SendAbortStop) PrepareCaptureStopCommandUnderLock(string consumerId)
    {
        lock (_captureLock)
        {
            _captureCoordinator.RemoveSubscription(consumerId);

            if (PendingCaptureStarts.TryGetPendingTaskAsync() is { IsCompleted: false })
            {
                PendingCaptureStarts.MarkSubscriptionRemoved(consumerId);

                var sendAbortStop = false;
                if (!_captureCoordinator.HasSubscriptions)
                {
                    sendAbortStop = AbortPendingCaptureStart_NoLock();
                }

                return (default, null, sendAbortStop);
            }

            var command = _captureCoordinator.GetRequiredCommand();
            if (command.Type is CaptureCommandType.None)
            {
                return (default, null, false);
            }

            PendingCaptureStartRegistration? pendingStart = null;
            if (command.Type is CaptureCommandType.Start)
            {
                var previousTransportCommand = _captureCoordinator.GetTransportCommand();
                pendingStart = PendingCaptureStarts.Begin(
                    command,
                    notifyOnFailure: true,
                    forceReconcileOnFailure: true,
                    previousTransportCommand: previousTransportCommand);
            }

            _captureCoordinator.MarkCommandIssued(command);
            return (command, pendingStart, false);
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
                    w.Write(requestId);
                    w.Write(command.CaptureMouse);
                    w.Write(command.CaptureKeyboard);
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
                    writer.Write(requestId);
                    writer.Write(command.CaptureMouse);
                    writer.Write(command.CaptureKeyboard);
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
            PendingCaptureStartRegistration? pendingStart = null;
            CaptureCommand command;
            lock (_captureLock)
            {
                _captureCoordinator.ResetTransportState();
                command = _captureCoordinator.GetRequiredCommand();
                if (command.Type is CaptureCommandType.Start)
                {
                    var previousTransportCommand = _captureCoordinator.GetTransportCommand();
                    pendingStart = PendingCaptureStarts.TryReissueCurrent(
                        command,
                        notifyOnFailure: true,
                        forceReconcileOnFailure: true,
                        previousTransportCommand: previousTransportCommand,
                        out var reissuedPendingStart)
                        ? reissuedPendingStart
                        : PendingCaptureStarts.Begin(
                            command,
                            notifyOnFailure: true,
                            forceReconcileOnFailure: true,
                            previousTransportCommand: previousTransportCommand);
                }

                if (command.Type is not CaptureCommandType.None)
                {
                    _captureCoordinator.MarkCommandIssued(command);
                }
            }

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
                lock (_captureLock)
                {
                    _captureCoordinator.MarkTransportStopped();
                }

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
        lock (_captureLock)
        {
            _captureCoordinator.MarkTransportStopped();
        }

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
        lock (_captureLock)
        {
            _captureCoordinator.MarkTransportStopped();
        }

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
        lock (_captureLock)
        {
            if (clearSubscriptions)
            {
                _captureCoordinator.Clear();
            }
            else
            {
                _captureCoordinator.ResetTransportState();
            }
        }
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

        bool shouldReconcile = RollbackFailedParticipants(failureContext);

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

    private bool RollbackFailedParticipants(PendingCaptureStartFailureContext failureContext)
    {
        var removedConsumersSinceStart = failureContext.RemovedConsumersSinceStart.Length is 0
            ? null
            : new HashSet<string>(failureContext.RemovedConsumersSinceStart, StringComparer.Ordinal);
        bool shouldReconcile;
        var rollbackChangedSubscriptions = false;
        lock (_captureLock)
        {
            _captureCoordinator.MarkTransportStopped();
            foreach (var participant in failureContext.FailedAsyncParticipants)
            {
                if (!participant.ShouldRestoreOnFailure)
                {
                    continue;
                }

                if ((removedConsumersSinceStart?.Contains(participant.ConsumerId)) is true)
                {
                    continue;
                }

                rollbackChangedSubscriptions |= RestoreSubscription_NoLock(
                    participant.ConsumerId,
                    participant.HadPreviousSubscription,
                    participant.PreviousCaptureMouse,
                    participant.PreviousCaptureKeyboard);
            }

            var currentRequiredCommand = _captureCoordinator.GetRequiredCommand();
            shouldReconcile = failureContext.ForceReconcileOnFailure ||
                CaptureStartFailureReconciler.ShouldReconcile(
                    currentRequiredCommand,
                    failureContext.FailedCommand,
                    failureContext.FailedAsyncParticipants.Length is 0 && failureContext.FailedPreviousTransportCommand.Type is CaptureCommandType.Start,
                    failureContext.SubscriptionRemovedSinceStart,
                    rollbackChangedSubscriptions);
        }

        return shouldReconcile;
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
        PendingCaptureStartRegistration? deferredPendingStart = null;
        CaptureCommand deferredCommand;

        lock (_captureLock)
        {
            if (PendingCaptureStarts.TryGetPendingTaskAsync() is { IsCompleted: false })
            {
                return true;
            }

            deferredCommand = _captureCoordinator.GetRequiredCommand();
            if (deferredCommand.Type is CaptureCommandType.None)
            {
                return true;
            }

            if (deferredCommand.Type is CaptureCommandType.Start)
            {
                deferredPendingStart = PendingCaptureStarts.Begin(deferredCommand, notifyOnFailure: true);
            }

            _captureCoordinator.MarkCommandIssued(deferredCommand);
        }

        try
        {
            _ = SendCaptureCommand(
                deferredCommand,
                requestId: deferredPendingStart?.RequestId ?? 0,
                throwOnFailure: deferredCommand.Type is CaptureCommandType.Start);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            lock (_captureLock)
            {
                _captureCoordinator.MarkTransportStopped();
            }

            PendingCaptureStarts.ClearCurrent(deferredPendingStart?.RequestId ?? 0);
            throw;
        }

        return true;
    }

    private bool ShouldRetrySharedPendingStartFailure(
        Exception exception,
        bool joinedExistingPendingStart,
        string consumerId,
        bool mouse,
        bool keyboard)
    {
        if (!joinedExistingPendingStart || exception is not InvalidOperationException)
        {
            return false;
        }

        lock (_captureLock)
        {
            return _captureCoordinator.TryGetSubscription(
                consumerId,
                out var currentCaptureMouse,
                out var currentCaptureKeyboard) &&
                currentCaptureMouse == mouse &&
                currentCaptureKeyboard == keyboard;
        }
    }

    private bool AbortPendingCaptureStart_NoLock()
    {
        _captureCoordinator.MarkTransportStopped();

        _ = PendingCaptureStarts.TryFailCurrent(
            new OperationCanceledException("Capture startup was cancelled before daemon acknowledgement."),
            out _);

        // The Stop command must not be sent while holding _captureLock because the transport
        // send path waits on the write gate. The caller performs the send after the lock exits.
        return true;
    }

    private bool RestoreSubscription_NoLock(
        string consumerId,
        bool hadPreviousSubscription,
        bool previousCaptureMouse,
        bool previousCaptureKeyboard)
    {
        var hasCurrentSubscription = _captureCoordinator.TryGetSubscription(
            consumerId,
            out var currentCaptureMouse,
            out var currentCaptureKeyboard);

        if (hadPreviousSubscription)
        {
            if (hasCurrentSubscription &&
                currentCaptureMouse == previousCaptureMouse &&
                currentCaptureKeyboard == previousCaptureKeyboard)
            {
                return false;
            }

            _captureCoordinator.SetSubscription(consumerId, previousCaptureMouse, previousCaptureKeyboard);
            return true;
        }

        if (!hasCurrentSubscription)
        {
            return false;
        }

        _captureCoordinator.RemoveSubscription(consumerId);
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
