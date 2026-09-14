namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Owns elapsed scheduling, pause accounting and drift recovery for one playback iteration.</summary>
internal sealed class PlaybackTimeline(
    PlaybackSessionResourceOwner session,
    IPlaybackTimingService timingService,
    IPlaybackPauseToken pauseToken,
    Func<MacroEvent, double> resolveDelay)
{
    private const double MinCatchUpResetDriftMs = 30.0;
    private const double CatchUpResetDelayMultiplier = 2.0;
    private double _scheduledElapsedMs;
    private double _timelineAnchorElapsedMs;
    private bool _hasTimelineAnchor;
    private int _observedPauseResumeVersion = session.PauseResumeVersion;

    public int EventCount { get; set; }

    public void Reanchor(double elapsedMilliseconds)
    {
        _timelineAnchorElapsedMs = elapsedMilliseconds;
        _scheduledElapsedMs = 0;
        _hasTimelineAnchor = true;
    }

    public async Task WaitAsync(MacroEvent ev, double speedMultiplier,
        Func<double> playbackElapsedMilliseconds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (session.IsPaused)
        {
            Log.Debug("[MacroPlayer] Paused before event {Current}", EventCount);
            var pausedStartMs = playbackElapsedMilliseconds();
            await session.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            var pausedDurationMs = playbackElapsedMilliseconds() - pausedStartMs;
            if (_hasTimelineAnchor)
            {
                _timelineAnchorElapsedMs += pausedDurationMs;
            }
        }

        int currentPauseResumeVersion = session.PauseResumeVersion;
        if (currentPauseResumeVersion != _observedPauseResumeVersion)
        {
            _observedPauseResumeVersion = currentPauseResumeVersion;
            if (_hasTimelineAnchor)
            {
                _scheduledElapsedMs = playbackElapsedMilliseconds() - _timelineAnchorElapsedMs;
            }
        }

        EventCount++;

        double eventDelaySource = resolveDelay(ev);
        bool isMotionEvent = ev.Type is EventType.MouseMove;
        if (eventDelaySource <= 0)
        {
            return;
        }

        double adjustedDelay = eventDelaySource / speedMultiplier;
        if (!_hasTimelineAnchor)
        {
            _timelineAnchorElapsedMs = playbackElapsedMilliseconds();
            _hasTimelineAnchor = true;
        }

        _scheduledElapsedMs += adjustedDelay;
        var elapsedSinceAnchorMs = playbackElapsedMilliseconds() - _timelineAnchorElapsedMs;
        var remainingDelayMs = _scheduledElapsedMs - elapsedSinceAnchorMs;
        if (remainingDelayMs > 0)
        {
            await timingService.WaitAsync(remainingDelayMs, pauseToken, cancellationToken).ConfigureAwait(false);
            elapsedSinceAnchorMs = playbackElapsedMilliseconds() - _timelineAnchorElapsedMs;
            remainingDelayMs = _scheduledElapsedMs - elapsedSinceAnchorMs;
        }

        if (remainingDelayMs <= 0
            && (isMotionEvent || ShouldResetPlaybackTimeline(remainingDelayMs, adjustedDelay)))
        {
            _scheduledElapsedMs = elapsedSinceAnchorMs;
        }
    }

    private static bool ShouldResetPlaybackTimeline(double remainingDelayMs, double adjustedDelayMs)
    {
        double allowedDriftMs = Math.Max(MinCatchUpResetDriftMs, adjustedDelayMs * CatchUpResetDelayMultiplier);
        return remainingDelayMs <= -allowedDriftMs;
    }
}
