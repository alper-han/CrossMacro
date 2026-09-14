namespace CrossMacro.UI.ViewModels.Playback;

/// <summary>Owns one accepted playback request from approval through player settlement.</summary>
internal sealed class PlaybackSession : IDisposable
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private bool _playerStarted;
    private bool _stopped;
    private bool _disposed;

    internal CancellationToken Token => _cancellation.Token;
    internal bool IsStopRequested { get { lock (_gate) { return _stopped; } } }

    internal Task PlayAsync(IMacroPlayer player, MacroSequence macro, PlaybackOptions options)
    {
        lock (_gate)
        {
            _cancellation.Token.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            _playerStarted = true;
            return player.PlayAsync(macro, options, _cancellation.Token);
        }
    }

    internal void RequestStop(IMacroPlayer player)
    {
        lock (_gate)
        {
            if (_stopped || _disposed) { return; }
            _stopped = true;
            try { _cancellation.Cancel(); }
            finally { if (_playerStarted) { player.StopPlayback(); } }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            _disposed = true;
            _cancellation.Dispose();
        }
    }
}
