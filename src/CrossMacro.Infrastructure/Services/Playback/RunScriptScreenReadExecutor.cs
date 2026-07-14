using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Infrastructure.Services.ScreenReading;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptScreenReadExecutor
{
    private readonly IScreenPixelReader _screenPixelReader;
    private readonly IMousePositionProvider? _mousePositionProvider;
    private readonly Func<MacroEvent, CancellationToken, Task>? _executeEventAsync;
    private readonly IImageClickMovementResolver? _imageClickMovementResolver;
    private readonly IInputSimulator? _inputSimulator;
    private readonly IImageAssetCodec _imageAssetCodec;

    public RunScriptScreenReadExecutor(
        IScreenPixelReader screenPixelReader,
        IMousePositionProvider? mousePositionProvider,
        Func<MacroEvent, CancellationToken, Task>? executeEventAsync = null,
        IImageClickMovementResolver? imageClickMovementResolver = null,
        IInputSimulator? inputSimulator = null,
        IImageAssetCodec? imageAssetCodec = null)
    {
        _screenPixelReader = screenPixelReader ?? throw new ArgumentNullException(nameof(screenPixelReader));
        _mousePositionProvider = mousePositionProvider;
        _executeEventAsync = executeEventAsync;
        _imageClickMovementResolver = imageClickMovementResolver;
        _inputSimulator = inputSimulator;
        _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();
    }

    public async Task ExecuteAsync(
        MacroSequence macro,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(runtimeVariables);

        for (var i = 0; i < macro.ScriptSteps.Count; i++)
        {
            await ExecuteStepAsync(macro.ScriptSteps[i], i + 1, runtimeVariables, cancellationToken, macro.Images);
        }
    }

    public async Task ExecuteStepAsync(
        string step,
        int stepNumber,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? imageAssets = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(runtimeVariables);

        cancellationToken.ThrowIfCancellationRequested();

        var trimmedStep = step.Trim();
        if (trimmedStep.Length == 0)
        {
            return;
        }

        if (!RunScriptScreenReadingStepParser.TryParseCommand(trimmedStep, out var command, out var parts))
        {
            return;
        }

        if (command is RunScriptScreenReadingCommand.ImageSearch
            or RunScriptScreenReadingCommand.ImageClick
            or RunScriptScreenReadingCommand.WaitImage)
        {
            if (!RunScriptScreenReadingStepParser.TryValidateStep(trimmedStep, out var validationError)
                || validationError is not null)
            {
                throw new InvalidOperationException($"Step {stepNumber}: {command.ToString().ToLowerInvariant()} failed: {validationError ?? "invalid image command"}");
            }
        }

        if (command == RunScriptScreenReadingCommand.PixelColor)
        {
            await ExecutePixelColorAsync(stepNumber, parts, runtimeVariables, cancellationToken);
            return;
        }

        if (command == RunScriptScreenReadingCommand.WaitColor)
        {
            await ExecuteWaitColorAsync(stepNumber, parts, runtimeVariables, cancellationToken);
            return;
        }

        if (command == RunScriptScreenReadingCommand.PixelSearch)
        {
            await ExecutePixelSearchAsync(stepNumber, parts, runtimeVariables, cancellationToken);
            return;
        }

        if (command == RunScriptScreenReadingCommand.ImageSearch)
        {
            await ExecuteImageSearchAsync(stepNumber, parts, runtimeVariables, cancellationToken, imageAssets);
            return;
        }

        if (command == RunScriptScreenReadingCommand.ImageClick)
        {
            await ExecuteImageClickAsync(stepNumber, parts, runtimeVariables, cancellationToken, imageAssets);
            return;
        }

        if (command == RunScriptScreenReadingCommand.WaitImage)
        {
            await ExecuteWaitImageAsync(stepNumber, parts, runtimeVariables, cancellationToken, imageAssets);
        }
    }

    internal static bool IsScreenReadingStep(string step)
    {
        return RunScriptSyntax.IsScreenReadingStep(step);
    }

    private async Task ExecutePixelColorAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var isRelative = parts.Count > 1 && string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase);
        var coordinateIndex = isRelative ? 2 : 1;
        var x = ParseInteger(parts[coordinateIndex]);
        var y = ParseInteger(parts[coordinateIndex + 1]);
        var point = isRelative
            ? await ResolveRelativePointAsync(stepNumber, x, y, cancellationToken)
            : new ScreenPoint(x, y);

        var timeout = ParseScreenReadTimeout(parts, variableIndex: isRelative ? 4 : 3);
        var result = await _screenPixelReader.GetPixelAsync(point, CreateOptions(timeout, cancellationToken));
        EnsureSuccess(stepNumber, "pixelcolor", result);

        var variableIndex = isRelative ? 4 : 3;
        if (parts.Count > variableIndex && !RunScriptScreenReadingStepParser.IsScreenReadTimeoutKeyword(parts[variableIndex]))
        {
            runtimeVariables[parts[variableIndex]] = result.Value.ToString();
        }
    }

    private async Task ExecuteWaitColorAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var point = new ScreenPoint(ParseInteger(parts[1]), ParseInteger(parts[2]));
        var expected = ResolveTargetColor(parts[3], stepNumber, runtimeVariables);
        var timeout = parts.Count >= 5
            ? TimeSpan.FromMilliseconds(ParseInteger(parts[4]))
            : (TimeSpan?)null;
        var resultVariable = parts.Count >= 6 ? parts[5] : null;

        var result = await _screenPixelReader.WaitForPixelAsync(point, expected, CreateOptions(timeout, cancellationToken));
        if (resultVariable != null && CanStoreResultVariable(result))
        {
            runtimeVariables[resultVariable] = result.IsSuccess ? "true" : "false";
            return;
        }

        EnsureSuccess(stepNumber, "waitcolor", result);
    }

    private async Task ExecutePixelSearchAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
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
            throw new InvalidOperationException($"Step {stepNumber}: pixelsearch failed: bounds exceed the supported screen coordinate range.");
        }

        var width = (int)widthValue;
        var height = (int)heightValue;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Step {stepNumber}: pixelsearch failed: bounds must be end-exclusive and produce a positive region.");
        }

        var region = new ScreenRect(left, top, width, height);

        var timeout = ParseScreenReadTimeout(parts, GetPixelSearchOptionStartIndex(parts));
        var result = await _screenPixelReader.SearchPixelAsync(region, expected, tolerance, CreateOptions(timeout, cancellationToken));
        var variableLayout = GetPixelSearchVariableLayout(parts);
        if (variableLayout.FoundVariableName != null && CanStoreResultVariable(result))
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

        if (variableLayout.XVariableName != null)
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
            throw new InvalidOperationException($"Step {stepNumber}: pixelcolor rel failed: no mouse position provider is available.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var position = await _mousePositionProvider.GetAbsolutePositionAsync();
        if (position is null)
        {
            throw new InvalidOperationException($"Step {stepNumber}: pixelcolor rel failed: current mouse position is unavailable.");
        }

        return new ScreenPoint(checked(position.Value.X + dx), checked(position.Value.Y + dy));
    }

    private async Task ExecuteImageSearchAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? imageAssets)
    {
        if (_screenPixelReader is not IScreenImageSearchReader imageSearchReader)
        {
            throw new InvalidOperationException($"Step {stepNumber}: imagesearch failed: screen image matching is not available for provider '{_screenPixelReader.ProviderName}'.");
        }

		var hasRegion = HasImageSearchRegion(parts);
		var imageNameIndex = hasRegion ? 5 : 1;
		var region = hasRegion ? ParseImageSearchRegion(stepNumber, parts) : (ScreenRect?)null;
		var imageName = parts[imageNameIndex];
		using var template = DecodeImageAsset(stepNumber, "imagesearch", imageName, imageAssets);
		var variableLayout = GetImageSearchVariableLayout(parts, imageNameIndex + 1);
		var matchOptions = ParseImageSearchOptions(stepNumber, "imagesearch", parts, imageNameIndex + 1 + variableLayout.VariableCount, region);
		var timeout = ParseImageTimeout(parts, imageNameIndex + 1 + variableLayout.VariableCount);

        cancellationToken.ThrowIfCancellationRequested();
        var result = await imageSearchReader.SearchImageAsync(region, template, matchOptions, CreateOptions(timeout, cancellationToken));
        if (variableLayout.FoundVariableName != null && CanStoreResultVariable(result))
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
            throw new InvalidOperationException($"Step {stepNumber}: {command} failed: screen image matching is not available for provider '{_screenPixelReader.ProviderName}'.");
        }

        return imageSearchReader;
    }

    private static bool HasImageSearchRegion(IReadOnlyList<string> parts)
    {
        return parts.Count >= 6
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private async Task ExecuteImageClickAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? imageAssets)
    {
        if (_executeEventAsync is null)
        {
            throw new InvalidOperationException($"Step {stepNumber}: imageclick failed: input playback is not available in this runtime path.");
        }

        var imageSearchReader = GetImageSearchReader(stepNumber, "imageclick");
		var hasRegion = HasImageSearchRegion(parts);
		var imageNameIndex = hasRegion ? 5 : 1;
		var region = hasRegion ? ParseImageSearchRegion(stepNumber, parts, "imageclick") : (ScreenRect?)null;
		var imageName = parts[imageNameIndex];
		using var template = DecodeImageAsset(stepNumber, "imageclick", imageName, imageAssets);
		var variableLayout = GetImageClickVariableLayout(parts, imageNameIndex + 1);
		var optionStartIndex = imageNameIndex + 1 + variableLayout.VariableCount;
		var matchOptions = ParseImageSearchOptions(stepNumber, "imageclick", parts, optionStartIndex, region);
		var timeout = ParseImageTimeout(parts, optionStartIndex);
        var button = ParseImageClickButton(parts, optionStartIndex);

        var result = await imageSearchReader.SearchImageAsync(region, template, matchOptions, CreateOptions(timeout, cancellationToken));
        if (!result.IsSuccess && variableLayout.FoundVariableName != null && result.ErrorKind == ScreenReadErrorKind.CaptureTimeout)
        {
            StoreImageSearchVariables(runtimeVariables, variableLayout, false, default);
            return;
        }

        EnsureSuccess(stepNumber, "imageclick", result);

        var clickPoint = new ScreenPoint(
            checked(result.Value.Point.X + (result.Value.MatchedWidth > 0 ? result.Value.MatchedWidth : template.LogicalBounds.Width) / 2),
            checked(result.Value.Point.Y + (result.Value.MatchedHeight > 0 ? result.Value.MatchedHeight : template.LogicalBounds.Height) / 2));
        StoreImageSearchVariables(runtimeVariables, variableLayout, true, clickPoint);
        if (_imageClickMovementResolver is null || _inputSimulator is null)
        {
            throw new ImageClickMovementUnsupportedException($"Step {stepNumber}: imageclick failed: input movement is not available in this runtime path.");
        }

        var movement = await _imageClickMovementResolver.ResolveAsync(_inputSimulator, clickPoint, cancellationToken).ConfigureAwait(false);
        if (!movement.IsSuccess)
        {
            throw new ImageClickMovementUnsupportedException($"Step {stepNumber}: imageclick failed: {movement.ErrorMessage}");
        }

        await _executeEventAsync(new MacroEvent
        {
            Type = EventType.Click,
            X = movement.X,
            Y = movement.Y,
            Button = button,
            CoordinateMode = movement.CoordinateMode
        }, cancellationToken);
    }

    private async Task ExecuteWaitImageAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? imageAssets)
    {
        var imageSearchReader = GetImageSearchReader(stepNumber, "waitimage");
		var hasRegion = HasImageSearchRegion(parts);
		var imageNameIndex = hasRegion ? 5 : 1;
		var region = hasRegion ? ParseImageSearchRegion(stepNumber, parts, "waitimage") : (ScreenRect?)null;
		var imageName = parts[imageNameIndex];
		using var template = DecodeImageAsset(stepNumber, "waitimage", imageName, imageAssets);
		var variableLayout = GetImageSearchVariableLayout(parts, imageNameIndex + 1);
		var optionStartIndex = imageNameIndex + 1 + variableLayout.VariableCount;
		var matchOptions = ParseImageSearchOptions(stepNumber, "waitimage", parts, optionStartIndex, region);
		var timeout = ParseImageTimeout(parts, optionStartIndex) ?? ScreenReadOptions.Default.Timeout ?? TimeSpan.FromSeconds(5);
        var pollInterval = ScreenReadOptions.Default.PollInterval ?? TimeSpan.FromMilliseconds(50);
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            var result = await imageSearchReader.SearchImageAsync(region, template, matchOptions, CreateOptions(remaining, cancellationToken));
            if (result.IsSuccess)
            {
                StoreImageSearchVariables(runtimeVariables, variableLayout, true, result.Value.Point);
                return;
            }

            if (result.ErrorKind != ScreenReadErrorKind.CaptureTimeout)
            {
                EnsureSuccess(stepNumber, "waitimage", result);
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                if (variableLayout.FoundVariableName != null)
                {
                    StoreImageSearchVariables(runtimeVariables, variableLayout, false, default);
                    return;
                }

                EnsureSuccess(stepNumber, "waitimage", result);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    private static ScreenRect ParseImageSearchRegion(int stepNumber, IReadOnlyList<string> parts, string command = "imagesearch")
    {
        var x1 = ParseInteger(parts[1]);
        var y1 = ParseInteger(parts[2]);
        var x2 = ParseInteger(parts[3]);
        var y2 = ParseInteger(parts[4]);
        var widthValue = (long)x2 - x1;
        var heightValue = (long)y2 - y1;
        if (widthValue > int.MaxValue || heightValue > int.MaxValue)
        {
            throw new InvalidOperationException($"Step {stepNumber}: {command} failed: bounds exceed the supported screen coordinate range.");
        }

        var width = (int)widthValue;
        var height = (int)heightValue;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Step {stepNumber}: {command} failed: bounds must be end-exclusive and produce a positive region.");
        }

        return new ScreenRect(x1, y1, width, height);
    }

    private ScreenFrame DecodeImageAsset(
        int stepNumber,
        string command,
        string imageName,
        IReadOnlyDictionary<string, string>? imageAssets)
    {
        if (imageAssets is null || !imageAssets.TryGetValue(imageName, out var base64Png))
        {
            throw new InvalidOperationException($"Step {stepNumber}: {command} failed: image asset '{imageName}' is not defined.");
        }

        try
        {
            return _imageAssetCodec.DecodeBase64Png(base64Png, imageName);
        }
        catch (InvalidDataException ex) when (ex.Message.Contains("not valid Base64", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Step {stepNumber}: {command} failed: image asset '{imageName}' is not valid Base64.", ex);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException($"Step {stepNumber}: {command} failed: image asset '{imageName}' is not a supported PNG: {ex.Message}", ex);
        }
    }

    private static ImageSearchVariableLayout GetImageSearchVariableLayout(IReadOnlyList<string> parts, int startIndex)
    {
        var index = startIndex;
        var variableNames = new List<string>(capacity: 3);
        while (index < parts.Count && !RunScriptScreenReadingStepParser.IsImageSearchOptionKeyword(parts[index]))
        {
            variableNames.Add(parts[index]);
            index++;
        }

        return variableNames.Count == 3
            ? new ImageSearchVariableLayout(variableNames[0], variableNames[1], variableNames[2], 3)
            : new ImageSearchVariableLayout(null, null, null, 0);
    }

    private static ImageSearchVariableLayout GetImageClickVariableLayout(IReadOnlyList<string> parts, int startIndex)
    {
        var index = startIndex;
        var variableNames = new List<string>(capacity: 3);
        while (index < parts.Count && !IsImageClickOptionKeyword(parts[index]))
        {
            variableNames.Add(parts[index]);
            index++;
        }

        return variableNames.Count == 3
            ? new ImageSearchVariableLayout(variableNames[0], variableNames[1], variableNames[2], 3)
            : new ImageSearchVariableLayout(null, null, null, 0);
    }

    private static bool IsImageClickOptionKeyword(string value)
    {
        return RunScriptScreenReadingStepParser.IsImageSearchOptionKeyword(value)
            || string.Equals(value, "button", StringComparison.OrdinalIgnoreCase);
    }

	private static ScreenImageMatchOptions ParseImageSearchOptions(
		int stepNumber,
		string command,
		IReadOnlyList<string> parts,
		int startIndex,
		ScreenRect? region)
	{
			var similarity = ScreenImageMatchOptions.Default.MinimumSimilarity;
				var downsample = ScreenImageMatchOptions.Default.DownsampleFactor;
            var selectionMode = ScreenImageMatchSelectionMode.FirstThresholdMatch;
            var scaleAware = false;
			var hasSimilarity = false;
			var hasDownsample = false;
			var hasTimeout = false;
			var hasButton = false;
			for (var index = startIndex; index < parts.Count;)
			{
				if (RunScriptSyntax.IsImageSearchSimilarityKeyword(parts[index]))
				{
					if (hasSimilarity)
					{
						throw new InvalidOperationException($"Step {stepNumber}: {command} failed: duplicate similarity option.");
					}

				if (index + 1 >= parts.Count
					|| !double.TryParse(parts[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out similarity)
					|| !double.IsFinite(similarity)
					|| similarity is < 0.0 or > 1.0)
				{
					throw new InvalidOperationException($"Step {stepNumber}: {command} failed: similarity must be a finite number between 0.0 and 1.0.");
					}

					hasSimilarity = true;
					index += 2;
				continue;
			}

					if (RunScriptSyntax.IsImageSearchDownsampleKeyword(parts[index]))
				{
					if (hasDownsample)
					{
						throw new InvalidOperationException($"Step {stepNumber}: {command} failed: duplicate downsample option.");
					}

				if (index + 1 >= parts.Count || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out downsample) || downsample < 1)
				{
					throw new InvalidOperationException($"Step {stepNumber}: {command} failed: downsample must be an integer of at least 1.");
					}

					hasDownsample = true;
					index += 2;
					continue;
				}

				if (RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(parts[index]))
				{
					if (index + 1 >= parts.Count || !RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out var parsedMode))
					{
						throw new InvalidOperationException($"Step {stepNumber}: {command} failed: matchmode must be first or best.");
					}

					selectionMode = parsedMode == EditorImageMatchMode.BestMatch
						? ScreenImageMatchSelectionMode.BestMatch
						: ScreenImageMatchSelectionMode.FirstThresholdMatch;
					index += 2;
					continue;
				}

                if (RunScriptSyntax.IsImageSearchScaleAwareKeyword(parts[index]))
                {
                    scaleAware = true;
                    index++;
                    continue;
                }

				if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
				{
					if (hasTimeout)
					{
						throw new InvalidOperationException($"Step {stepNumber}: {command} failed: duplicate timeout option.");
					}

				if (index + 1 >= parts.Count || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
				{
					throw new InvalidOperationException($"Step {stepNumber}: {command} failed: timeout must be an integer of at least 0 milliseconds.");
					}

					hasTimeout = true;
					index += 2;
				continue;
			}

			if (string.Equals(parts[index], "button", StringComparison.OrdinalIgnoreCase))
			{
				if (hasButton)
				{
					throw new InvalidOperationException($"Step {stepNumber}: {command} failed: duplicate button option.");
				}

				if (index + 1 >= parts.Count || !IsImageClickButton(parts[index + 1]))
				{
					throw new InvalidOperationException($"Step {stepNumber}: {command} failed: button must be left, right, or middle.");
				}

				hasButton = true;
				index += 2;
				continue;
			}

			throw new InvalidOperationException($"Step {stepNumber}: {command} failed: unknown image option '{parts[index]}'.");
        }

            return ScreenImageMatchOptions.Create(region, similarity, downsample, selectionMode) with { ScaleAware = scaleAware };
		}

	private static bool IsImageClickButton(string value)
	{
		return string.Equals(value, "left", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(value, "right", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase);
	}

    private static ScreenReadOptions CreateOptions(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        return new ScreenReadOptions(
            timeout ?? ScreenReadOptions.Default.Timeout,
            ScreenReadOptions.Default.PollInterval,
            cancellationToken);
    }

    private static void EnsureSuccess<T>(int stepNumber, string command, ScreenReadResult<T> result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        if (result.ErrorKind == ScreenReadErrorKind.Canceled)
        {
            throw new OperationCanceledException(result.ErrorMessage);
        }

        var message = result.ErrorMessage ?? "Unknown screen read error.";
        throw new InvalidOperationException($"Step {stepNumber}: {command} failed: {result.ErrorKind}: {message}");
    }

    private static bool CanStoreResultVariable<T>(ScreenReadResult<T> result)
    {
        return result.IsSuccess || result.ErrorKind == ScreenReadErrorKind.CaptureTimeout;
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

	private static TimeSpan? ParseImageTimeout(IReadOnlyList<string> parts, int startIndex)
	{
		for (var index = startIndex; index < parts.Count; index++)
		{
			if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
			{
				if (index + 1 >= parts.Count || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
				{
					throw new InvalidOperationException("Image timeout must be an integer of at least 0 milliseconds.");
				}

				return TimeSpan.FromMilliseconds(timeoutMs);
            }
        }

        return null;
    }

    private static TimeSpan? ParseScreenReadTimeout(IReadOnlyList<string> parts, int variableIndex)
    {
        var startIndex = variableIndex;
        if (startIndex < parts.Count && !RunScriptScreenReadingStepParser.IsScreenReadTimeoutKeyword(parts[startIndex]))
        {
            startIndex++;
        }

		for (var index = startIndex; index < parts.Count; index++)
        {
            if (RunScriptScreenReadingStepParser.IsScreenReadTimeoutKeyword(parts[index]))
            {
                return TimeSpan.FromMilliseconds(ParseInteger(parts[index + 1]));
            }
        }

        return null;
    }

    private static MouseButton ParseImageClickButton(IReadOnlyList<string> parts, int startIndex)
    {
        for (var index = startIndex; index < parts.Count - 1; index++)
        {
			if (!string.Equals(parts[index], "button", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

			if (index + 1 >= parts.Count)
			{
				throw new InvalidOperationException("Image click button requires left, right, or middle.");
			}

			return parts[index + 1].ToLowerInvariant() switch
			{
				"right" => MouseButton.Right,
				"middle" => MouseButton.Middle,
				"left" => MouseButton.Left,
				_ => throw new InvalidOperationException("Image click button must be left, right, or middle.")
            };
        }

        return MouseButton.Left;
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

        if (!token.StartsWith("$", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Step {stepNumber}: invalid color token '{token}'. Expected RRGGBB or $variable.");
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(token);
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            throw new InvalidOperationException($"Step {stepNumber}: invalid color variable '{token}'. Expected $variable.");
        }

        if (!runtimeVariables.TryGetValue(variableName, out var value))
        {
            throw new InvalidOperationException($"Step {stepNumber}: color variable '{variableName}' is not defined.");
        }

        if (!ScreenPixelColor.TryParse(value, out color))
        {
            throw new InvalidOperationException($"Step {stepNumber}: color variable '{variableName}' value '{value}' is invalid. Expected RRGGBB.");
        }

        return color;
    }

    private static PixelSearchVariableLayout GetPixelSearchVariableLayout(IReadOnlyList<string> parts) =>
        RunScriptScreenReadingStepParser.GetPixelSearchVariableLayout(parts);

    private static bool HasPixelSearchVariables(IReadOnlyList<string> parts) =>
        GetPixelSearchVariableLayout(parts).XVariableName != null;

    private static int ParsePixelSearchTolerance(IReadOnlyList<string> parts)
    {
        for (var index = GetPixelSearchOptionStartIndex(parts); index < parts.Count - 1; index++)
        {
            if (RunScriptScreenReadingStepParser.IsPixelSearchToleranceKeyword(parts[index]))
            {
                return ParseInteger(parts[index + 1]);
            }
        }

        return 0;
    }

    private static int GetPixelSearchOptionStartIndex(IReadOnlyList<string> parts)
    {
        var variableLayout = GetPixelSearchVariableLayout(parts);
        if (variableLayout.FoundVariableName != null)
        {
            return 9;
        }

        return variableLayout.XVariableName != null ? 8 : 6;
    }

    private readonly record struct ImageSearchVariableLayout(
        string? FoundVariableName,
        string? XVariableName,
        string? YVariableName,
        int VariableCount);
}
