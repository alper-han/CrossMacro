using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using Canonical = CrossMacro.Infrastructure.Persistence.Macros;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Handles saving and loading macro sequences from files
/// </summary>
public class MacroFileManager : IMacroFileManager
{
    private const long MaxMacroFileBytes = 32L * 1024 * 1024;
    private const int MaxMacroLineChars = 256 * 1024;
    private const int MaxMacroFileLines = 100_000;
    private const int MaxMacroScriptSteps = 10_000;
    private const int MaxMacroEvents = 1_000_000;
    private const string TrailingDelayHeader = "# TrailingDelayMs: ";
    private const string TrailingRandomDelayHeader = "# TrailingRandomDelayMs: ";
    private const string TextInputBoundaryHeader = "# TextInputBoundaryBase64: ";
    private const string ImageHeader = "# Image: ";
    private const string ScriptSectionHeader = "[Script]";
    private const string EventsSectionHeader = "[Events]";
    private const string ScriptContinuationPrefix = "| ";
    private readonly Func<IKeyCodeMapper> _keyCodeMapperFactory;
    private readonly IImageAssetCodec _imageAssetCodec;
    private readonly IScriptValidationService? _scriptValidationService;

    private enum MacroFileReadSection
    {
        Header,
        Script,
        Events
    }

    public MacroFileManager(
        Func<IKeyCodeMapper> keyCodeMapperFactory,
        IImageAssetCodec? imageAssetCodec = null,
        IScriptValidationService? scriptValidationService = null)
    {
        _keyCodeMapperFactory = keyCodeMapperFactory ?? throw new ArgumentNullException(nameof(keyCodeMapperFactory));
        _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();
        _scriptValidationService = scriptValidationService;
    }
    
    /// <summary>
    /// Saves a macro sequence to a custom text file (.macro)
    /// </summary>
    public async Task SaveAsync(MacroSequence macro, string filePath)
    {
        var document = Canonical.PersistedMacroCodec.Encode(macro);
        await SaveDocumentAsync(document, filePath).ConfigureAwait(false);
    }

    private async Task SaveDocumentAsync(Canonical.PersistedMacroDocument document, string filePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        var macro = Canonical.PersistedMacroCodec.Decode(document);
            
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        
        if (!macro.IsValid())
            throw new InvalidOperationException("Cannot save invalid macro sequence");

        ValidateScriptStepsBeforeSave(macro);
        var imageAssets = ValidateImagesBeforeSave(macro);
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var temporaryStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                65536,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                using (var writer = new StreamWriter(temporaryStream, new UTF8Encoding(false), 1024, leaveOpen: true))
                {
                    await writer.WriteLineAsync($"# Name: {macro.Name}");
                    await writer.WriteLineAsync($"# Created: {macro.CreatedAt:O}");
                    await writer.WriteLineAsync($"# DurationMs: {macro.TotalDurationMs}");
                    await writer.WriteLineAsync($"# IsAbsolute: {macro.IsAbsoluteCoordinates}");
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
                    foreach (var image in imageAssets)
                    {
                        await writer.WriteLineAsync($"{ImageHeader}{image.Key} = {image.Value}");
                    }

                    await writer.WriteLineAsync($"# Format: {document.Format}");
                    await writer.WriteLineAsync(ScriptSectionHeader);
                    foreach (var scriptStep in macro.ScriptSteps)
                    {
                        if (!string.IsNullOrWhiteSpace(scriptStep))
                        {
                            await WriteScriptStepAsync(writer, scriptStep);
                        }
                    }

                    await writer.WriteLineAsync(EventsSectionHeader);
                    foreach (var ev in macro.Events)
                    {
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
                                await writer.WriteLineAsync(BuildMouseButtonLine("P", ev));
                                break;
                            case EventType.ButtonRelease:
                                await writer.WriteLineAsync(BuildMouseButtonLine("R", ev));
                                break;
                            case EventType.Click:
                                await writer.WriteLineAsync(BuildMouseButtonLine("C", ev));
                                break;
                            case EventType.KeyPress:
                                await writer.WriteLineAsync($"KP,{ev.KeyCode}");
                                break;
                            case EventType.KeyRelease:
                                await writer.WriteLineAsync($"KR,{ev.KeyCode}");
                                break;
                        }
                    }

                    await writer.FlushAsync();
                }

