namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>Owns subscription and pending-start transitions. No transition performs transport I/O.</summary>
internal sealed class IpcCaptureState
{
    private readonly CaptureSubscriptionCoordinator _captureCoordinator = new();
    private readonly Lock _captureLock = new();
    internal PendingCaptureStartRegistry PendingCaptureStarts { get; } = new();

    public void MarkTransportStopped()
    {
        lock (_captureLock) { _captureCoordinator.MarkTransportStopped(); }
    }

    public void CleanupSubscriptions(bool clearSubscriptions)
    {
        lock (_captureLock)
        {
            if (clearSubscriptions) { _captureCoordinator.Clear(); }
            else { _captureCoordinator.ResetTransportState(); }
        }
    }

    public (CaptureCommand Command, PendingCaptureStartRegistration? PendingStart, bool ShouldSend) PrepareCaptureCommandUnderLock(
        string consumerId, bool mouse, bool keyboard, bool isConnected)
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

            if (command.Type is CaptureCommandType.Start && !isConnected)
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

    /// <summary>
    /// Registers (or re-validates) a consumer's start request. <paramref name="previous"/> is the
    /// subscription captured by an earlier iteration of the same request, or <see langword="null"/>
    /// on the first call.
    /// </summary>
    public StartCapturePlan PrepareStartCaptureUnderLock(
        string consumerId,
        bool mouse,
        bool keyboard,
        PreviousSubscription? previous)
    {
        lock (_captureLock)
        {
            var waitTask = PendingCaptureStarts.TryGetPendingTaskAsync();

            if (previous is not { } registered)
            {
                registered = _captureCoordinator.TryGetSubscription(consumerId, out var previousMouse, out var previousKeyboard)
                    ? new PreviousSubscription(Existed: true, previousMouse, previousKeyboard)
                    : new PreviousSubscription(Existed: false, CaptureMouse: false, CaptureKeyboard: false);
                _captureCoordinator.SetSubscription(consumerId, mouse, keyboard);
            }

            if (!waitTask.IsCompleted)
            {
                PendingCaptureStarts.RegisterAsyncParticipant(
                    consumerId,
                    registered.Existed,
                    registered.CaptureMouse,
                    registered.CaptureKeyboard);
                return new StartCapturePlan(waitTask, PendingStart: null, Command: default, JoinedExistingPendingStart: true, registered);
            }

            var command = _captureCoordinator.GetRequiredCommand();
            if (command.Type is CaptureCommandType.None)
            {
                return new StartCapturePlan(waitTask, PendingStart: null, Command: default, JoinedExistingPendingStart: false, registered);
            }

            PendingCaptureStartRegistration? pendingStart = null;
            if (command.Type is CaptureCommandType.Start)
            {
                pendingStart = PendingCaptureStarts.Begin(
                    command,
                    notifyOnFailure: false,
                    forceReconcileOnFailure: false,
                    previousTransportCommand: _captureCoordinator.GetTransportCommand(),
                    originConsumerId: consumerId,
                    originHadPreviousSubscription: registered.Existed,
                    originCaptureMouse: registered.CaptureMouse,
                    originCaptureKeyboard: registered.CaptureKeyboard);
                waitTask = pendingStart.Value.Completion.Task;
            }

            _captureCoordinator.MarkCommandIssued(command);
            return new StartCapturePlan(waitTask, pendingStart, command, JoinedExistingPendingStart: false, registered);
        }
    }

    public (CaptureCommand Command, PendingCaptureStartRegistration? PendingStart, bool SendAbortStop) PrepareCaptureStopCommandUnderLock(string consumerId)
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
                    sendAbortStop = AbortPendingCaptureStartUnderLock();
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

    public bool RollbackFailedParticipants(PendingCaptureStartFailureContext failureContext)
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

                rollbackChangedSubscriptions |= RestoreSubscription(
                    participant.ConsumerId,
                    new PreviousSubscription(
                        participant.HadPreviousSubscription,
                        participant.PreviousCaptureMouse,
                        participant.PreviousCaptureKeyboard));
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

    public bool ShouldRetrySharedPendingStartFailure(
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

    private bool AbortPendingCaptureStartUnderLock()
    {
        _captureCoordinator.MarkTransportStopped();

        _ = PendingCaptureStarts.TryFailCurrent(
            new OperationCanceledException("Capture startup was cancelled before daemon acknowledgement."),
            out _);

        // The Stop command must not be sent while holding _captureLock because the transport
        // send path waits on the write gate. The caller performs the send after the lock exits.
        return true;
    }

    /// <summary>
    /// Puts a consumer's subscription back to what it was before a failed start. Takes the capture
    /// lock (re-entrantly when the caller already holds it). Returns whether anything changed.
    /// </summary>
    private bool RestoreSubscription(string consumerId, PreviousSubscription previous)
    {
        lock (_captureLock)
        {
            var hasCurrentSubscription = _captureCoordinator.TryGetSubscription(
                consumerId,
                out var currentCaptureMouse,
                out var currentCaptureKeyboard);

            if (previous.Existed)
            {
                if (hasCurrentSubscription &&
                    currentCaptureMouse == previous.CaptureMouse &&
                    currentCaptureKeyboard == previous.CaptureKeyboard)
                {
                    return false;
                }

                _captureCoordinator.SetSubscription(consumerId, previous.CaptureMouse, previous.CaptureKeyboard);
                return true;
            }

            if (!hasCurrentSubscription)
            {
                return false;
            }

            _captureCoordinator.RemoveSubscription(consumerId);
            return true;
        }
    }

    public void RestoreSubscriptionAfterSendFailure(string consumerId, PreviousSubscription previous)
    {
        lock (_captureLock)
        {
            _ = RestoreSubscription(consumerId, previous);
            _captureCoordinator.MarkTransportStopped();
        }
    }

    public (CaptureCommand Command, PendingCaptureStartRegistration? PendingStart) PrepareReplay()
    {
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
        return (command, pendingStart);
    }

    public (CaptureCommand Command, PendingCaptureStartRegistration? PendingStart) PrepareReconcile()
    {
        PendingCaptureStartRegistration? deferredPendingStart = null;
        CaptureCommand deferredCommand;
        lock (_captureLock)
        {
            if (PendingCaptureStarts.TryGetPendingTaskAsync() is { IsCompleted: false })
            {
                return (default, null);
            }

            deferredCommand = _captureCoordinator.GetRequiredCommand();
            if (deferredCommand.Type is CaptureCommandType.None)
            {
                return (default, null);
            }

            if (deferredCommand.Type is CaptureCommandType.Start)
            {
                deferredPendingStart = PendingCaptureStarts.Begin(deferredCommand, notifyOnFailure: true);
            }

            _captureCoordinator.MarkCommandIssued(deferredCommand);
        }
        return (deferredCommand, deferredPendingStart);
    }
}
