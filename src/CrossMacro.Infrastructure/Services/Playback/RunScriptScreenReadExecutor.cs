
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptScreenReadExecutor(
    IScreenPixelReader screenPixelReader,
    IMousePositionProvider? mousePositionProvider,
    Func<MacroEvent, CancellationToken, Task>? executeEventAsync = null,
    IImageClickMovementResolver? imageClickMovementResolver = null,
    IInputSimulator? inputSimulator = null,
    IImageAssetCodec? imageAssetCodec = null,
    Func<CancellationToken, Task>? flushPendingCursorMovementAsync = null)
{
    private readonly IScreenPixelReader _screenPixelReader = screenPixelReader ?? throw new ArgumentNullException(nameof(screenPixelReader));
    private readonly IMousePositionProvider? _mousePositionProvider = mousePositionProvider;
    private readonly Func<MacroEvent, CancellationToken, Task>? _executeEventAsync = executeEventAsync;
    private readonly IImageClickMovementResolver? _imageClickMovementResolver = imageClickMovementResolver;
    private readonly IInputSimulator? _inputSimulator = inputSimulator;
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();
    private readonly Func<CancellationToken, Task>? _flushPendingCursorMovementAsync = flushPendingCursorMovementAsync;

    public async Task ExecuteAsync(
        MacroSequence macro,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(runtimeVariables);

        for (var i = 0; i < macro.ScriptSteps.Count; i++)
        {
            await ExecuteStepAsync(macro.ScriptSteps[i], i + 1, runtimeVariables, cancellationToken, macro.Images).ConfigureAwait(false);
        }
    }

    public async Task ExecuteStepAsync(
        string step,
        int stepNumber,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken,
        IDictionary<string, string>? imageAssets = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(runtimeVariables);

        cancellationToken.ThrowIfCancellationRequested();

        var trimmedStep = step.Trim();
        if (trimmedStep.Length is 0)
        {
            return;
        }

        if (!RunScriptScreenReadingStepParser.TryParseCommand(trimmedStep, out var command, out var parts))
        {
            return;
        }

        if ((command is RunScriptScreenReadingCommand.ImageSearch
            or RunScriptScreenReadingCommand.ImageClick
            or RunScriptScreenReadingCommand.WaitImage)
            && (!RunScriptScreenReadingStepParser.TryValidateStep(trimmedStep, out var validationError)
                || validationError is not null))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: {validationError ?? "invalid image command"}");
        }

        if (command is RunScriptScreenReadingCommand.PixelColor)
        {
            await ExecutePixelColorAsync(stepNumber, parts, runtimeVariables, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command is RunScriptScreenReadingCommand.WaitColor)
        {
            await ExecuteWaitColorAsync(stepNumber, parts, runtimeVariables, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command is RunScriptScreenReadingCommand.PixelSearch)
        {
            await ExecutePixelSearchAsync(stepNumber, parts, runtimeVariables, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command is RunScriptScreenReadingCommand.ImageSearch)
        {
            await ExecuteImageSearchAsync(stepNumber, parts, runtimeVariables, imageAssets, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command is RunScriptScreenReadingCommand.ImageClick)
        {
            await ExecuteImageClickAsync(stepNumber, parts, runtimeVariables, imageAssets, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command is RunScriptScreenReadingCommand.WaitImage)
        {
            await ExecuteWaitImageAsync(stepNumber, parts, runtimeVariables, imageAssets, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static bool IsScreenReadingStep(string step)
    {
        return RunScriptSyntax.IsScreenReadingStep(step);
    }

    private async Task ExecutePixelColorAsync(
        int stepNumber,
        string[] parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var isRelative = parts.Length > 1 && string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase);
        var coordinateIndex = isRelative ? 2 : 1;
        var x = ParseInteger(parts[coordinateIndex]);
        var y = ParseInteger(parts[coordinateIndex + 1]);
        var point = isRelative
            ? await ResolveRelativePointAsync(stepNumber, x, y, cancellationToken).ConfigureAwait(false)
            : new ScreenPoint(x, y);

        var result = await _screenPixelReader.GetPixelAsync(point, CreateSingleCaptureOptions(cancellationToken)).ConfigureAwait(false);
        EnsureSuccess(stepNumber, "pixelcolor", result);

        var variableIndex = isRelative ? 4 : 3;
        if (parts.Length > variableIndex)
        {
            runtimeVariables[parts[variableIndex]] = result.Value.ToString();
        }
    }

    private async Task ExecuteWaitColorAsync(
        int stepNumber,
        string[] parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var point = new ScreenPoint(ParseInteger(parts[1]), ParseInteger(parts[2]));
        var expected = ResolveTargetColor(parts[3], stepNumber, runtimeVariables);
        var index = 4;
        TimeSpan? timeout = index < parts.Length
            ? TimeSpan.FromMilliseconds(ParseInteger(parts[index++]))
            : null;
        var resultVariable = index < parts.Length
            ? parts[index]
            : null;

        var result = await _screenPixelReader.WaitForPixelAsync(point, expected, CreateWaitingOptions(timeout, cancellationToken)).ConfigureAwait(false);
        if (resultVariable is not null && CanStoreResultVariable(result))
        {
            runtimeVariables[resultVariable] = result.IsSuccess ? "true" : "false";
            return;
        }

        EnsureSuccess(stepNumber, "waitcolor", result);
    }

    private async Task ExecutePixelSearchAsync(
        int stepNumber,
        string[] parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var x1 = ParseInteger(parts[1]);
        var y1 = ParseInteger(parts[2]);
        var x2 = ParseInteger(parts[3]);
        var y2 = ParseInteger(parts[4]);
        var expected = ResolveTargetColor(parts[5], stepNumber, runtimeVariables);
        var tolerance = ParsePixelSearchTolerance(parts);
        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var right = Math.Max(x1, x2);
        var bottom = Math.Max(y1, y2);
        var widthValue = (long)right - left;
        var heightValue = (long)bottom - top;
        if (widthValue > int.MaxValue || heightValue > int.MaxValue)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: pixelsearch failed: bounds exceed the supported screen coordinate range.");
        }

        var width = (int)widthValue;
        var height = (int)heightValue;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: pixelsearch failed: bounds must be end-exclusive and produce a positive region.");
        }

        var region = new ScreenRect(left, top, width, height);

        var timeout = ParseScreenReadTimeout(parts, GetPixelSearchOptionStartIndex(parts));
        var result = await _screenPixelReader.SearchPixelAsync(region, expected, tolerance, CreateWaitingOptions(timeout, cancellationToken)).ConfigureAwait(false);
        var variableLayout = GetPixelSearchVariableLayout(parts);
        if (variableLayout.FoundVariableName is not null && CanStoreResultVariable(result))
        {
            runtimeVariables[variableLayout.FoundVariableName] = result.IsSuccess ? "true" : "false";
            runtimeVariables[variableLayout.XVariableName!] = result.IsSuccess
                ? result.Value.Point.X.ToString(CultureInfo.InvariantCulture)
                : "-1";
            runtimeVariables[variableLayout.YVariableName!] = result.IsSuccess
                ? result.Value.Point.Y.ToString(CultureInfo.InvariantCulture)
                : "-1";
            return;
        }

        EnsureSuccess(stepNumber, "pixelsearch", result);

        if (variableLayout.XVariableName is not null)
        {
            runtimeVariables[variableLayout.XVariableName] = result.Value.Point.X.ToString(CultureInfo.InvariantCulture);
            runtimeVariables[variableLayout.YVariableName!] = result.Value.Point.Y.ToString(CultureInfo.InvariantCulture);
        }
    }

    private async Task<ScreenPoint> ResolveRelativePointAsync(
        int stepNumber,
        int dx,
        int dy,
        CancellationToken cancellationToken)
    {
        if (_mousePositionProvider is null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: pixelcolor rel failed: no mouse position provider is available.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_flushPendingCursorMovementAsync is not null)
        {
            await _flushPendingCursorMovementAsync(cancellationToken).ConfigureAwait(false);
        }

        var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false) ?? throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: pixelcolor rel failed: current mouse position is unavailable.");
        return new ScreenPoint(checked(position.X + dx), checked(position.Y + dy));
    }

    private async Task ExecuteImageSearchAsync(
        int stepNumber,
        string[] parts,
        IDictionary<string, string> runtimeVariables,
        IDictionary<string, string>? imageAssets,
        CancellationToken cancellationToken)
    {
        if (_screenPixelReader is not IScreenImageSearchReader imageSearchReader)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: imagesearch failed: screen image matching is not available for provider '{_screenPixelReader.ProviderName}'.");
        }

        var hasRegion = HasImageSearchRegion(parts);
        var imageNameIndex = hasRegion ? 5 : 1;
        var region = hasRegion ? ParseImageSearchRegion(stepNumber, parts) : (ScreenRect?)null;
        var imageName = parts[imageNameIndex];
        using var template = await DecodeImageAssetAsync(stepNumber, "imagesearch", imageName, imageAssets, cancellationToken).ConfigureAwait(false);
        var variableLayout = GetImageSearchVariableLayout(parts, imageNameIndex + 1);
        var matchOptions = ParseImageSearchOptions(stepNumber, "imagesearch", parts, imageNameIndex + 1 + variableLayout.VariableCount, region);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await imageSearchReader.SearchImageAsync(region, template, matchOptions, CreateSingleCaptureOptions(cancellationToken)).ConfigureAwait(false);
        if (variableLayout.FoundVariableName is not null && CanStoreResultVariable(result))
        {
            runtimeVariables[variableLayout.FoundVariableName] = result.IsSuccess ? "true" : "false";
            runtimeVariables[variableLayout.XVariableName!] = result.IsSuccess
                ? result.Value.Point.X.ToString(CultureInfo.InvariantCulture)
                : "-1";
            runtimeVariables[variableLayout.YVariableName!] = result.IsSuccess
                ? result.Value.Point.Y.ToString(CultureInfo.InvariantCulture)
                : "-1";
            return;
        }

        EnsureSuccess(stepNumber, "imagesearch", result);
    }

    private IScreenImageSearchReader GetImageSearchReader(int stepNumber, string command)
    {
        if (_screenPixelReader is not IScreenImageSearchReader imageSearchReader)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: screen image matching is not available for provider '{_screenPixelReader.ProviderName}'.");
        }

        return imageSearchReader;
    }

    private static bool HasImageSearchRegion(string[] parts)
    {
        return parts.Length >= 6
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private async Task ExecuteImageClickAsync(
        int stepNumber,
        string[] parts,
        IDictionary<string, string> runtimeVariables,
        IDictionary<string, string>? imageAssets,
        CancellationToken cancellationToken)
    {
        if (_executeEventAsync is null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: imageclick failed: input playback is not available in this runtime path.");
        }

        var imageSearchReader = GetImageSearchReader(stepNumber, "imageclick");
        var hasRegion = HasImageSearchRegion(parts);
        var imageNameIndex = hasRegion ? 5 : 1;
        var region = hasRegion ? ParseImageSearchRegion(stepNumber, parts, "imageclick") : (ScreenRect?)null;
        var imageName = parts[imageNameIndex];
        using var template = await DecodeImageAssetAsync(stepNumber, "imageclick", imageName, imageAssets, cancellationToken).ConfigureAwait(false);
        var variableLayout = GetImageClickVariableLayout(parts, imageNameIndex + 1);
        var optionStartIndex = imageNameIndex + 1 + variableLayout.VariableCount;
        var matchOptions = ParseImageSearchOptions(stepNumber, "imageclick", parts, optionStartIndex, region);
        var timeout = ParseImageTimeout(parts, optionStartIndex) ?? ScreenReadOptions.DefaultTimeout;
        var button = ParseImageClickButton(parts, optionStartIndex);

        var result = await SearchImageUntilConsistentAsync(imageSearchReader, region, template, matchOptions, timeout, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess && variableLayout.FoundVariableName is not null && result.ErrorKind is ScreenReadErrorKind.CaptureTimeout)
        {
            StoreImageSearchVariables(runtimeVariables, variableLayout, found: false, default);
            return;
        }

        EnsureSuccess(stepNumber, "imageclick", result);

        var clickPoint = new ScreenPoint(
            checked(result.Value.Point.X + ((result.Value.MatchedWidth > 0 ? result.Value.MatchedWidth : template.LogicalBounds.Width) / 2)),
            checked(result.Value.Point.Y + ((result.Value.MatchedHeight > 0 ? result.Value.MatchedHeight : template.LogicalBounds.Height) / 2)));
        StoreImageSearchVariables(runtimeVariables, variableLayout, found: true, clickPoint);
        if (_imageClickMovementResolver is null || _inputSimulator is null)
        {
            throw new ImageClickMovementUnsupportedException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: imageclick failed: input movement is not available in this runtime path.");
        }

        var movement = await _imageClickMovementResolver.ResolveAsync(_inputSimulator, clickPoint, cancellationToken).ConfigureAwait(false);
        if (!movement.IsSuccess)
        {
            throw new ImageClickMovementUnsupportedException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: imageclick failed: {movement.ErrorMessage}");
        }

        await _executeEventAsync(new MacroEvent
        {
            Type = EventType.Click,
            X = movement.X,
            Y = movement.Y,
            Button = button,
            CoordinateMode = movement.CoordinateMode,
            CoordinateSpace = movement.CoordinateMode is MouseCoordinateMode.Absolute
                ? MouseCoordinateSpace.LogicalDesktop
                : MouseCoordinateSpace.RawDevice,
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteWaitImageAsync(
        int stepNumber,
        string[] parts,
        IDictionary<string, string> runtimeVariables,
        IDictionary<string, string>? imageAssets,
        CancellationToken cancellationToken)
    {
        var imageSearchReader = GetImageSearchReader(stepNumber, "waitimage");
        var hasRegion = HasImageSearchRegion(parts);
        var imageNameIndex = hasRegion ? 5 : 1;
        var region = hasRegion ? ParseImageSearchRegion(stepNumber, parts, "waitimage") : (ScreenRect?)null;
        var imageName = parts[imageNameIndex];
        using var template = await DecodeImageAssetAsync(stepNumber, "waitimage", imageName, imageAssets, cancellationToken).ConfigureAwait(false);
        var variableLayout = GetImageSearchVariableLayout(parts, imageNameIndex + 1);
        var optionStartIndex = imageNameIndex + 1 + variableLayout.VariableCount;
        var matchOptions = ParseImageSearchOptions(stepNumber, "waitimage", parts, optionStartIndex, region);
        var timeout = ParseImageTimeout(parts, optionStartIndex) ?? ScreenReadOptions.DefaultTimeout;
        var result = await SearchImageUntilConsistentAsync(imageSearchReader, region, template, matchOptions, timeout, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            StoreImageSearchVariables(runtimeVariables, variableLayout, found: true, result.Value.Point);
            return;
        }

        if (result.ErrorKind is ScreenReadErrorKind.CaptureTimeout && variableLayout.FoundVariableName is not null)
        {
            StoreImageSearchVariables(runtimeVariables, variableLayout, found: false, default);
            return;
        }

        EnsureSuccess(stepNumber, "waitimage", result);
    }

    private static ScreenRect ParseImageSearchRegion(int stepNumber, string[] parts, string command = "imagesearch")
    {
        var x1 = ParseInteger(parts[1]);
        var y1 = ParseInteger(parts[2]);
        var x2 = ParseInteger(parts[3]);
        var y2 = ParseInteger(parts[4]);
        var widthValue = (long)x2 - x1;
        var heightValue = (long)y2 - y1;
        if (widthValue > int.MaxValue || heightValue > int.MaxValue)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: bounds exceed the supported screen coordinate range.");
        }

        var width = (int)widthValue;
        var height = (int)heightValue;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: bounds must be end-exclusive and produce a positive region.");
        }

        return new ScreenRect(x1, y1, width, height);
    }

    private async Task<ScreenFrame> DecodeImageAssetAsync(
        int stepNumber,
        string command,
        string imageName,
        IDictionary<string, string>? imageAssets,
        CancellationToken cancellationToken)
    {
        if (imageAssets is null || !imageAssets.TryGetValue(imageName, out var base64Png))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: image asset '{imageName}' is not defined.");
        }

        try
        {
            return await _imageAssetCodec.DecodeBase64PngAsync(base64Png, imageName, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException ex) when (ex.Message.Contains("not valid Base64", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: image asset '{imageName}' is not valid Base64.", ex);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: image asset '{imageName}' is not a supported PNG: {ex.Message}", ex);
        }
    }

    private static ImageSearchVariableLayout GetImageSearchVariableLayout(string[] parts, int startIndex)
    {
        var index = startIndex;
        var variableNames = new List<string>(capacity: 3);
        while (index < parts.Length && !RunScriptScreenReadingStepParser.IsImageSearchOptionKeyword(parts[index]))
        {
            variableNames.Add(parts[index]);
            index++;
        }

        return variableNames.Count is 3
            ? new ImageSearchVariableLayout(variableNames[0], variableNames[1], variableNames[2], 3)
            : new ImageSearchVariableLayout(FoundVariableName: null, XVariableName: null, YVariableName: null, 0);
    }

    private static ImageSearchVariableLayout GetImageClickVariableLayout(string[] parts, int startIndex)
    {
        var index = startIndex;
        var variableNames = new List<string>(capacity: 3);
        while (index < parts.Length && !IsImageClickOptionKeyword(parts[index]))
        {
            variableNames.Add(parts[index]);
            index++;
        }

        return variableNames.Count is 3
            ? new ImageSearchVariableLayout(variableNames[0], variableNames[1], variableNames[2], 3)
            : new ImageSearchVariableLayout(FoundVariableName: null, XVariableName: null, YVariableName: null, 0);
    }

    private static bool IsImageClickOptionKeyword(string value)
    {
        return RunScriptScreenReadingStepParser.IsImageSearchOptionKeyword(value)
            || string.Equals(value, "button", StringComparison.OrdinalIgnoreCase);
    }

    private static ScreenImageMatchOptions ParseImageSearchOptions(
        int stepNumber,
        string command,
        string[] parts,
        int startIndex,
        ScreenRect? region)
    {
        var similarity = 0.95;
        var selectionMode = ScreenImageMatchSelectionMode.Automatic;
        var hasSimilarity = false;
        var hasTimeout = false;
        var hasButton = false;
        var hasMatchMode = false;
        for (var index = startIndex; index < parts.Length;)
        {
            if (RunScriptSyntax.IsImageSearchSimilarityKeyword(parts[index]))
            {
                if (hasSimilarity)
                {
                    throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: duplicate similarity option.");
                }

                if (index + 1 >= parts.Length
                    || !double.TryParse(parts[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out similarity)
                    || !double.IsFinite(similarity)
                    || similarity is < 0.0 or > 1.0)
                {
                    throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: similarity must be a finite number between 0.0 and 1.0.");
                }

                hasSimilarity = true;
                index += 2;
                continue;
            }

            if (RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(parts[index]))
            {
                if (hasMatchMode)
                {
                    throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: duplicate matchmode option.");
                }

                if (index + 1 >= parts.Length || !RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out var parsedMode))
                {
                    throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: matchmode must be auto, first, or best.");
                }

                selectionMode = parsedMode switch
                {
                    EditorImageMatchMode.Automatic => ScreenImageMatchSelectionMode.Automatic,
                    EditorImageMatchMode.BestMatch => ScreenImageMatchSelectionMode.BestMatch,
                    EditorImageMatchMode.FirstThresholdMatch => ScreenImageMatchSelectionMode.FirstThresholdMatch,
                    _ => throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: matchmode is invalid."),
                };
                hasMatchMode = true;
                index += 2;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
            {
                if (hasTimeout)
                {
                    throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: duplicate timeout option.");
                }

                if (index + 1 >= parts.Length || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
                {
                    throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: timeout must be an integer of at least 0 milliseconds.");
                }

                hasTimeout = true;
                index += 2;
                continue;
            }

            if (string.Equals(parts[index], "button", StringComparison.OrdinalIgnoreCase))
            {
                if (hasButton)
                {
                    throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: duplicate button option.");
                }

                if (index + 1 >= parts.Length || !IsImageClickButton(parts[index + 1]))
                {
                    throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: button must be left, right, or middle.");
                }

                hasButton = true;
                index += 2;
                continue;
            }

            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: unknown image option '{parts[index]}'.");
        }

        return ScreenImageMatchOptions.Create(region, similarity, selectionMode);
    }

    private static bool IsImageClickButton(string value)
    {
        return string.Equals(value, "left", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "right", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase);
    }

    private static ScreenReadOptions CreateSingleCaptureOptions(CancellationToken cancellationToken)
    {
        return new ScreenReadOptions(
            ScreenReadOptions.DefaultTimeout,
            pollInterval: null,
            pollUntilMatch: false,
            cancellationToken);
    }

    private static ScreenReadOptions CreateWaitingOptions(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        return new ScreenReadOptions(
            timeout ?? ScreenReadOptions.DefaultTimeout,
            ScreenReadOptions.DefaultPollInterval,
            pollUntilMatch: true,
            cancellationToken);
    }

    private static Task<ScreenReadResult<ScreenImageMatch>> SearchImageUntilConsistentAsync(
        IScreenImageSearchReader reader,
        ScreenRect? region,
        ScreenFrame template,
        ScreenImageMatchOptions matchOptions,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return ScreenReadPolling.PollImageUntilConsistentAsync(
            (remaining, token) => reader.SearchImageAsync(
                region,
                template,
                matchOptions,
                new ScreenReadOptions(
                    remaining,
                    pollInterval: null,
                    pollUntilMatch: false,
                    token)),
            timeout,
            ScreenReadOptions.DefaultPollInterval,
            cancellationToken);
    }

    private static void EnsureSuccess<T>(int stepNumber, string command, ScreenReadResult<T> result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        if (result.ErrorKind is ScreenReadErrorKind.Canceled)
        {
            throw new OperationCanceledException(result.ErrorMessage);
        }

        var message = result.ErrorMessage ?? "Unknown screen read error.";
        throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: {result.ErrorKind}: {message}");
    }

    private static bool CanStoreResultVariable<T>(ScreenReadResult<T> result)
    {
        return result.IsSuccess || result.ErrorKind is ScreenReadErrorKind.CaptureTimeout;
    }

    private static void StoreImageSearchVariables(
        IDictionary<string, string> runtimeVariables,
        ImageSearchVariableLayout variableLayout,
        bool found,
        ScreenPoint point)
    {
        if (variableLayout.FoundVariableName is null)
        {
            return;
        }

        runtimeVariables[variableLayout.FoundVariableName] = found ? "true" : "false";
        runtimeVariables[variableLayout.XVariableName!] = found
            ? point.X.ToString(CultureInfo.InvariantCulture)
            : "-1";
        runtimeVariables[variableLayout.YVariableName!] = found
            ? point.Y.ToString(CultureInfo.InvariantCulture)
            : "-1";
    }

    private static TimeSpan? ParseImageTimeout(string[] parts, int startIndex)
    {
        for (var index = startIndex; index < parts.Length; index++)
        {
            if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
            {
                if (index + 1 >= parts.Length || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
                {
                    throw new InvalidOperationException("Image timeout must be an integer of at least 0 milliseconds.");
                }

                return TimeSpan.FromMilliseconds(timeoutMs);
            }
        }

        return null;
    }

    private static TimeSpan? ParseScreenReadTimeout(string[] parts, int variableIndex)
    {
        var startIndex = variableIndex;
        if (startIndex < parts.Length && !RunScriptScreenReadingStepParser.IsScreenReadTimeoutKeyword(parts[startIndex]))
        {
            startIndex++;
        }

        for (var index = startIndex; index < parts.Length; index++)
        {
            if (RunScriptScreenReadingStepParser.IsScreenReadTimeoutKeyword(parts[index]))
            {
                return TimeSpan.FromMilliseconds(ParseInteger(parts[index + 1]));
            }
        }

        return null;
    }

    private static MacroMouseButton ParseImageClickButton(string[] parts, int startIndex)
    {
        for (var index = startIndex; index < parts.Length - 1; index++)
        {
            if (!string.Equals(parts[index], "button", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= parts.Length)
            {
                throw new InvalidOperationException("Image click button requires left, right, or middle.");
            }

            return parts[index + 1].ToUpperInvariant() switch
            {
                "RIGHT" => MacroMouseButton.Right,
                "MIDDLE" => MacroMouseButton.Middle,
                "LEFT" => MacroMouseButton.Left,
                _ => throw new InvalidOperationException("Image click button must be left, right, or middle."),
            };
        }

        return MacroMouseButton.Left;
    }

    private static int ParseInteger(string value)
    {
        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static ScreenPixelColor ResolveTargetColor(
        string token,
        int stepNumber,
        IDictionary<string, string> runtimeVariables)
    {
        if (ScreenPixelColor.TryParse(token, out var color))
        {
            return color;
        }

        if (!token.StartsWith('$'))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: invalid color token '{token}'. Expected RRGGBB or $variable.");
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(token);
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: invalid color variable '{token}'. Expected $variable.");
        }

        if (!runtimeVariables.TryGetValue(variableName, out var value))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: color variable '{variableName}' is not defined.");
        }

        if (!ScreenPixelColor.TryParse(value, out color))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: color variable '{variableName}' value '{value}' is invalid. Expected RRGGBB.");
        }

        return color;
    }

    private static PixelSearchVariableLayout GetPixelSearchVariableLayout(string[] parts) =>
        RunScriptScreenReadingStepParser.GetPixelSearchVariableLayout(parts);

    private static int ParsePixelSearchTolerance(string[] parts)
    {
        for (var index = GetPixelSearchOptionStartIndex(parts); index < parts.Length - 1; index++)
        {
            if (RunScriptScreenReadingStepParser.IsPixelSearchToleranceKeyword(parts[index]))
            {
                return ParseInteger(parts[index + 1]);
            }
        }

        return 0;
    }

    private static int GetPixelSearchOptionStartIndex(string[] parts)
    {
        var variableLayout = GetPixelSearchVariableLayout(parts);
        if (variableLayout.FoundVariableName is not null)
        {
            return 9;
        }

        return variableLayout.XVariableName is not null ? 8 : 6;
    }

    private readonly record struct ImageSearchVariableLayout(
        string? FoundVariableName,
        string? XVariableName,
        string? YVariableName,
        int VariableCount);
}
