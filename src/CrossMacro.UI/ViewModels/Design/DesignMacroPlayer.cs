
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignMacroPlayer : IMacroPlayer
{
    public bool IsPlaying { get; }
    public bool IsPaused { get; private set; }

    public int CurrentLoop { get; private set; }

    public int TotalLoops { get; private set; }

    public bool IsWaitingBetweenLoops { get; private set; }

    public event EventHandler? StateChanged;

    public Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        TotalLoops = (options?.Loop) is true ? Math.Max(1, options?.RepeatCount ?? 1) : 1;
        CurrentLoop = 1;
        IsPaused = false;
        IsWaitingBetweenLoops = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void StopPlayback()
    {
        CurrentLoop = 0;
        TotalLoops = 0;
        IsPaused = false;
        IsWaitingBetweenLoops = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        IsPaused = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResumePlayback()
    {
        IsPaused = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() { /* Empty */ }
}
