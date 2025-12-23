using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Core.Services;

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
    
    public PlaybackValidator(IMousePositionProvider? provider = null)
    {
        _provider = provider;
    }

    public ValidationResult Validate(MacroSequence macro)
    {
        var result = new ValidationResult();

        if (macro == null || macro.Events.Count == 0)
        {
            result.AddError("Macro is empty or null");
            return result;
        }

        if (OperatingSystem.IsLinux() && !CanAccessUInput())
        {
            result.AddError("/dev/uinput not accessible - check permissions");
        }

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

        return result;
    }

    private bool CanAccessUInput()
    {
        try
        {
            return File.Exists("/dev/uinput") || File.Exists("/dev/input/uinput");
        }
        catch
        {
            return false;
        }
    }
}
