using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public void AddWarning(string message) => Warnings.Add(message);
    public void AddError(string message) => Errors.Add(message);
}

public class PlaybackValidator
{
    private readonly IMousePositionProvider? _provider;
    private readonly IKeyCodeMapper _keyCodeMapper;

    public PlaybackValidator(IKeyCodeMapper keyCodeMapper, IMousePositionProvider? provider = null)
    {
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        _provider = provider;
    }

    public ValidationResult Validate(MacroSequence macro)
    {
        var result = new ValidationResult();

        if (macro == null || (macro.Events.Count == 0 && !HasRuntimeScriptSteps(macro)))
        {
            result.AddError("Macro is empty or null");
            return result;
        }

        if (macro.Events.Any(e => e.Type == EventType.None && !IsSpecialControlEvent(e)))
        {
            result.AddWarning("Macro contains events with Type 'None'");
        }

        if (macro.Events.Any(e => !Enum.IsDefined(typeof(EventType), e.Type)))
        {
            result.AddError("Macro contains invalid/undefined EventType values");
        }

        ValidateScriptSteps(macro, result);


        if (_provider == null)
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
                && MacroPositionSemantics.ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) == MouseCoordinateMode.Absolute)
            .ToList();
        if (buttonEvents.Count == 0)
        {
            return;
        }

        bool hasZeroZeroButtonEvent = buttonEvents.Any(e => e.X == 0 && e.Y == 0);
        if (!hasZeroZeroButtonEvent)
        {
            return;
        }

        bool hasNonZeroButtonEvent = buttonEvents.Any(e => e.X != 0 || e.Y != 0);
        bool hasNonZeroMouseMove = macro.Events.Any(e =>
            e.Type == EventType.MouseMove
            && (e.X != 0 || e.Y != 0));

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
        return macro.ScriptSteps.Any(s =>
            RunScriptSyntax.IsScreenReadingStep(s) || RunScriptSyntax.IsWindowStep(s));
    }

    private void ValidateScriptSteps(MacroSequence macro, ValidationResult result)
    {
        var scriptSteps = macro.ScriptSteps
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select((step, index) => new RunScriptStep(step, SourceIndex: index))
            .ToList();
        if (scriptSteps.Count == 0)
        {
            return;
        }

        var compiler = new RunScriptCompiler(_keyCodeMapper);
        var compileResult = compiler.Compile(scriptSteps);
        if (!compileResult.Success)
        {
            result.AddError($"Macro script steps are invalid: {compileResult.ErrorMessage}");
        }
    }

}
