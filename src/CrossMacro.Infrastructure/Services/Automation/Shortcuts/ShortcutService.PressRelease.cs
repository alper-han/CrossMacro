namespace CrossMacro.Infrastructure.Services.Automation.Shortcuts;

public sealed partial class ShortcutService
{
    private readonly Dictionary<Guid, PressReleaseSession> _pressedShortcuts = new();
    private readonly Dictionary<Guid, PressReleaseSession> _pressReleaseTails = new();
    private readonly HashSet<PressReleaseSession> _pressReleaseSessions = [];

    private IReadOnlyList<PressReleaseSession> RegisterPressReleaseSessions(
        IReadOnlyList<ShortcutCandidate> candidates, RawHotkeyInputEventArgs input)
    {
        List<PressReleaseSession>? sessions = null;
        lock (_lock)
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                var task = candidates[index].Task;
                if (_disposed || !IsListening || !Tasks.Contains(task) || !task.IsEnabled
                    || !task.RunOnPressAndRelease || _pressedShortcuts.ContainsKey(task.Id))
                {
                    continue;
                }

                var session = CreatePressReleaseSession(task, CancellationToken.None);
                session.Keys.UnionWith(input.PressedModifiers);
                _ = session.Keys.Add(input.KeyCode);
                _pressedShortcuts[task.Id] = session;
                (sessions ??= []).Add(session);
            }
        }
        return sessions ?? (IReadOnlyList<PressReleaseSession>)[];
    }

    // Called under _lock. Register before asynchronous window matching so a quick
    // release and subsequent gesture cannot overtake the first gesture.
    private PressReleaseSession CreatePressReleaseSession(ShortcutTask task, CancellationToken cancellationToken)
    {
        _ = _pressReleaseTails.TryGetValue(task.Id, out var previous);
        var session = new PressReleaseSession(task, previous?.Completion.Task ?? Task.CompletedTask, cancellationToken);
        _pressReleaseTails[task.Id] = session;
        _ = _pressReleaseSessions.Add(session);
        return session;
    }

    private Task? ResolvePressReleaseSessionsAsync(IReadOnlyList<PressReleaseSession> sessions, ShortcutTask? selected)
    {
        Task? execution = null;
        foreach (var session in sessions)
        {
            var accepted = ReferenceEquals(session.Task, selected);
            var pending = ExecutePressReleaseSessionAsync(session, accepted);
            if (accepted)
            {
                execution = pending;
            }
        }
        return execution;
    }

    private async Task ExecutePressReleaseSessionAsync(PressReleaseSession session, bool accepted)
    {
        try
        {
            var token = session.Cancellation.Token;
            await session.PreviousExecution.WaitAsync(token).ConfigureAwait(false);
            if (!accepted)
            {
                return;
            }

            token.ThrowIfCancellationRequested();
            lock (_lock)
            {
                if (!Tasks.Contains(session.Task) || !session.Task.RunOnPressAndRelease)
                {
                    return;
                }
            }

            var succeeded = await ExecuteTaskAsync(session.Task,
                cancellationToken: token, macroFilePath: session.PressPath, forceSingleShot: true).ConfigureAwait(false);
            if (!succeeded)
            {
                return;
            }

            await session.Released.Task.WaitAsync(token).ConfigureAwait(false);
            _ = await ExecuteTaskAsync(session.Task,
                cancellationToken: token, macroFilePath: session.ReleasePath, forceSingleShot: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested)
        {
            // Disabling, deleting, editing or changing profiles cancels the gesture.
        }
        finally
        {
            lock (_lock)
            {
                _ = _pressReleaseSessions.Remove(session);
            }
            _ = CompletePressReleaseSessionAsync(session);
            session.Cancellation.Dispose();
        }
    }

    private async Task CompletePressReleaseSessionAsync(PressReleaseSession session)
    {
        // Canceling this run must not let successors overtake its unfinished predecessor.
        await session.PreviousExecution.ConfigureAwait(false);
        lock (_lock)
        {
            if (_pressReleaseTails.TryGetValue(session.Task.Id, out var tail) && ReferenceEquals(tail, session))
            {
                _ = _pressReleaseTails.Remove(session.Task.Id);
            }
        }
        _ = session.Completion.TrySetResult();
    }

    private void ReleasePressReleaseSessions(int keyCode)
    {
        lock (_lock)
        {
            // Physical admission stays latched even after matching or playback fails.
            foreach (var (taskId, session) in _pressedShortcuts)
            {
                if (!session.Keys.Contains(keyCode))
                {
                    continue;
                }
                _ = _pressedShortcuts.Remove(taskId);
                _ = session.Released.TrySetResult();
            }
        }
    }

    // Called under _lock. Do not synthesize release actions after configuration
    // changes or shutdown: pending gestures belong to the old configuration.
    private void CancelPressReleaseSessions(Guid? taskId = null)
    {
        // Cancellation can complete a session and remove it from the set.
        var sessions = _pressReleaseSessions.ToArray();
        for (var index = 0; index < sessions.Length; index++)
        {
            var session = sessions[index];
            if (taskId is null || session.Task.Id == taskId)
            {
                session.Cancellation.Cancel();
            }
        }
        if (taskId is { } id)
        {
            _ = _pressedShortcuts.Remove(id);
            _ = _pressReleaseTails.Remove(id);
        }
        else
        {
            _pressedShortcuts.Clear();
            _pressReleaseTails.Clear();
        }
    }

    private sealed class PressReleaseSession(ShortcutTask task, Task previousExecution, CancellationToken cancellationToken)
    {
        public ShortcutTask Task { get; } = task;
        public string PressPath { get; } = task.MacroFilePath;
        public string ReleasePath { get; } = task.ReleaseMacroFilePath;
        public Task PreviousExecution { get; } = previousExecution;
        public HashSet<int> Keys { get; } = [];
        public CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
