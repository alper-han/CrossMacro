using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Handles saving and loading macro sequences from files
/// </summary>
public class MacroFileManager : IMacroFileManager
{
    private const string TrailingDelayHeader = "# TrailingDelayMs: ";
    private const string TrailingRandomDelayHeader = "# TrailingRandomDelayMs: ";
    private const string TextInputBoundaryHeader = "# TextInputBoundaryBase64: ";
    private const string ReadableFormatHeader = "# Format: CrossMacroFormatV2";
    private const string ScriptSectionHeader = "[Script]";
    private const string EventsSectionHeader = "[Events]";
    private const string ScriptContinuationPrefix = "| ";
    private readonly Func<IKeyCodeMapper> _keyCodeMapperFactory;

    private enum MacroFileReadSection
    {
        Header,
        Script,
        Events
    }

    public MacroFileManager(Func<IKeyCodeMapper> keyCodeMapperFactory)
    {
        _keyCodeMapperFactory = keyCodeMapperFactory ?? throw new ArgumentNullException(nameof(keyCodeMapperFactory));
    }
    
    /// <summary>
    /// Saves a macro sequence to a custom text file (.macro)
    /// </summary>
    public async Task SaveAsync(MacroSequence macro, string filePath)
    {
        if (macro == null)
            throw new ArgumentNullException(nameof(macro));
            
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        
        if (!macro.IsValid())
            throw new InvalidOperationException("Cannot save invalid macro sequence");

        ValidateScriptStepsBeforeSave(macro);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        using var writer = new StreamWriter(filePath);
        
        // Write Header
        // TODO: Deprecate # IsAbsolute once all supported .macro files carry explicit per-event coordinate mode tokens.
        // It must remain for now as the legacy fallback for old token-less coordinate events.
        var legacyHeaderIsAbsolute = GetLegacyHeaderCoordinateMode(macro);
        await writer.WriteLineAsync($"# Name: {macro.Name}");
        await writer.WriteLineAsync($"# Created: {macro.CreatedAt:O}");
        await writer.WriteLineAsync($"# DurationMs: {macro.TotalDurationMs}");
        await writer.WriteLineAsync($"# IsAbsolute: {legacyHeaderIsAbsolute}");
        await writer.WriteLineAsync($"# SkipInitialZero: {macro.SkipInitialZeroZero}");
        if (macro.TrailingDelayMs > 0)
        {
            await writer.WriteLineAsync($"{TrailingDelayHeader}{macro.TrailingDelayMs}");
        }
        if (macro.HasTrailingRandomDelay)
        {
            await writer.WriteLineAsync($"{TrailingRandomDelayHeader}{macro.TrailingDelayMinMs},{macro.TrailingDelayMaxMs}");
        }
        foreach (var boundary in macro.TextInputBoundaries)
        {
            if (boundary.EventCount <= 0 || boundary.StartEventIndex < 0)
            {
                continue;
            }

            var json = JsonSerializer.Serialize(boundary, MacroFileJsonContext.Default.TextInputBoundary);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await writer.WriteLineAsync($"{TextInputBoundaryHeader}{encoded}");
        }

        await writer.WriteLineAsync(ReadableFormatHeader);
        await writer.WriteLineAsync(ScriptSectionHeader);
        foreach (var scriptStep in macro.ScriptSteps)
        {
            if (string.IsNullOrWhiteSpace(scriptStep))
            {
                continue;
            }

            await WriteScriptStepAsync(writer, scriptStep);
        }

        await writer.WriteLineAsync(EventsSectionHeader);
        
        // Write Events
        foreach (var ev in macro.Events)
        {
            // Write Delay as separate line if > 0
            if (ev.DelayMs > 0)
            {
                await writer.WriteLineAsync($"W,{ev.DelayMs}");
            }
            if (ev.HasRandomDelay)
            {
                await writer.WriteLineAsync($"WR,{ev.RandomDelayMinMs},{ev.RandomDelayMaxMs}");
            }

            switch (ev.Type)
            {
                case EventType.MouseMove:
                    await writer.WriteLineAsync(BuildMouseMoveLine(ev));
                    break;
                    
                case EventType.ButtonPress:
                    // Format: P,X,Y,Button
                    await writer.WriteLineAsync(BuildMouseButtonLine("P", ev));
                    break;
                    
                case EventType.ButtonRelease:
                    // Format: R,X,Y,Button
                    await writer.WriteLineAsync(BuildMouseButtonLine("R", ev));
                    break;
                    
                case EventType.Click:
                    // Format: C,X,Y,Button (Used for Scroll)
                    await writer.WriteLineAsync(BuildMouseButtonLine("C", ev));
                    break;
                    
                case EventType.KeyPress:
                    // Format: KP,KeyCode
                    await writer.WriteLineAsync($"KP,{ev.KeyCode}");
                    break;
                    
                case EventType.KeyRelease:
                    // Format: KR,KeyCode
                    await writer.WriteLineAsync($"KR,{ev.KeyCode}");
                    break;
            }
        }
    }