                temporaryStream.Flush(true);
            }

            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, null);
            }
            else
            {
                File.Move(temporaryPath, filePath, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
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

        var validationService = _scriptValidationService ?? new ScriptValidationService(_keyCodeMapperFactory());
        var diagnostic = validationService.Validate(steps).FirstOrDefault();
        if (diagnostic is not null)
        {
            throw new InvalidOperationException($"Cannot save invalid macro script steps: {diagnostic.Message}");
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
        return await LoadRuntimeAsync(filePath).ConfigureAwait(false);
    }

    private async Task<MacroSequence?> LoadRuntimeAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Macro file not found", filePath);
        
        ValidateMacroFile(filePath);
        var macro = new MacroSequence();
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536);
        var lineReader = new BoundedLineReader(reader, MaxMacroLineChars);
        
        int currentDelay = 0;
        bool currentHasRandomDelay = false;
        int currentRandomDelayMinMs = 0;
        int currentRandomDelayMaxMs = 0;
        var section = MacroFileReadSection.Header;
        string? pendingScriptStep = null;
        var totalEncodedImageChars = 0L;
        var lineNumber = 0;
        var scriptStepCount = 0;

        void CommitPendingScriptStep()
        {
            if (!string.IsNullOrWhiteSpace(pendingScriptStep))
            {
                if (++scriptStepCount > MaxMacroScriptSteps)
                {
                    throw new InvalidDataException($"Macro script exceeds the maximum of {MaxMacroScriptSteps} steps.");
                }

                macro.ScriptSteps.Add(pendingScriptStep);
            }

            pendingScriptStep = null;
        }
        
        while (await lineReader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (lineNumber > MaxMacroFileLines)
            {
                throw new InvalidDataException($"Macro file exceeds the maximum of {MaxMacroFileLines} lines.");
            }

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
                else if (line.StartsWith("# IsAbsolute: ") && bool.TryParse(line.Substring(14).Trim(), out var isAbsolute))
                    macro.IsAbsoluteCoordinates = isAbsolute;
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
                else if (line.StartsWith(ImageHeader, StringComparison.Ordinal))
                {
                    TryAddImageMetadata(macro, line, ref totalEncodedImageChars);
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
                    if (macro.Events.Count >= MaxMacroEvents)
                    {
                        throw new InvalidDataException($"Macro events exceed the maximum of {MaxMacroEvents} events at line {lineNumber}.");
                    }

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

        ValidateImageReferences(macro.ScriptSteps, macro.Images, "Loaded macro");
        return macro;
    }

    private static void ValidateMacroFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if ((fileInfo.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("Macro path must refer to a regular file.");
        }

        long length;
        try
        {
            length = fileInfo.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidDataException("Macro path must refer to a regular file.", ex);
        }

        if (length <= 0 || length > MaxMacroFileBytes)
        {
            throw new InvalidDataException(length <= 0
                ? "Macro file is empty."
                : $"Macro file exceeds the maximum size of {MaxMacroFileBytes} bytes.");
        }
    }

    private static bool IsCurrentPositionToken(string token)
    {
        return token.Trim().Equals("CurrentPosition", StringComparison.OrdinalIgnoreCase)
            || token.Trim().Equals("Current", StringComparison.OrdinalIgnoreCase)
            || token.Trim().Equals("Live", StringComparison.OrdinalIgnoreCase)
            || token.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || token.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private void TryAddImageMetadata(MacroSequence macro, string line, ref long totalEncodedImageChars)
    {
        var metadata = line.Substring(ImageHeader.Length);
        var separatorIndex = metadata.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            throw new InvalidDataException("Malformed image metadata: missing '=' separator.");
        }

        var name = metadata[..separatorIndex].Trim();
        var encoded = metadata[(separatorIndex + 1)..].Trim();
        if (!IsValidImageName(name))
        {
            throw new InvalidDataException($"Image asset '{name}': Image asset name is invalid.");
        }

        _imageAssetCodec.ValidateBase64Png(encoded, name);
        totalEncodedImageChars = checked(totalEncodedImageChars + encoded.Length);
        _imageAssetCodec.ValidateMacroBudget(totalEncodedImageChars);
        macro.Images[name] = encoded;
    }

    private List<KeyValuePair<string, string>> ValidateImagesBeforeSave(MacroSequence macro)
    {
        if (macro.Images is null || macro.Images.Count == 0)
        {
            ValidateImageReferences(macro.ScriptSteps, new Dictionary<string, string>(StringComparer.Ordinal), "Saved macro");
            return [];
        }

        var imageAssets = new List<KeyValuePair<string, string>>(macro.Images.Count);
        long totalEncodedBytes = 0;
        foreach (var image in macro.Images.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!IsValidImageName(image.Key))
            {
                throw new InvalidDataException($"Image asset '{image.Key}': Image asset name is invalid.");
            }

            var encoded = image.Value?.Trim();
            if (encoded is null || encoded.Length == 0 || encoded.Any(char.IsWhiteSpace))
            {
                throw new InvalidDataException($"Image asset '{image.Key}': Image asset metadata is malformed.");
            }

            _imageAssetCodec.ValidateBase64Png(encoded, image.Key);
            totalEncodedBytes = checked(totalEncodedBytes + encoded.Length);
            imageAssets.Add(new KeyValuePair<string, string>(image.Key, encoded));
        }

        _imageAssetCodec.ValidateMacroBudget(totalEncodedBytes);
        ValidateImageReferences(macro.ScriptSteps, macro.Images, "Saved macro");
        return imageAssets;
    }

    private static void ValidateImageReferences(
        IReadOnlyList<string> scriptSteps,
        IReadOnlyDictionary<string, string> images,
        string context)
    {
        for (var index = 0; index < scriptSteps.Count; index++)
        {
            var step = scriptSteps[index];
            if (!RunScriptScreenReadingStepParser.TryParseCommand(step.Trim(), out var command, out var parts)
                || command is not (RunScriptScreenReadingCommand.ImageSearch or RunScriptScreenReadingCommand.ImageClick or RunScriptScreenReadingCommand.WaitImage))
            {
                continue;
            }

            if (!RunScriptScreenReadingStepParser.TryValidateStep(step, out var error) || error is not null)
            {
                throw new InvalidDataException($"{context} script step {index + 1}: {error ?? "invalid image command"}");
            }

            var imageNameIndex = parts.Length >= 6
                && int.TryParse(parts[1], out _)
                && int.TryParse(parts[2], out _)
                && int.TryParse(parts[3], out _)
                && int.TryParse(parts[4], out _)
                ? 5
                : 1;
            var imageName = parts[imageNameIndex];
            if (!images.ContainsKey(imageName))
            {
                throw new InvalidDataException($"{context} script step {index + 1}: image asset '{imageName}' is not defined.");
            }
        }
    }

    private sealed class BoundedLineReader
    {
        private readonly StreamReader _reader;
        private readonly int _maxChars;
        private readonly char[] _buffer = new char[4096];
        private int _bufferPosition;
        private int _bufferLength;

        public BoundedLineReader(StreamReader reader, int maxChars)
        {
            _reader = reader;
            _maxChars = maxChars;
        }

        public async Task<string?> ReadLineAsync()
        {
            var builder = new StringBuilder();
            while (true)
            {
                if (_bufferPosition == _bufferLength)
                {
                    _bufferLength = await _reader.ReadAsync(_buffer.AsMemory()).ConfigureAwait(false);
                    _bufferPosition = 0;
                    if (_bufferLength == 0)
                    {
                        return builder.Length == 0 ? null : builder.ToString();
                    }
                }

                var lineEnd = Array.IndexOf(_buffer, '\n', _bufferPosition, _bufferLength - _bufferPosition);
                var segmentEnd = lineEnd >= 0 ? lineEnd : _bufferLength;
                var segmentLength = segmentEnd - _bufferPosition;
                if (builder.Length + segmentLength > _maxChars)
                {
                    throw new InvalidDataException($"Macro line exceeds the maximum of {_maxChars} characters.");
                }

                builder.Append(_buffer, _bufferPosition, segmentLength);
                _bufferPosition = segmentEnd;
                if (lineEnd < 0)
                {
                    continue;
                }

                _bufferPosition++;
                if (builder.Length > 0 && builder[^1] == '\r')
                {
                    builder.Length--;
                }

                return builder.ToString();
            }
        }
    }

    private static bool IsValidImageName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (!(char.IsLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        for (var index = 1; index < name.Length; index++)
        {
            var character = name[index];
            if (!(char.IsLetterOrDigit(character) || character == '_'))
            {
                return false;
            }
        }

        return true;
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
