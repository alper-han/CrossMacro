using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Linux.Ipc;

internal readonly record struct PendingCaptureStartRegistration(
    int RequestId,
    TaskCompletionSource<bool> Completion);

internal readonly record struct PendingCaptureStartFailureContext(
    bool NotifyOnFailure,
    bool ForceReconcileOnFailure,
    CaptureCommand FailedCommand,
    PendingAsyncParticipantSnapshot[] FailedAsyncParticipants,
    CaptureCommand FailedPreviousTransportCommand,
    bool SubscriptionRemovedSinceStart,
    string[] RemovedConsumersSinceStart,
    TaskCompletionSource<bool> Completion);

internal readonly record struct PendingAsyncParticipantSnapshot(
    string ConsumerId,
    bool HadPreviousSubscription,
    bool PreviousCaptureMouse,
    bool PreviousCaptureKeyboard,
    bool ShouldRestoreOnFailure);

internal sealed class PendingCaptureStartRegistry
{
    private readonly Lock _lock = new();
    private PendingCaptureStartState? _pending;
    private int _nextRequestId;

    public PendingCaptureStartRegistration Begin(
        CaptureCommand command,
        bool notifyOnFailure,
        bool forceReconcileOnFailure = false,
        CaptureCommand previousTransportCommand = default,
        string? originConsumerId = null,
        bool originHadPreviousSubscription = false,
        bool originCaptureMouse = false,
        bool originCaptureKeyboard = false)
    {
        lock (_lock)
        {
            if (_pending is { Completion: { Task: { IsCompleted: false } } })
            {
                throw new InvalidOperationException("A capture start is already pending.");
            }

            _pending = new PendingCaptureStartState(
                Interlocked.Increment(ref _nextRequestId),
                command,
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                notifyOnFailure,
                forceReconcileOnFailure,
                previousTransportCommand);
            _pending.RegisterAsyncParticipant(
                originConsumerId,
                originHadPreviousSubscription,
                originCaptureMouse,
                originCaptureKeyboard);
            return new PendingCaptureStartRegistration(_pending.RequestId, _pending.Completion);
        }
    }

    public void RegisterAsyncParticipant(
        string consumerId,
        bool hadPreviousSubscription,
        bool previousCaptureMouse,
        bool previousCaptureKeyboard)
    {
        lock (_lock)
        {
            if (_pending is { Completion: { Task: { IsCompleted: false } } } pendingStart)
            {
                pendingStart.RegisterAsyncParticipant(
                    consumerId,
                    hadPreviousSubscription,
                    previousCaptureMouse,
                    previousCaptureKeyboard,
                    shouldRestoreOnFailure: false);
            }
        }
    }

    public void RequestFailureNotification()
    {
        lock (_lock)
        {
            if (_pending is { Completion: { Task: { IsCompleted: false } } } pendingStart)
            {
                pendingStart.NotifyOnFailure = true;
            }
        }
    }

    public bool TryComplete(int requestId, out PendingCaptureStartRegistration completed)
    {
        PendingCaptureStartState? pendingStart;
        lock (_lock)
        {
            if (_pending is not { Completion: { Task: { IsCompleted: false } } } current ||
                current.RequestId != requestId)
            {
                completed = default;
                return false;
            }

            pendingStart = current;
            _pending = null;
        }

        completed = new PendingCaptureStartRegistration(
            pendingStart.RequestId,
            pendingStart.Completion);
        return true;
    }

    public bool TryFail(int requestId, out PendingCaptureStartFailureContext failureContext)
    {
        PendingCaptureStartState? pendingStart;
        lock (_lock)
        {
            if (_pending is not { Completion: { Task: { IsCompleted: false } } } current ||
                current.RequestId != requestId)
            {
                failureContext = default;
                return false;
            }

            pendingStart = current;
            _pending = null;
        }

        failureContext = new PendingCaptureStartFailureContext(
            pendingStart.NotifyOnFailure,
            pendingStart.ForceReconcileOnFailure,
            pendingStart.Command,
            pendingStart.GetAsyncParticipantsSnapshot(),
            pendingStart.PreviousTransportCommand,
            pendingStart.SubscriptionRemovedSinceStart,
            pendingStart.GetRemovedConsumerIdsSnapshot(),
            pendingStart.Completion);
        return true;
    }

