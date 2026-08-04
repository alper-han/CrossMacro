
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Handles saving and loading macro sequences from files
/// </summary>
public class MacroFileManager(
    Func<IKeyCodeMapper> keyCodeMapperFactory,
    IImageAssetCodec? imageAssetCodec = null,
    IScriptValidationService? scriptValidationService = null) : IMacroFileManager
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
    private readonly Func<IKeyCodeMapper> _keyCodeMapperFactory = keyCodeMapperFactory ?? throw new ArgumentNullException(nameof(keyCodeMapperFactory));
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();
    private readonly IScriptValidationService? _scriptValidationService = scriptValidationService;

    private enum MacroFileReadSection
    {
        Header,
        Script,
        Events,
    }

    /// <summary>
    /// Saves a macro sequence to a custom text file (.macro)
    /// </summary>
    public async Task SaveAsync(MacroSequence macro, string filePath)
    {
        var document = PersistedMacroCodec.Encode(macro);
        await SaveDocumentAsync(document, filePath).ConfigureAwait(false);
    }

    private async Task SaveDocumentAsync(PersistedMacroDocument document, string filePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        var macro = PersistedMacroCodec.Decode(document);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        if (!macro.IsValid())
        {
            throw new InvalidOperationException("Cannot save invalid macro sequence");
        }

        ValidateScriptStepsBeforeSave(macro);
        var imageAssets = await ValidateImagesBeforeSaveAsync(macro).ConfigureAwait(false);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var temporaryStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                65536,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using (temporaryStream.ConfigureAwait(false))
            {
                using (var writer = new StreamWriter(temporaryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true))
                {
                    await writer.WriteLineAsync($"# Name: {macro.Name}").ConfigureAwait(false);
                    await writer.WriteLineAsync($"# Created: {macro.CreatedAt:O}").ConfigureAwait(false);
                    await writer.WriteLineAsync($"# DurationMs: {macro.TotalDurationMs.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                    await writer.WriteLineAsync($"# IsAbsolute: {macro.IsAbsoluteCoordinates}").ConfigureAwait(false);
                    await writer.WriteLineAsync($"# SkipInitialZero: {macro.SkipInitialZeroZero}").ConfigureAwait(false);
                    if (macro.TrailingDelayMs > 0)
                    {
                        await writer.WriteLineAsync($"{TrailingDelayHeader}{macro.TrailingDelayMs.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                    }
                    if (macro.HasTrailingRandomDelay)
                    {
                        await writer.WriteLineAsync($"{TrailingRandomDelayHeader}{macro.TrailingDelayMinMs.ToString(CultureInfo.InvariantCulture)},{macro.TrailingDelayMaxMs.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                    }
                    foreach (var boundary in macro.TextInputBoundaries)
                    {
                        if (boundary.EventCount <= 0 || boundary.StartEventIndex < 0)
                        {
                            continue;
                        }

                        var json = JsonSerializer.Serialize(boundary, MacroFileJsonContext.Default.TextInputBoundary);
                        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                        await writer.WriteLineAsync($"{TextInputBoundaryHeader}{encoded}").ConfigureAwait(false);
                    }
                    foreach (var image in imageAssets)
                    {
                        await writer.WriteLineAsync($"{ImageHeader}{image.Key} = {image.Value}").ConfigureAwait(false);
                    }

                    await writer.WriteLineAsync($"# Format: {document.Format}").ConfigureAwait(false);
                    await writer.WriteLineAsync(ScriptSectionHeader).ConfigureAwait(false);
                    foreach (var scriptStep in macro.ScriptSteps.Where(step => !string.IsNullOrWhiteSpace(step)))
                    {
                        await WriteScriptStepAsync(writer, scriptStep).ConfigureAwait(false);
                    }

                    await writer.WriteLineAsync(EventsSectionHeader).ConfigureAwait(false);
                    foreach (var ev in macro.Events)
                    {
                        if (ev.DelayMs > 0)
                        {
                            await writer.WriteLineAsync($"W,{ev.DelayMs.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                        }
                        if (ev.HasRandomDelay)
                        {
                            await writer.WriteLineAsync($"WR,{ev.RandomDelayMinMs.ToString(CultureInfo.InvariantCulture)},{ev.RandomDelayMaxMs.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                        }

                        switch (ev.Type)
                        {
                            case EventType.MouseMove:
                                await writer.WriteLineAsync(BuildMouseMoveLine(ev)).ConfigureAwait(false);
                                break;
                            case EventType.ButtonPress:
                                await writer.WriteLineAsync(BuildMouseButtonLine("P", ev)).ConfigureAwait(false);
                                break;
                            case EventType.ButtonRelease:
                                await writer.WriteLineAsync(BuildMouseButtonLine("R", ev)).ConfigureAwait(false);
                                break;
                            case EventType.Click:
                                await writer.WriteLineAsync(BuildMouseButtonLine("C", ev)).ConfigureAwait(false);
                                break;
                            case EventType.KeyPress:
                                await writer.WriteLineAsync($"KP,{ev.KeyCode.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                                break;
                            case EventType.KeyRelease:
                                await writer.WriteLineAsync($"KR,{ev.KeyCode.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                                break;
                        }
                    }

                    await writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                }

                await temporaryStream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }

            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, destinationBackupFileName: null);
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
            return $"{command},{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)},{ev.Button},CurrentPosition";
        }

        if (MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev) && ev.CoordinateMode is not null)
        {
            return $"{command},{ToCoordinateModeToken(ev)},{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)},{ev.Button}";
        }

        return $"{command},{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)},{ev.Button}";
    }

    private static string BuildMouseMoveLine(MacroEvent ev)
    {
        return ev.CoordinateMode is not null ? $"M,{ToCoordinateModeToken(ev)},{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)}"
            : $"M,{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string ToCoordinateModeToken(MacroEvent ev)
    {
        if (ev.CoordinateMode is MouseCoordinateMode.Absolute)
        {
            return "abs";
        }

        return ev.CoordinateSpace is MouseCoordinateSpace.LogicalDesktop ? "rel-logical" : "rel-raw";
    }

    private void ValidateScriptStepsBeforeSave(MacroSequence macro)
    {
        if (macro.ScriptSteps is null)
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

        if (steps.Count is 0)
        {
            return;
        }

        var validationService = _scriptValidationService ?? new ScriptValidationService(_keyCodeMapperFactory());
        var diagnostics = validationService.Validate(steps);
        var diagnostic = diagnostics.Count > 0 ? diagnostics[0] : null;
        if (diagnostic is not null)
        {
            throw new InvalidOperationException($"Cannot save invalid macro script steps: {diagnostic.Message}");
        }
    }

    private static async Task WriteScriptStepAsync(TextWriter writer, string scriptStep)
    {
        var normalized = scriptStep.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        await writer.WriteLineAsync(lines[0]).ConfigureAwait(false);
        for (var index = 1; index < lines.Length; index++)
        {
            await writer.WriteLineAsync($"{ScriptContinuationPrefix}{lines[index]}").ConfigureAwait(false);
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
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Macro file not found", filePath);
        }

        ValidateMacroFile(filePath);
        var macro = new MacroSequence();
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var fileStreamDisposal = fileStream.ConfigureAwait(false);
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536);
        var lineReader = new BoundedLineReader(reader, MaxMacroLineChars);

        int currentDelay = 0;
        bool currentHasRandomDelay = false;
        int currentRandomDelayMinMs = 0;
        int currentRandomDelayMaxMs = 0;
        var section = MacroFileReadSection.Header;
        StringBuilder? pendingScriptStep = null;
        var totalEncodedImageChars = 0L;
        var lineNumber = 0;
        var scriptStepCount = 0;

        void CommitPendingScriptStep()
        {
            var scriptStep = pendingScriptStep?.ToString();
            if (!string.IsNullOrWhiteSpace(scriptStep))
            {
                if (++scriptStepCount > MaxMacroScriptSteps)
                {
                    throw new InvalidDataException($"Macro script exceeds the maximum of {MaxMacroScriptSteps} steps.");
                }

                macro.ScriptSteps.Add(scriptStep);
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
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

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

            if (section is MacroFileReadSection.Script)
            {
                if (trimmed.StartsWith('#'))
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

                    _ = pendingScriptStep.Append('\n').Append(line[ScriptContinuationPrefix.Length..]);
                    continue;
                }

                CommitPendingScriptStep();
                pendingScriptStep = new StringBuilder(line);
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                if (section is not MacroFileReadSection.Header)
                {
                    continue;
                }

                // Parse Header
                if (line.StartsWith("# Name: ", StringComparison.Ordinal))
                {
                    macro.Name = line.Substring(8).Trim();
                }
                else if (line.StartsWith("# Created: ", StringComparison.Ordinal) && DateTime.TryParse(line.Substring(11).Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    macro.CreatedAt = date;
                }
                else if (line.StartsWith("# DurationMs: ", StringComparison.Ordinal) && long.TryParse(line.Substring(14).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration))
                {
                    macro.TotalDurationMs = duration;
                }
                else if (line.StartsWith("# IsAbsolute: ", StringComparison.Ordinal) && bool.TryParse(line.Substring(14).Trim(), out var isAbsolute))
                {
                    macro.IsAbsoluteCoordinates = isAbsolute;
                }
                else if (line.StartsWith("# SkipInitialZero: ", StringComparison.Ordinal) && bool.TryParse(line.Substring(19).Trim(), out var skipZero))
                {
                    macro.SkipInitialZeroZero = skipZero;
                }
                else if (line.StartsWith(TrailingDelayHeader, StringComparison.Ordinal)
                                    && int.TryParse(line.Substring(TrailingDelayHeader.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var trailingDelay))
                {
                    macro.TrailingDelayMs = trailingDelay;
                }
                else if (line.StartsWith(TrailingRandomDelayHeader, StringComparison.Ordinal))
                {
                    var trailingRandomParts = line.Substring(TrailingRandomDelayHeader.Length).Trim().Split(',');
                    if (trailingRandomParts.Length >= 2
                        && int.TryParse(trailingRandomParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var trailingRandomMin)
                        && int.TryParse(trailingRandomParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var trailingRandomMax))
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
                    totalEncodedImageChars = await TryAddImageMetadataAsync(macro, line, totalEncodedImageChars).ConfigureAwait(false);
                }

                continue;
            }

            if (section is MacroFileReadSection.Script)
            {
                continue;
            }

            // Parse Event
            var parts = line.Split(',');
            if (parts.Length is 0)
            {
                continue;
            }

            string type = parts[0].ToUpperInvariant();

            // Handle Wait
            if ((type is "W" or "WAIT") && parts.Length >= 2)
            {
                if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int delay))
                {
                    currentDelay += delay;
                }
                continue;
            }
            if ((type is "WR" or "WAITRANDOM") && parts.Length >= 3)
            {
                if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int randomMinDelay) && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int randomMaxDelay))
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
                    RandomDelayMaxMs = currentRandomDelayMaxMs,
                };
                bool validEvent = false;

                // Handle Move
                if ((type is "M" or "MOVE") && parts.Length >= 3)
                {
                    var coordinateIndex = 1;
                    if (!int.TryParse(parts[coordinateIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x))
                    {
                        if (parts.Length < 4 || !TryParseCoordinateMode(parts[coordinateIndex], out var mode, out var space))
                        {
                            throw new FormatException($"Invalid coordinate mode token '{parts[coordinateIndex]}'");
                        }

                        ev.CoordinateMode = mode;
                        ev.CoordinateSpace = space;
                        coordinateIndex++;
                        x = int.Parse(parts[coordinateIndex], NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }

                    ev.Type = EventType.MouseMove;
                    ev.X = x;
                    ev.Y = int.Parse(parts[coordinateIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture);
                    ev.Button = MacroMouseButton.None;
                    validEvent = true;
                }
                // Handle Button Events
                else if ((type is "P" or "PRESS" or "R" or "RELEASE" or "C" or "CLICK") && parts.Length >= 4)
                {
                    var coordinateIndex = 1;
                    MouseCoordinateMode? coordinateMode = null;
                    MouseCoordinateSpace? coordinateSpace = null;
                    if (!int.TryParse(parts[coordinateIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x))
                    {
                        if (parts.Length < 5 || !TryParseCoordinateMode(parts[coordinateIndex], out var mode, out var space))
                        {
                            throw new FormatException($"Invalid coordinate mode token '{parts[coordinateIndex]}'");
                        }

                        coordinateMode = mode;
                        coordinateSpace = space;
                        coordinateIndex++;
                        x = int.Parse(parts[coordinateIndex], NumberStyles.Integer, CultureInfo.InvariantCulture);
                    }

                    ev.Type = type switch
                    {
                        "P" or "PRESS" => EventType.ButtonPress,
                        "R" or "RELEASE" => EventType.ButtonRelease,
                        "C" or "CLICK" => EventType.Click,
                        _ => EventType.Click,
                    };
                    ev.X = x;
                    ev.Y = int.Parse(parts[coordinateIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture);
                    ev.Button = Enum.Parse<MacroMouseButton>(parts[coordinateIndex + 2], ignoreCase: true);
                    ev.UseCurrentPosition = parts.Length > coordinateIndex + 3 && IsCurrentPositionToken(parts[coordinateIndex + 3]);
                    if (!ev.UseCurrentPosition && MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev))
                    {
                        ev.CoordinateMode = coordinateMode;
                        ev.CoordinateSpace = coordinateSpace;
                    }

                    validEvent = true;
                }
                // Handle Keyboard Events
                else if ((type is "KP" or "KEYPRESS") && parts.Length >= 2)
                {
                    ev.Type = EventType.KeyPress;
                    ev.KeyCode = int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
                    ev.Button = MacroMouseButton.None;
                    ev.X = 0;
                    ev.Y = 0;
                    validEvent = true;
                }
                else if ((type is "KR" or "KEYRELEASE") && parts.Length >= 2)
                {
                    ev.Type = EventType.KeyRelease;
                    ev.KeyCode = int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
                    ev.Button = MacroMouseButton.None;
                    ev.X = 0;
                    ev.Y = 0;
                    validEvent = true;
                }

                if (validEvent)
                {
                    if (macro.Events.Count >= MaxMacroEvents)
                    {
                        throw new InvalidDataException($"Macro events exceed the maximum of {MaxMacroEvents} events at line {lineNumber.ToString(CultureInfo.InvariantCulture)}.");
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
            catch (Exception ex) when (ex is not OutOfMemoryException)
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
        macro.MouseMoveCount = macro.Events.Count(e => e.Type is EventType.MouseMove);
        macro.ClickCount = macro.Events.Count(e => e.Type is not EventType.MouseMove);

        ValidateImageReferences(macro.ScriptSteps, macro.Images, "Loaded macro");
        return macro;
    }

    private static void ValidateMacroFile(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
            || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
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

        if (length is <= 0 or > MaxMacroFileBytes)
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

    private async Task<long> TryAddImageMetadataAsync(MacroSequence macro, string line, long totalEncodedImageChars)
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

        await _imageAssetCodec.ValidateBase64PngAsync(encoded, name, CancellationToken.None).ConfigureAwait(false);
        totalEncodedImageChars = checked(totalEncodedImageChars + encoded.Length);
        _imageAssetCodec.ValidateMacroBudget(totalEncodedImageChars);
        macro.Images[name] = encoded;
        return totalEncodedImageChars;
    }

    private async Task<List<KeyValuePair<string, string>>> ValidateImagesBeforeSaveAsync(MacroSequence macro)
    {
        if (macro.Images is null || macro.Images.Count is 0)
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
            if (encoded is null || encoded.Length is 0 || encoded.Any(char.IsWhiteSpace))
            {
                throw new InvalidDataException($"Image asset '{image.Key}': Image asset metadata is malformed.");
            }

            await _imageAssetCodec.ValidateBase64PngAsync(encoded, image.Key, CancellationToken.None).ConfigureAwait(false);
            totalEncodedBytes = checked(totalEncodedBytes + encoded.Length);
            imageAssets.Add(new KeyValuePair<string, string>(image.Key, encoded));
        }

        _imageAssetCodec.ValidateMacroBudget(totalEncodedBytes);
        ValidateImageReferences(macro.ScriptSteps, macro.Images, "Saved macro");
        return imageAssets;
    }

    private static void ValidateImageReferences(
        IList<string> scriptSteps,
        IDictionary<string, string> images,
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
                throw new InvalidDataException($"{context} script step {(index + 1).ToString(CultureInfo.InvariantCulture)}: {error ?? "invalid image command"}");
            }

            var imageNameIndex = parts.Length >= 6
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? 5
                : 1;
            var imageName = parts[imageNameIndex];
            if (!images.ContainsKey(imageName))
            {
                throw new InvalidDataException($"{context} script step {(index + 1).ToString(CultureInfo.InvariantCulture)}: image asset '{imageName}' is not defined.");
            }
        }
    }

    private sealed class BoundedLineReader(StreamReader reader, int maxChars)
    {
        private readonly StreamReader _reader = reader;
        private readonly int _maxChars = maxChars;
        private readonly char[] _buffer = new char[4096];
        private int _bufferPosition;
        private int _bufferLength;

        public async Task<string?> ReadLineAsync()
        {
            var builder = new StringBuilder();
            while (true)
            {
                if (_bufferPosition == _bufferLength)
                {
                    _bufferLength = await _reader.ReadAsync(_buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                    _bufferPosition = 0;
                    if (_bufferLength is 0)
                    {
                        return builder.Length is 0 ? null : builder.ToString();
                    }
                }

                var lineEnd = Array.IndexOf(_buffer, '\n', _bufferPosition, _bufferLength - _bufferPosition);
                var segmentEnd = lineEnd >= 0 ? lineEnd : _bufferLength;
                var segmentLength = segmentEnd - _bufferPosition;
                if (builder.Length + segmentLength > _maxChars)
                {
                    throw new InvalidDataException($"Macro line exceeds the maximum of {_maxChars.ToString(CultureInfo.InvariantCulture)} characters.");
                }

                _ = builder.Append(_buffer, _bufferPosition, segmentLength);
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

    private static bool TryParseCoordinateMode(
        string token,
        out MouseCoordinateMode mode,
        out MouseCoordinateSpace? space)
    {
        var normalized = token.Trim();
        if (normalized.Equals("abs", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("absolute", StringComparison.OrdinalIgnoreCase))
        {
            mode = MouseCoordinateMode.Absolute;
            space = MouseCoordinateSpace.LogicalDesktop;
            return true;
        }

        if (normalized.Equals("rel", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("relative", StringComparison.OrdinalIgnoreCase))
        {
            mode = MouseCoordinateMode.Relative;
            space = null;
            return true;
        }

        if (normalized.Equals("rel-logical", StringComparison.OrdinalIgnoreCase))
        {
            mode = MouseCoordinateMode.Relative;
            space = MouseCoordinateSpace.LogicalDesktop;
            return true;
        }

        if (normalized.Equals("rel-raw", StringComparison.OrdinalIgnoreCase))
        {
            mode = MouseCoordinateMode.Relative;
            space = MouseCoordinateSpace.RawDevice;
            return true;
        }

        mode = default;
        space = null;
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

            if (ev.Type is EventType.MouseMove)
            {
                if (ev.X is not 0 || ev.Y is not 0)
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

            if (ev.X is not 0 || ev.Y is not 0)
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
