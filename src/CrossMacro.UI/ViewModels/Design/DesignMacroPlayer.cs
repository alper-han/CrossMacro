
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignMacroPlayer : IMacroPlayer
{
    public bool IsPlaying { get; }
    public bool IsPaused { get; private set; }

    public int CurrentLoop { get; private set; }

    public int TotalLoops { get; private set; }

    public bool IsWaitingBetweenLoops { get; private set; }

    public Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        TotalLoops = (options?.Loop) is true ? Math.Max(1, options?.RepeatCount ?? 1) : 1;
        CurrentLoop = 1;
        IsPaused = false;
        IsWaitingBetweenLoops = false;
        return Task.CompletedTask;
    }

    public void StopPlayback()
    {
        CurrentLoop = 0;
        TotalLoops = 0;
        IsPaused = false;
        IsWaitingBetweenLoops = false;
    }

    public void Pause() => IsPaused = true;

    public void ResumePlayback() => IsPaused = false;

    public void Dispose() { /* Empty */ }
}
