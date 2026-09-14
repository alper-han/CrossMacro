namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Owns one document's playback cancellation and completion handshake.</summary>
internal sealed class EditorTestPlayback(IMacroPlayer player) : IDisposable
{
    private const int Running = 0;
    private const int StopRequested = 1;
    private const int Completed = 2;
    private readonly Lock _gate = new();
    private Session? _active;
    private bool _disposed;
    internal bool IsActive { get { lock (_gate) { return _active is not null; } } }

    internal async Task PlayAsync(MacroSequence macro, Func<Task> onStarted, Func<EditorTestPlaybackResult, Task> onCompleted)
    {
        Session session;
        lock (_gate)
        {
            if (_disposed || _active is not null) { return; }
            session = new Session();
            _active = session;
        }

        var result = new EditorTestPlaybackResult(EditorTestPlaybackOutcome.Completed);
        try
        {
            await onStarted().ConfigureAwait(false);
            session.Cancellation.Token.ThrowIfCancellationRequested();
            Task playback;
            lock (_gate)
            {
                if (_disposed || session.Cancellation.IsCancellationRequested)
                {
                    result = new(EditorTestPlaybackOutcome.Cancelled);
                    playback = Task.CompletedTask;
                }
                else
                {
                    session.PlaybackStarted = true;
                    playback = player.PlayAsync(macro, new PlaybackOptions { Loop = false, RepeatCount = 1 }, session.Cancellation.Token);
                }
            }
            await playback.ConfigureAwait(false);
            if (session.Cancellation.IsCancellationRequested) { result = new(EditorTestPlaybackOutcome.Cancelled); }
        }
        catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested || Volatile.Read(ref session.State) is StopRequested)
        { result = new(EditorTestPlaybackOutcome.Cancelled); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { result = new(EditorTestPlaybackOutcome.Failed, error.Message); }
        finally
        {
            if (Interlocked.Exchange(ref session.State, Completed) is StopRequested)
            { await session.StopCompleted.Task.ConfigureAwait(false); }
            try { await onCompleted(result).ConfigureAwait(false); }
            finally
            {
                lock (_gate) { if (ReferenceEquals(_active, session)) { _active = null; } }
                session.Cancellation.Dispose();
            }
        }
    }

    internal Task StopAsync()
    {
        Session? session;
        lock (_gate) { session = _active; }
        return session is null ? Task.CompletedTask : StopAsync(session);
    }

    private async Task StopAsync(Session session)
    {
        var previous = Interlocked.CompareExchange(ref session.State, StopRequested, Running);
        if (previous is not Running)
        {
            if (previous is StopRequested || Volatile.Read(ref session.StopWasRequested) is 1)
            { await session.StopCompleted.Task.ConfigureAwait(false); }
            return;
        }
        Volatile.Write(ref session.StopWasRequested, 1);
        try
        {
            await session.Cancellation.CancelAsync().ConfigureAwait(false);
            if (session.PlaybackStarted) { player.StopPlayback(); }
        }
        finally { _ = session.StopCompleted.TrySetResult(); }
    }

    public void Dispose()
    {
        Session? session;
        lock (_gate) { if (_disposed) { return; } _disposed = true; session = _active; }
        if (session is null || Interlocked.CompareExchange(ref session.State, StopRequested, Running) is not Running) { return; }
        Volatile.Write(ref session.StopWasRequested, 1);
        try { session.Cancellation.Cancel(); }
        finally
        {
            try { if (session.PlaybackStarted) { player.StopPlayback(); } }
            finally { _ = session.StopCompleted.TrySetResult(); }
        }
    }

    private sealed class Session
    {
        internal CancellationTokenSource Cancellation { get; } = new();
        internal TaskCompletionSource StopCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool PlaybackStarted;
        internal int State;
        internal int StopWasRequested;
    }
}
