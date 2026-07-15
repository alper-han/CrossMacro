
namespace CrossMacro.Infrastructure.Services;

public class PlaybackValidator : IPlaybackValidator
{
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

    public ValidationResult Validate(MacroSequence macro)
    {
        var result = new ValidationResult();

        if (macro is null || (macro.Events.Count is 0 && !HasRuntimeScriptSteps(macro)))
        {
            result.AddError("Macro is empty or null");
            return result;
        }

        if (macro.Events.Any(e => e.Type is EventType.None && !IsSpecialControlEvent(e)))
        {
            result.AddWarning("Macro contains events with Type 'None'");
        }

        if (macro.Events.Any(e => !Enum.IsDefined(typeof(EventType), e.Type)))
        {
            result.AddError("Macro contains invalid/undefined EventType values");
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

        var longDelays = macro.Events
            .Where(e => e.DelayMs > 10000)
            .ToList();

        if (longDelays.Any())
        {
            var maxDelay = longDelays.Max(e => e.DelayMs);
            result.AddWarning($"Macro contains {longDelays.Count} delay(s) > 10 seconds (max: {maxDelay / 1000f:F1}s)");
        }

        if (macro.TotalDurationMs > 300000)
        {
            result.AddWarning($"Macro is very long ({macro.TotalDurationMs / 1000f / 60f:F1} minutes)");
        }

        if (macro.Events.Count > 10000)
        {
            result.AddWarning($"Macro has {macro.Events.Count} events - playback may be resource intensive");
        }

        AddSuspiciousAbsoluteButtonCoordinateWarning(macro, result);

        return result;
    }



    private bool IsSpecialControlEvent(MacroEvent e)
    {
        return false;
    }

    private static void AddSuspiciousAbsoluteButtonCoordinateWarning(MacroSequence macro, ValidationResult result)
    {
        var buttonEvents = macro.Events
            .Where(ev => IsNonScrollButtonEvent(ev)
&& MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateMode.Absolute)
            .ToList();
        if (buttonEvents.Count is 0)
        {
            return;
        }

        bool hasZeroZeroButtonEvent = buttonEvents.Any(e => e.X is 0 && e.Y is 0);
        if (!hasZeroZeroButtonEvent)
        {
            return;
        }

        bool hasNonZeroButtonEvent = buttonEvents.Any(e => e.X is not 0 || e.Y is not 0);
        bool hasNonZeroMouseMove = macro.Events.Any(e =>
            e.Type is EventType.MouseMove
&& (e.X is not 0 || e.Y is not 0));

        if (hasNonZeroButtonEvent || hasNonZeroMouseMove)
        {
            result.AddWarning(
                "Absolute macro contains click/down/up event(s) at (0,0); this may cause cursor jumps to top-left.");
        }
    }

    private static bool IsNonScrollButtonEvent(MacroEvent ev)
    {
        if (ev.Type is not EventType.ButtonPress and not EventType.ButtonRelease and not EventType.Click)
        {
            return false;
        }

        return ev.Button is not MouseButton.ScrollUp
            and not MouseButton.ScrollDown
            and not MouseButton.ScrollLeft
            and not MouseButton.ScrollRight;
    }

    private static bool HasRuntimeScriptSteps(MacroSequence macro)
    {
        return macro.ScriptSteps.Any(RunScriptRuntimeStepClassifier.IsRuntimeStep);
    }

    private void ValidateScriptSteps(MacroSequence macro, ValidationResult result)
    {
        var error = _scriptValidator.Validate(macro);
        if (error is not null)
        {
            result.AddError(error);
        }
    }

}
