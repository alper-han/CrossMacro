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

    public (
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
        lock (_captureLock)
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
        lock (_captureLock)
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
    }

    public void RestoreSubscriptionAfterSendFailure(string consumerId, bool hadPreviousSubscription, bool previousCaptureMouse, bool previousCaptureKeyboard)
    {
        lock (_captureLock)
        {
            _ = RestoreSubscription_NoLock(consumerId, hadPreviousSubscription, previousCaptureMouse, previousCaptureKeyboard);
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
