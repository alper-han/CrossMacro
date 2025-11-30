using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Wayland;

namespace CrossMacro.Core.Services;

/// <summary>
/// Validation result for playback pre-flight checks
/// </summary>
public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public void AddWarning(string message) => Warnings.Add(message);
    public void AddError(string message) => Errors.Add(message);
}

/// <summary>
/// Pre-flight validation for macro playback
/// </summary>
public class PlaybackValidator
{
    private readonly IMousePositionProvider? _provider;
    
    public PlaybackValidator(IMousePositionProvider? provider = null)
    {
        _provider = provider;
    }

    /// <summary>
    /// Validate macro before playback
    /// </summary>
    public ValidationResult Validate(MacroSequence macro)
    {
        var result = new ValidationResult();

        // Check 1: Empty sequence
        if (macro == null || macro.Events.Count == 0)
        {
            result.AddError("Macro is empty or null");
            return result; // Early return on critical error
        }

        // Check 2: uinput device permissions
        if (!CanAccessUInput())
        {
            result.AddError("/dev/uinput not accessible - check permissions");
        }

        // Check 3: Position provider availability
        if (_provider == null)
        {
            result.AddWarning("No position provider available - using fallback mode");
        }
        else if (!_provider.IsSupported)
        {
            result.AddWarning($"Position provider '{_provider.ProviderName}' is not supported on this system");
        }

        // Check 4: Suspicious delays
        var longDelays = macro.Events
            .Where(e => e.DelayMs > 10000) // > 10 seconds
            .ToList();
        
        if (longDelays.Any())
        {
            var maxDelay = longDelays.Max(e => e.DelayMs);
            result.AddWarning($"Macro contains {longDelays.Count} delay(s) > 10 seconds (max: {maxDelay / 1000f:F1}s)");
        }

        // Check 5: Very long macro
        if (macro.TotalDurationMs > 300000) // > 5 minutes
        {
            result.AddWarning($"Macro is very long ({macro.TotalDurationMs / 1000f / 60f:F1} minutes)");
        }

        // Check 6: Event count sanity
        if (macro.Events.Count > 10000)
        {
            result.AddWarning($"Macro has {macro.Events.Count} events - playback may be resource intensive");
        }

        return result;
    }

    private bool CanAccessUInput()
    {
        try
        {
            // Try both common paths
            return File.Exists("/dev/uinput") || File.Exists("/dev/input/uinput");
        }
        catch
        {
            return false;
        }
    }
}