    private static string BuildMouseButtonLine(string command, MacroEvent ev)
    {
        if (ev.UseCurrentPosition)
        {
            return $"{command},{ev.X},{ev.Y},{ev.Button},CurrentPosition";
        }

        if (MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev) && ev.CoordinateMode.HasValue)
        {
            return $"{command},{ToCoordinateModeToken(ev.CoordinateMode.Value)},{ev.X},{ev.Y},{ev.Button}";
        }

        return $"{command},{ev.X},{ev.Y},{ev.Button}";
    }

    private static string BuildMouseMoveLine(MacroEvent ev)
    {
        return ev.CoordinateMode.HasValue
            ? $"M,{ToCoordinateModeToken(ev.CoordinateMode.Value)},{ev.X},{ev.Y}"
            : $"M,{ev.X},{ev.Y}";
    }

    private static bool GetLegacyHeaderCoordinateMode(MacroSequence macro)
    {
        if (macro.Events.Any(ev => MacroPositionSemantics.IsCoordinateBearing(ev)
            && !MacroPositionSemantics.HasExplicitCoordinateMode(ev)))
        {
            return macro.IsAbsoluteCoordinates;
        }

        // Event-level coordinate mode wins; the header remains legacy/default metadata.
        foreach (var ev in macro.Events)
        {
            if (MacroPositionSemantics.HasExplicitCoordinateMode(ev))
            {
                return ev.CoordinateMode == MouseCoordinateMode.Absolute;
            }
        }

        return macro.IsAbsoluteCoordinates;
    }

    private static string ToCoordinateModeToken(MouseCoordinateMode mode)
    {
        return mode == MouseCoordinateMode.Absolute ? "abs" : "rel";
    }

    private void ValidateScriptStepsBeforeSave(MacroSequence macro)
    {
        if (macro.ScriptSteps == null)
        {
            return;
        }

        var steps = new List<RunScriptStep>(macro.ScriptSteps.Count);
        for (var index = 0; index < macro.ScriptSteps.Count; index++)
        {
            var step = macro.ScriptSteps[index];
            if (string.IsNullOrWhiteSpace(step))
            {
                continue;
            }

            steps.Add(new RunScriptStep(step, SourceIndex: index));
        }

        if (steps.Count == 0)
        {
            return;
        }

        var compiler = new RunScriptCompiler(_keyCodeMapperFactory());
        var result = compiler.Compile(steps);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Cannot save invalid macro script steps: {result.ErrorMessage}");
        }
    }

    private static async Task WriteScriptStepAsync(TextWriter writer, string scriptStep)
    {
        var normalized = scriptStep.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        await writer.WriteLineAsync(lines[0]);
        for (var index = 1; index < lines.Length; index++)
        {
            await writer.WriteLineAsync($"{ScriptContinuationPrefix}{lines[index]}");
        }
    }

    /// <summary>
    /// Loads a macro sequence from a custom text file (.macro)
    /// </summary>
    public async Task<MacroSequence?> LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Macro file not found", filePath);
        
        var macro = new MacroSequence();
        var lines = await File.ReadAllLinesAsync(filePath);
        
        int currentDelay = 0;
        bool currentHasRandomDelay = false;
        int currentRandomDelayMinMs = 0;
        int currentRandomDelayMaxMs = 0;
        var section = MacroFileReadSection.Header;
        string? pendingScriptStep = null;

        void CommitPendingScriptStep()
        {
            if (!string.IsNullOrWhiteSpace(pendingScriptStep))
            {
                macro.ScriptSteps.Add(pendingScriptStep);
            }

            pendingScriptStep = null;
        }
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (string.Equals(trimmed, ScriptSectionHeader, StringComparison.Ordinal))
            {
                CommitPendingScriptStep();
                section = MacroFileReadSection.Script;
                continue;
            }

            if (string.Equals(trimmed, EventsSectionHeader, StringComparison.Ordinal))
            {
                CommitPendingScriptStep();
                section = MacroFileReadSection.Events;
                continue;
            }

            if (section == MacroFileReadSection.Script)
            {
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith(ScriptContinuationPrefix, StringComparison.Ordinal))
                {
                    if (pendingScriptStep is null)
                    {
                        Log.Warning("Ignoring orphan script continuation line: {Line}", line);
                        continue;
                    }

                    pendingScriptStep += "\n" + line[ScriptContinuationPrefix.Length..];
                    continue;
                }

                CommitPendingScriptStep();
                pendingScriptStep = line;
                continue;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                if (section != MacroFileReadSection.Header)
                {
                    continue;
                }

                // Parse Header
                if (line.StartsWith("# Name: "))
                    macro.Name = line.Substring(8).Trim();
                else if (line.StartsWith("# Created: ") && DateTime.TryParse(line.Substring(11).Trim(), out var date))
                    macro.CreatedAt = date;
                else if (line.StartsWith("# DurationMs: ") && long.TryParse(line.Substring(14).Trim(), out var duration))
                    macro.TotalDurationMs = duration;
                else if (line.StartsWith("# IsAbsolute: ") && bool.TryParse(line.Substring(14).Trim(), out var isAbs))
                    macro.IsAbsoluteCoordinates = isAbs;
                else if (line.StartsWith("# SkipInitialZero: ") && bool.TryParse(line.Substring(19).Trim(), out var skipZero))
                    macro.SkipInitialZeroZero = skipZero;
                else if (line.StartsWith(TrailingDelayHeader, StringComparison.Ordinal)
                    && int.TryParse(line.Substring(TrailingDelayHeader.Length).Trim(), out var trailingDelay))
                    macro.TrailingDelayMs = trailingDelay;
                else if (line.StartsWith(TrailingRandomDelayHeader, StringComparison.Ordinal))
                {
                    var trailingRandomParts = line.Substring(TrailingRandomDelayHeader.Length).Trim().Split(',');
                    if (trailingRandomParts.Length >= 2
                        && int.TryParse(trailingRandomParts[0], out var trailingRandomMin)
                        && int.TryParse(trailingRandomParts[1], out var trailingRandomMax))
                    {
                        macro.HasTrailingRandomDelay = true;
                        macro.TrailingDelayMinMs = trailingRandomMin;
                        macro.TrailingDelayMaxMs = trailingRandomMax;
                    }
                }
                else if (line.StartsWith(TextInputBoundaryHeader, StringComparison.Ordinal))
                {
                    var encoded = line.Substring(TextInputBoundaryHeader.Length).Trim();
                    if (encoded.Length > 0)
                    {
                        try
                        {
                            var boundaryBytes = Convert.FromBase64String(encoded);
                            var boundaryJson = Encoding.UTF8.GetString(boundaryBytes);
                            var boundary = JsonSerializer.Deserialize(boundaryJson, MacroFileJsonContext.Default.TextInputBoundary);
                            if (boundary is { StartEventIndex: >= 0, EventCount: > 0 })
                            {
                                macro.TextInputBoundaries.Add(boundary);
                            }
                        }
                        catch (Exception ex) when (ex is FormatException or JsonException)
                        {
                            Log.Warning(ex, "Ignoring malformed text input boundary metadata");
                        }
                    }
                }
                
                continue;
            }

            if (section == MacroFileReadSection.Script)
            {
                continue;
            }
            
            // Parse Event
            var parts = line.Split(',');
            if (parts.Length == 0) continue;
            
            string type = parts[0].ToUpperInvariant();
            
            // Handle Wait
            if ((type == "W" || type == "WAIT") && parts.Length >= 2)
            {
                if (int.TryParse(parts[1], out int delay))
                {
                    currentDelay += delay;
                }
                continue;
            }
            if ((type == "WR" || type == "WAITRANDOM") && parts.Length >= 3)
            {
                if (int.TryParse(parts[1], out int randomMinDelay) && int.TryParse(parts[2], out int randomMaxDelay))
                {
                    currentHasRandomDelay = true;
                    currentRandomDelayMinMs += randomMinDelay;
                    currentRandomDelayMaxMs += randomMaxDelay;
                }
                continue;
            }
            
            try
            {
                var ev = new MacroEvent
                {
                    DelayMs = currentDelay,
                    HasRandomDelay = currentHasRandomDelay,
                    RandomDelayMinMs = currentRandomDelayMinMs,
                    RandomDelayMaxMs = currentRandomDelayMaxMs
                };
                bool validEvent = false;

                // Handle Move
                if ((type == "M" || type == "MOVE") && parts.Length >= 3)
                {
                    var coordinateIndex = 1;
                    if (!int.TryParse(parts[coordinateIndex], out var x))
                    {
                        if (parts.Length < 4 || !TryParseCoordinateMode(parts[coordinateIndex], out var mode))
                        {
                            throw new FormatException($"Invalid coordinate mode token '{parts[coordinateIndex]}'");
                        }

                        ev.CoordinateMode = mode;
                        coordinateIndex++;
                        x = int.Parse(parts[coordinateIndex]);
                    }

                    ev.Type = EventType.MouseMove;
                    ev.X = x;
                    ev.Y = int.Parse(parts[coordinateIndex + 1]);
                    ev.Button = MouseButton.None;
                    validEvent = true;
                }
                // Handle Button Events
                else if ((type == "P" || type == "PRESS" || 
                          type == "R" || type == "RELEASE" || 
                          type == "C" || type == "CLICK") && parts.Length >= 4)
                {
                    var coordinateIndex = 1;
                    MouseCoordinateMode? coordinateMode = null;
                    if (!int.TryParse(parts[coordinateIndex], out var x))
                    {
                        if (parts.Length < 5 || !TryParseCoordinateMode(parts[coordinateIndex], out var mode))
                        {
                            throw new FormatException($"Invalid coordinate mode token '{parts[coordinateIndex]}'");
                        }

                        coordinateMode = mode;
                        coordinateIndex++;
                        x = int.Parse(parts[coordinateIndex]);
                    }

                    ev.Type = type switch 
                    {
                        "P" or "PRESS" => EventType.ButtonPress,
                        "R" or "RELEASE" => EventType.ButtonRelease,
                        "C" or "CLICK" => EventType.Click,
                        _ => EventType.Click
                    };
                    ev.X = x;
                    ev.Y = int.Parse(parts[coordinateIndex + 1]);
                    ev.Button = Enum.Parse<MouseButton>(parts[coordinateIndex + 2]);
                    ev.UseCurrentPosition = parts.Length > coordinateIndex + 3 && IsCurrentPositionToken(parts[coordinateIndex + 3]);
                    if (!ev.UseCurrentPosition && MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev))
                    {
                        ev.CoordinateMode = coordinateMode;
                    }

                    validEvent = true;
                }
                // Handle Keyboard Events
                else if ((type == "KP" || type == "KEYPRESS") && parts.Length >= 2)
                {
                    ev.Type = EventType.KeyPress;
                    ev.KeyCode = int.Parse(parts[1]);
                    ev.Button = MouseButton.None;
                    ev.X = 0;
                    ev.Y = 0;
                    validEvent = true;
                }
                else if ((type == "KR" || type == "KEYRELEASE") && parts.Length >= 2)
                {
                    ev.Type = EventType.KeyRelease;
                    ev.KeyCode = int.Parse(parts[1]);
                    ev.Button = MouseButton.None;
                    ev.X = 0;
                    ev.Y = 0;
                    validEvent = true;
                }
                
                if (validEvent)
                {
                    // Reconstruct timestamp
                    if (macro.Events.Count > 0)
                    {
                        ev.Timestamp = macro.Events[^1].Timestamp + ev.DelayMs;
                        if (ev.HasRandomDelay)
                        {
                            ev.Timestamp += ev.RandomDelayMinMs;
                        }
                    }
                    else
                    {
                        ev.Timestamp = 0;
                    }
                    
                    macro.Events.Add(ev);
                    currentDelay = 0; // Reset delay after consuming it
                    currentHasRandomDelay = false;
                    currentRandomDelayMinMs = 0;
                    currentRandomDelayMaxMs = 0;
                }
                else
                {
                    Log.Warning("Ignoring unsupported or malformed event line: {Line}", line);
                    currentDelay = 0;
                    currentHasRandomDelay = false;
                    currentRandomDelayMinMs = 0;
                    currentRandomDelayMaxMs = 0;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error parsing line: {Line}", line);
                currentDelay = 0;
                currentHasRandomDelay = false;
                currentRandomDelayMinMs = 0;
                currentRandomDelayMaxMs = 0;
            }
        }

        CommitPendingScriptStep();

        MarkLegacyCurrentPositionEvents(macro);
        
        // Recalculate stats
        macro.CalculateDuration();
        macro.MouseMoveCount = macro.Events.Count(e => e.Type == EventType.MouseMove);
        macro.ClickCount = macro.Events.Count(e => e.Type != EventType.MouseMove);
        
        return macro;
    }

    private static bool IsCurrentPositionToken(string token)
    {
        return token.Trim().Equals("CurrentPosition", StringComparison.OrdinalIgnoreCase)
            || token.Trim().Equals("Current", StringComparison.OrdinalIgnoreCase)
            || token.Trim().Equals("Live", StringComparison.OrdinalIgnoreCase)
            || token.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || token.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseCoordinateMode(string token, out MouseCoordinateMode mode)
    {
        var normalized = token.Trim();
        if (normalized.Equals("abs", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("absolute", StringComparison.OrdinalIgnoreCase))
        {
            mode = MouseCoordinateMode.Absolute;
            return true;
        }

        if (normalized.Equals("rel", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("relative", StringComparison.OrdinalIgnoreCase))
        {
            mode = MouseCoordinateMode.Relative;
            return true;
        }

        mode = default;
        return false;
    }

    private static void MarkLegacyCurrentPositionEvents(MacroSequence macro)
    {
        if (macro.IsAbsoluteCoordinates
            || !macro.SkipInitialZeroZero
            || macro.Events.Any(ev => ev.UseCurrentPosition))
        {
            return;
        }

        var markedAny = false;

        for (int index = 0; index < macro.Events.Count; index++)
        {
            var ev = macro.Events[index];

            if (ev.Type == EventType.MouseMove)
            {
                if (ev.X != 0 || ev.Y != 0)
                {
                    break;
                }

                continue;
            }

            if (!MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev))
            {
                continue;
            }

            if (MacroPositionSemantics.HasExplicitCoordinateMode(ev))
            {
                break;
            }

            if (ev.X != 0 || ev.Y != 0)
            {
                if (!markedAny)
                {
                    return;
                }

                break;
            }

            ev.UseCurrentPosition = true;
            macro.Events[index] = ev;
            markedAny = true;
        }
    }
}
