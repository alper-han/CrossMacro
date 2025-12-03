using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Handles saving and loading macro sequences from files
/// </summary>
public class MacroFileManager : IMacroFileManager
{
    public MacroFileManager()
    {
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
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        using var writer = new StreamWriter(filePath);
        
        // Write Header
        await writer.WriteLineAsync($"# Name: {macro.Name}");
        await writer.WriteLineAsync($"# Created: {macro.CreatedAt:O}");
        await writer.WriteLineAsync($"# DurationMs: {macro.TotalDurationMs}");
        await writer.WriteLineAsync($"# IsAbsolute: {macro.IsAbsoluteCoordinates}");
        await writer.WriteLineAsync("# Format: Cmd,Args...");
        
        // Write Events
        foreach (var ev in macro.Events)
        {
            // Write Delay as separate line if > 0
            if (ev.DelayMs > 0)
            {
                await writer.WriteLineAsync($"W,{ev.DelayMs}");
            }

            switch (ev.Type)
            {
                case EventType.MouseMove:
                    // Format: M,X,Y
                    await writer.WriteLineAsync($"M,{ev.X},{ev.Y}");
                    break;
                    
                case EventType.ButtonPress:
                    // Format: P,X,Y,Button
                    await writer.WriteLineAsync($"P,{ev.X},{ev.Y},{ev.Button}");
                    break;
                    
                case EventType.ButtonRelease:
                    // Format: R,X,Y,Button
                    await writer.WriteLineAsync($"R,{ev.X},{ev.Y},{ev.Button}");
                    break;
                    
                case EventType.Click:
                    // Format: C,X,Y,Button (Used for Scroll)
                    await writer.WriteLineAsync($"C,{ev.X},{ev.Y},{ev.Button}");
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
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            if (line.StartsWith("#"))
            {
                // Parse Header
                if (line.StartsWith("# Name: "))
                    macro.Name = line.Substring(8).Trim();
                else if (line.StartsWith("# Created: ") && DateTime.TryParse(line.Substring(11).Trim(), out var date))
                    macro.CreatedAt = date;
                else if (line.StartsWith("# DurationMs: ") && long.TryParse(line.Substring(14).Trim(), out var duration))
                    macro.TotalDurationMs = duration;
                else if (line.StartsWith("# IsAbsolute: ") && bool.TryParse(line.Substring(14).Trim(), out var isAbs))
                    macro.IsAbsoluteCoordinates = isAbs;
                
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
            
            try
            {
                var ev = new MacroEvent { DelayMs = currentDelay };
                bool validEvent = false;

                // Handle Move
                if ((type == "M" || type == "MOVE") && parts.Length >= 3)
                {
                    ev.Type = EventType.MouseMove;
                    ev.X = int.Parse(parts[1]);
                    ev.Y = int.Parse(parts[2]);
                    ev.Button = MouseButton.None;
                    validEvent = true;
                }
                // Handle Button Events
                else if ((type == "P" || type == "PRESS" || 
                          type == "R" || type == "RELEASE" || 
                          type == "C" || type == "CLICK") && parts.Length >= 4)
                {
                    ev.Type = type switch 
                    {
                        "P" or "PRESS" => EventType.ButtonPress,
                        "R" or "RELEASE" => EventType.ButtonRelease,
                        "C" or "CLICK" => EventType.Click,
                        _ => EventType.Click
                    };
                    ev.X = int.Parse(parts[1]);
                    ev.Y = int.Parse(parts[2]);
                    ev.Button = Enum.Parse<MouseButton>(parts[3]);
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
                    }
                    else
                    {
                        ev.Timestamp = 0;
                    }
                    
                    macro.Events.Add(ev);
                    currentDelay = 0; // Reset delay after consuming it
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error parsing line: {Line}", line);
            }
        }
        
        // Recalculate stats
        macro.CalculateDuration();
        macro.MouseMoveCount = macro.Events.Count(e => e.Type == EventType.MouseMove);
        macro.ClickCount = macro.Events.Count(e => e.Type != EventType.MouseMove);
        
        return macro;
    }
}
