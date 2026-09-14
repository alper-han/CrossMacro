
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptScreenReadExecutor(
    IScreenPixelReader screenPixelReader,
    IMousePositionProvider? mousePositionProvider,
    Func<MacroEvent, CancellationToken, Task>? executeEventAsync = null,
    IImageClickMovementResolver? imageClickMovementResolver = null,
    IInputSimulator? inputSimulator = null,
    IImageAssetCodec? imageAssetCodec = null,
    Func<CancellationToken, Task>? flushPendingCursorMovementAsync = null,
    TimeProvider? timeProvider = null)
{
    private readonly IScreenPixelReader _screenPixelReader = screenPixelReader ?? throw new ArgumentNullException(nameof(screenPixelReader));
    private readonly IMousePositionProvider? _mousePositionProvider = mousePositionProvider;
    private readonly Func<MacroEvent, CancellationToken, Task>? _executeEventAsync = executeEventAsync;
    private readonly IImageClickMovementResolver? _imageClickMovementResolver = imageClickMovementResolver;
    private readonly IInputSimulator? _inputSimulator = inputSimulator;
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();
    private readonly Func<CancellationToken, Task>? _flushPendingCursorMovementAsync = flushPendingCursorMovementAsync;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

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

        if (!RunScriptScreenReadingStepParser.TryParseStep(trimmedStep, out var parsed, out var validationError))
        {
            return;
        }
        if (validationError is not null)
        {
            _ = RunScriptScreenReadingStepParser.TryParseCommand(trimmedStep, out var command, out _);
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: {validationError}");
        }
        switch (parsed)
        {
            case ParsedScreenReadStep.PixelColor pixel:
                await ExecutePixelColorAsync(stepNumber, pixel, runtimeVariables, cancellationToken).ConfigureAwait(false);
                break;
            case ParsedScreenReadStep.WaitColor wait:
                await ExecuteWaitColorAsync(stepNumber, wait, runtimeVariables, cancellationToken).ConfigureAwait(false);
                break;
            case ParsedScreenReadStep.PixelSearch search:
                await ExecutePixelSearchAsync(stepNumber, search, runtimeVariables, cancellationToken).ConfigureAwait(false);
                break;
            case ParsedScreenReadStep.Image image when image.Command is RunScriptScreenReadingCommand.ImageSearch:
                await ExecuteImageSearchAsync(stepNumber, image, runtimeVariables, imageAssets, cancellationToken).ConfigureAwait(false);
                break;
            case ParsedScreenReadStep.Image image when image.Command is RunScriptScreenReadingCommand.ImageClick:
                await ExecuteImageClickAsync(stepNumber, image, runtimeVariables, imageAssets, cancellationToken).ConfigureAwait(false);
                break;
            case ParsedScreenReadStep.Image image:
                await ExecuteWaitImageAsync(stepNumber, image, runtimeVariables, imageAssets, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    internal static bool IsScreenReadingStep(string step)
    {
        return RunScriptSyntax.IsScreenReadingStep(step);
    }

    private async Task ExecutePixelColorAsync(
        int stepNumber,
        ParsedScreenReadStep.PixelColor step,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var point = step.Relative
            ? await ResolveRelativePointAsync(stepNumber, step.X, step.Y, cancellationToken).ConfigureAwait(false)
            : new ScreenPoint(step.X, step.Y);

        var result = await _screenPixelReader.GetPixelAsync(point, CreateSingleCaptureOptions(cancellationToken)).ConfigureAwait(false);
        EnsureSuccess(stepNumber, "pixelcolor", result);

        if (step.ResultVariable is { } variable)
        {
            runtimeVariables[variable] = result.Value.ToString();
        }
    }

    private async Task ExecuteWaitColorAsync(
        int stepNumber,
        ParsedScreenReadStep.WaitColor step,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var point = new ScreenPoint(step.X, step.Y);
        var expected = ResolveTargetColor(step.ColorToken, stepNumber, runtimeVariables);
        var timeout = ToTimeout(step.TimeoutMs);
        var resultVariable = step.ResultVariable;

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
        ParsedScreenReadStep.PixelSearch step,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var x1 = step.X1;
        var y1 = step.Y1;
        var x2 = step.X2;
        var y2 = step.Y2;
        var expected = ResolveTargetColor(step.ColorToken, stepNumber, runtimeVariables);
        var tolerance = step.Tolerance;
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

        var timeout = ToTimeout(step.TimeoutMs);
        var result = await _screenPixelReader.SearchPixelAsync(region, expected, tolerance, CreateWaitingOptions(timeout, cancellationToken)).ConfigureAwait(false);
        var variableLayout = step.Variables;
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

        var position = await _mousePositionProvider.GetAbsolutePositionAsync()
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: pixelcolor rel failed: current mouse position is unavailable.");
        return new ScreenPoint(checked(position.X + dx), checked(position.Y + dy));
    }

    private async Task ExecuteImageSearchAsync(
        int stepNumber,
        ParsedScreenReadStep.Image step,
        IDictionary<string, string> runtimeVariables,
        IDictionary<string, string>? imageAssets,
        CancellationToken cancellationToken)
    {
        if (_screenPixelReader is not IScreenImageSearchReader imageSearchReader)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: imagesearch failed: screen image matching is not available for provider '{_screenPixelReader.ProviderName}'.");
        }

        var region = step.Region is { } imageRegion
            ? ResolveImageSearchRegion(stepNumber, imageRegion, runtimeVariables, "imagesearch")
            : (ScreenRect?)null;
        using var template = await DecodeImageAssetAsync(stepNumber, "imagesearch", step.ImageName, imageAssets, cancellationToken).ConfigureAwait(false);
        var variableLayout = step.Variables;
        var matchOptions = CreateImageSearchOptions(step, region);
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

    private async Task ExecuteImageClickAsync(
        int stepNumber,
        ParsedScreenReadStep.Image step,
        IDictionary<string, string> runtimeVariables,
        IDictionary<string, string>? imageAssets,
        CancellationToken cancellationToken)
    {
        if (_executeEventAsync is null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: imageclick failed: input playback is not available in this runtime path.");
        }

        var imageSearchReader = GetImageSearchReader(stepNumber, "imageclick");
        var region = step.Region is { } imageRegion
            ? ResolveImageSearchRegion(stepNumber, imageRegion, runtimeVariables, "imageclick")
            : (ScreenRect?)null;
        using var template = await DecodeImageAssetAsync(stepNumber, "imageclick", step.ImageName, imageAssets, cancellationToken).ConfigureAwait(false);
        var variableLayout = step.Variables;
        var matchOptions = CreateImageSearchOptions(step, region);
        var timeout = ToTimeout(step.TimeoutMs) ?? ScreenReadOptions.DefaultTimeout;
        var button = step.Button;

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
        ParsedScreenReadStep.Image step,
        IDictionary<string, string> runtimeVariables,
        IDictionary<string, string>? imageAssets,
        CancellationToken cancellationToken)
    {
        var imageSearchReader = GetImageSearchReader(stepNumber, "waitimage");
        var region = step.Region is { } imageRegion
            ? ResolveImageSearchRegion(stepNumber, imageRegion, runtimeVariables, "waitimage")
            : (ScreenRect?)null;
        using var template = await DecodeImageAssetAsync(stepNumber, "waitimage", step.ImageName, imageAssets, cancellationToken).ConfigureAwait(false);
        var variableLayout = step.Variables;
        var matchOptions = CreateImageSearchOptions(step, region);
        var timeout = ToTimeout(step.TimeoutMs) ?? ScreenReadOptions.DefaultTimeout;
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

    private static ScreenRect ResolveImageSearchRegion(
        int stepNumber,
        ParsedScreenReadStep.ImageRegion region,
        IDictionary<string, string> runtimeVariables,
        string command)
    {
        if (region is ParsedScreenReadStep.ExplicitRegion explicitRegion)
        {
            var left = ResolveImageRegionInteger(explicitRegion.Left, "left", stepNumber, command, runtimeVariables);
            var top = ResolveImageRegionInteger(explicitRegion.Top, "top", stepNumber, command, runtimeVariables);
            var explicitWidth = ResolveImageRegionInteger(explicitRegion.Width, "width", stepNumber, command, runtimeVariables);
            var explicitHeight = ResolveImageRegionInteger(explicitRegion.Height, "height", stepNumber, command, runtimeVariables);
            if (explicitWidth <= 0 || explicitHeight <= 0)
            {
                throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: region width and height must be >= 1.");
            }

            if ((long)left + explicitWidth > int.MaxValue
                || (long)left + explicitWidth < int.MinValue
                || (long)top + explicitHeight > int.MaxValue
                || (long)top + explicitHeight < int.MinValue)
            {
                throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: region endpoint exceeds the supported screen coordinate range.");
            }

            return new ScreenRect(left, top, explicitWidth, explicitHeight);
        }

        var legacy = (ParsedScreenReadStep.LegacyRegion)region;
        return new ScreenRect(legacy.Left, legacy.Top, legacy.Right - legacy.Left, legacy.Bottom - legacy.Top);
    }

    private static int ResolveImageRegionInteger(
        string token,
        string description,
        int stepNumber,
        string command,
        IDictionary<string, string> runtimeVariables)
    {
        var resolved = RunScriptRuntimeText.ResolveVariables(token, runtimeVariables, $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: ");
        if (!int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {command} failed: region {description} '{resolved}' is not an integer.");
        }

        return value;
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

    private static ScreenImageMatchOptions CreateImageSearchOptions(ParsedScreenReadStep.Image step, ScreenRect? region) =>
        ScreenImageMatchOptions.Create(region, step.Similarity, step.MatchMode switch
        {
            EditorImageMatchMode.Automatic => ScreenImageMatchSelectionMode.Automatic,
            EditorImageMatchMode.FirstThresholdMatch => ScreenImageMatchSelectionMode.FirstThresholdMatch,
            EditorImageMatchMode.BestMatch => ScreenImageMatchSelectionMode.BestMatch,
            _ => throw new ArgumentOutOfRangeException(nameof(step), step.MatchMode, "Image match mode is invalid."),
        });

    private static TimeSpan? ToTimeout(int? milliseconds) => milliseconds is { } value ? TimeSpan.FromMilliseconds(value) : null;

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

    private Task<ScreenReadResult<ScreenImageMatch>> SearchImageUntilConsistentAsync(
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
            cancellationToken,
            _timeProvider);
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
        PixelSearchVariableLayout variableLayout,
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

}