    public bool TryFailCurrent(Exception exception, out bool notifyOnFailure)
    {
        PendingCaptureStartState? pendingStart;
        lock (_lock)
        {
            pendingStart = _pending;
            _pending = null;
        }

        notifyOnFailure = pendingStart?.NotifyOnFailure ?? false;
        return pendingStart?.Completion.TrySetException(exception) ?? false;
    }

    public Task? TryGetPendingTask()
    {
        lock (_lock)
        {
            if (_pending is not { Completion: { Task: { IsCompleted: false } } } pendingStart)
            {
                _pending = null;
                return null;
            }

            return pendingStart.Completion.Task;
        }
    }

    public void MarkSubscriptionRemoved(string consumerId)
    {
        lock (_lock)
        {
            if (_pending is { Completion: { Task: { IsCompleted: false } } } pendingStart)
            {
                pendingStart.MarkSubscriptionRemovedSinceStart(consumerId);
            }
        }
    }

    public void ClearCurrent(int requestId)
    {
        if (requestId <= 0)
        {
            return;
        }

        lock (_lock)
        {
            if (_pending is { RequestId: var currentRequestId } && currentRequestId == requestId)
            {
                _pending = null;
            }
        }
    }

    private sealed class PendingCaptureStartState(
        int requestId,
        CaptureCommand command,
        TaskCompletionSource<bool> completion,
        bool notifyOnFailure,
        bool forceReconcileOnFailure,
        CaptureCommand previousTransportCommand)
    {
        private readonly Dictionary<string, PendingAsyncParticipantSnapshot> _asyncParticipants = new(StringComparer.Ordinal);
        private readonly HashSet<string> _removedConsumersSinceStart = new(StringComparer.Ordinal);

        public int RequestId { get; } = requestId;
        public CaptureCommand Command { get; } = command;
        public TaskCompletionSource<bool> Completion { get; } = completion;
        public bool NotifyOnFailure { get; set; } = notifyOnFailure;
        public bool ForceReconcileOnFailure { get; } = forceReconcileOnFailure;
        public CaptureCommand PreviousTransportCommand { get; } = previousTransportCommand;
        public bool SubscriptionRemovedSinceStart { get; private set; }

        public void RegisterAsyncParticipant(
            string? consumerId,
            bool hadPreviousSubscription,
            bool previousCaptureMouse,
            bool previousCaptureKeyboard,
            bool shouldRestoreOnFailure = true)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
            {
                return;
            }

            if (_asyncParticipants.TryGetValue(consumerId, out var existing))
            {
                // Preserve the first restore-capable snapshot for a consumer so overlapping async
                // joins cannot downgrade rollback metadata for the original caller.
                if (existing.ShouldRestoreOnFailure)
                {
                    return;
                }

                if (!shouldRestoreOnFailure)
                {
                    return;
                }
            }

            _asyncParticipants[consumerId] = new PendingAsyncParticipantSnapshot(
                consumerId,
                hadPreviousSubscription,
                previousCaptureMouse,
                previousCaptureKeyboard,
                shouldRestoreOnFailure);
        }

        public PendingAsyncParticipantSnapshot[] GetAsyncParticipantsSnapshot()
        {
            var snapshots = new PendingAsyncParticipantSnapshot[_asyncParticipants.Count];
            var index = 0;
            foreach (var participant in _asyncParticipants.Values)
            {
                snapshots[index++] = participant;
            }

            return snapshots;
        }

        public string[] GetRemovedConsumerIdsSnapshot()
        {
            if (_removedConsumersSinceStart.Count == 0)
            {
                return [];
            }

            var removedConsumers = new string[_removedConsumersSinceStart.Count];
            _removedConsumersSinceStart.CopyTo(removedConsumers);
            return removedConsumers;
        }

        public void MarkSubscriptionRemovedSinceStart(string consumerId)
        {
            SubscriptionRemovedSinceStart = true;
            if (string.IsNullOrWhiteSpace(consumerId))
            {
                return;
            }

            _removedConsumersSinceStart.Add(consumerId);
        }
    }
}
