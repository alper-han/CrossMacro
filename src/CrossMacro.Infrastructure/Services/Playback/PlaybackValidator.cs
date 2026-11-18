
namespace CrossMacro.Infrastructure.Services.Playback;

public class PlaybackValidator : IPlaybackValidator
{
    private const int LongDelaySeconds = 10;
    private const int LongMacroDurationMs = 300_000;
    private const int LargeMacroEventCount = 10_000;
    private readonly IMousePositionProvider? _provider;
    private readonly PlaybackScriptValidator _scriptValidator;

    public PlaybackValidator(
        IKeyCodeMapper keyCodeMapper,
        IMousePositionProvider? provider = null,
        PlaybackScriptValidator? scriptValidator = null,
        IScriptValidationService? scriptValidationService = null)
    {
        ArgumentNullException.ThrowIfNull(keyCodeMapper);
        _provider = provider;
        _scriptValidator = scriptValidator ?? new PlaybackScriptValidator(keyCodeMapper, scriptValidationService);
    }

    public PlaybackValidationResult Validate(MacroSequence macro)
    {
        var result = new PlaybackValidationResult();

        if (macro is null || (macro.Events.Count is 0 && !HasRuntimeScriptSteps(macro)))
        {
            result.AddError("Macro is empty or null");
            return result;
        }

        var scan = EventScan.Of(macro);
        if (scan.HasNoneType)
        {
            result.AddWarning("Macro contains events with Type 'None'");
        }

        if (scan.HasUndefinedType)
        {
            result.AddError("Macro contains invalid/undefined EventType values");
        }

        if (scan.ButtonEventsWithoutButton > 0)
        {
            result.AddError($"Macro contains {scan.ButtonEventsWithoutButton.ToString(CultureInfo.InvariantCulture)} click/down/up event(s) without a mouse button");
        }

        ValidateScriptSteps(macro, result);

        if (_provider is null)
        {
            result.AddWarning("No position provider available - using fallback mode");
        }
        else if (!_provider.IsSupported)
        {
            result.AddWarning($"Position provider '{_provider.ProviderName}' is not supported on this system");
        }

        if (scan.LongDelayCount > 0)
        {
            result.AddWarning($"Macro contains {scan.LongDelayCount.ToString(CultureInfo.InvariantCulture)} delay(s) > {LongDelaySeconds.ToString(CultureInfo.InvariantCulture)} seconds (max: {(scan.MaxDelayMicroseconds / 1_000_000d).ToString("F1", CultureInfo.InvariantCulture)}s)");
        }

        if (macro.TotalDurationMs > LongMacroDurationMs)
        {
            result.AddWarning($"Macro is very long ({(macro.TotalDurationMs / 1000f / 60f).ToString("F1", CultureInfo.InvariantCulture)} minutes)");
        }

        if (macro.Events.Count > LargeMacroEventCount)
        {
            result.AddWarning($"Macro has {macro.Events.Count.ToString(CultureInfo.InvariantCulture)} events - playback may be resource intensive");
        }

        if (scan.HasSuspiciousAbsoluteButtonCoordinate)
        {
            result.AddWarning(
                "Absolute macro contains click/down/up event(s) at (0,0); this may cause cursor jumps to top-left.");
        }

        return result;
    }

    private static bool HasRuntimeScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(RunScriptRuntimeStepClassifier.IsRuntimeStep);
    }

    private void ValidateScriptSteps(MacroSequence macro, PlaybackValidationResult result)
    {
        var error = _scriptValidator.Validate(macro);
        if (error is not null)
        {
            result.AddError(error);
        }
    }

    /// <summary>Everything validation needs to know about the events, gathered in one pass.</summary>
    private readonly record struct EventScan(
        bool HasNoneType,
        bool HasUndefinedType,
        int ButtonEventsWithoutButton,
        int LongDelayCount,
        long MaxDelayMicroseconds,
        bool HasSuspiciousAbsoluteButtonCoordinate)
    {
        public static EventScan Of(MacroSequence macro)
        {
            const long LongDelayMicroseconds = LongDelaySeconds * MacroTiming.MicrosecondsPerMillisecond * 1000;
            bool hasNoneType = false;
            bool hasUndefinedType = false;
            int buttonEventsWithoutButton = 0;
            int longDelayCount = 0;
            long maxDelayMicroseconds = 0;
            bool hasZeroZeroAbsoluteButton = false;
            bool hasNonZeroAbsoluteButton = false;
            bool hasNonZeroMouseMove = false;

            var events = macro.Events;
            for (int index = 0; index < events.Count; index++)
            {
                var ev = events[index];
                hasNoneType |= ev.Type is EventType.None;
                hasUndefinedType |= !Enum.IsDefined(ev.Type);
                if (ev.DelayMicroseconds > LongDelayMicroseconds)
                {
                    longDelayCount++;
                    maxDelayMicroseconds = Math.Max(maxDelayMicroseconds, ev.DelayMicroseconds);
                }

                if (ev.Type is EventType.MouseMove)
                {
                    hasNonZeroMouseMove |= ev.X is not 0 || ev.Y is not 0;
                    continue;
                }

                if (ev.Type is not EventType.ButtonPress and not EventType.ButtonRelease and not EventType.Click
                    || MacroPositionSemantics.IsScrollButton(ev.Button))
                {
                    continue;
                }

                if (ev.Button is MacroMouseButton.None)
                {
                    buttonEventsWithoutButton++;
                }

                if (MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateMode.Absolute)
                {
                    if (ev.X is 0 && ev.Y is 0)
                    {
                        hasZeroZeroAbsoluteButton = true;
                    }
                    else
                    {
                        hasNonZeroAbsoluteButton = true;
                    }
                }
            }

            return new EventScan(
                hasNoneType,
                hasUndefinedType,
                buttonEventsWithoutButton,
                longDelayCount,
                maxDelayMicroseconds,
                hasZeroZeroAbsoluteButton && (hasNonZeroAbsoluteButton || hasNonZeroMouseMove));
        }
    }
}
