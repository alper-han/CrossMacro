namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Stable playback API; the session owns runtime state and native resource lifetime.</summary>
public sealed class MacroPlayer(IPlaybackValidator validator, MacroPlayerDependencies dependencies) : IMacroPlayer, IPlaybackPauseToken, IRunScriptRuntimeVariableSource
{
    private readonly MacroPlaybackSession _session = new(validator, dependencies);

    public bool IsPlaying => _session.IsPlaying;
    public bool IsPaused => _session.IsPaused;
    public int CurrentLoop => _session.CurrentLoop;
    public int TotalLoops => _session.TotalLoops;
    public bool IsWaitingBetweenLoops => _session.IsWaitingBetweenLoops;
    public IReadOnlyDictionary<string, string> RuntimeVariables => _session.RuntimeVariables;

    public Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default) =>
        _session.PlayAsync(macro, options, cancellationToken);

    public void Pause() => _session.Pause();
    public void ResumePlayback() => _session.ResumePlayback();
    public void StopPlayback() => _session.StopPlayback();
    public void Dispose() => _session.Dispose();

    Task IPlaybackPauseToken.WaitIfPausedAsync(CancellationToken cancellationToken) =>
        ((IPlaybackPauseToken)_session).WaitIfPausedAsync(cancellationToken);
}
