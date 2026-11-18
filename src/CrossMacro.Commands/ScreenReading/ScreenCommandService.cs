
namespace CrossMacro.Commands.ScreenReading;

public sealed class ScreenCommandService(
    IScreenPixelReader? screenPixelReader,
    IMousePositionProvider? mousePositionProvider,
    IScreenImageAutomation? imageAutomation = null) : IScreenCommandService
{
    private readonly IScreenPixelReader? _screenPixelReader = screenPixelReader;
    private readonly IMousePositionProvider? _mousePositionProvider = mousePositionProvider;
    private readonly IScreenImageAutomation? _imageAutomation = imageAutomation ?? screenPixelReader as IScreenImageAutomation;

    public async Task<OperationResult> ExecuteAsync(ScreenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Action is ScreenAction.SearchImage && options.ImagePath is { Length: > 0 })
        {
            return await SearchImageAsync(options, cancellationToken).ConfigureAwait(false);
        }
        if (options.Action is ScreenAction.WaitImage && options.ImagePath is { Length: > 0 })
        {
            return await WaitImageAsync(options, cancellationToken).ConfigureAwait(false);
        }
        if (options.Action is ScreenAction.ImageClick && options.ImagePath is { Length: > 0 })
        {
            return await ImageClickAsync(options, cancellationToken).ConfigureAwait(false);
        }

        if (!TryGetScreenPixelReader(out var reader, out var unsupported))
        {
            return unsupported;
        }

        return options.Action switch
        {
            ScreenAction.Pixel => await PixelAsync(reader, options, cancellationToken).ConfigureAwait(false),
            ScreenAction.WaitColor when options.ExpectedColor is ScreenPixelColor expected => await WaitColorAsync(reader, options, expected, cancellationToken).ConfigureAwait(false),
            ScreenAction.SearchColor when options.ExpectedColor is ScreenPixelColor expected && options.X2 is int x2 && options.Y2 is int y2 && options.X != x2 && options.Y != y2 => await SearchColorAsync(reader, options, expected, x2, y2, cancellationToken).ConfigureAwait(false),
            ScreenAction.WaitColor or ScreenAction.SearchColor or ScreenAction.SearchImage or ScreenAction.WaitImage or ScreenAction.ImageClick => InvalidOptions(options.Action),
            _ => OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Unknown screen action."),
        };
    }

    private async Task<OperationResult> PixelAsync(IScreenPixelReader reader, ScreenOptions options, CancellationToken cancellationToken)
    {
        var point = await ResolvePointAsync(options, cancellationToken).ConfigureAwait(false);
        if (point.Error is { } error)
        {
            return error;
        }

        var result = await reader.GetPixelAsync(
            point.Point,
            new ScreenReadOptions(ScreenReadOptions.DefaultTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ToFailure("Failed to read screen pixel.", result);
        }

        var data = new ScreenPixelData(point.Point.X, point.Point.Y, result.Value.ToString(), reader.ProviderName, options.Relative);
        return OperationResult.Ok($"Pixel {point.Point.X.ToString(CultureInfo.InvariantCulture)},{point.Point.Y.ToString(CultureInfo.InvariantCulture)}: {data.Color}", data);
    }

    private static async Task<OperationResult> WaitColorAsync(IScreenPixelReader reader, ScreenOptions options, ScreenPixelColor expected, CancellationToken cancellationToken)
    {
        var point = new ScreenPoint(options.X, options.Y);
        var result = await reader.WaitForPixelAsync(point, expected, CreateWaitingOptions(options, cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ToFailure("Failed while waiting for screen color.", result);
        }

        var data = new ScreenWaitColorData(point.X, point.Y, expected.ToString(), result.Value.ToString(), reader.ProviderName, Matched: true, options.TimeoutMs);
        return OperationResult.Ok($"Pixel {point.X.ToString(CultureInfo.InvariantCulture)},{point.Y.ToString(CultureInfo.InvariantCulture)} matched {expected}.", data);
    }

    private static async Task<OperationResult> SearchColorAsync(IScreenPixelReader reader, ScreenOptions options, ScreenPixelColor expected, int x2, int y2, CancellationToken cancellationToken)
    {
        var left = Math.Min(options.X, x2);
        var top = Math.Min(options.Y, y2);
        var region = new ScreenRect(left, top, checked(Math.Max(options.X, x2) - left), checked(Math.Max(options.Y, y2) - top));
        var result = await reader.SearchPixelAsync(region, expected, options.Tolerance, CreateWaitingOptions(options, cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ToFailure("Failed while searching for screen color.", result);
        }

        var match = result.Value;
        var data = new ScreenSearchColorData(Found: true, match.Point.X, match.Point.Y, match.Color.ToString(), expected.ToString(), region.X, region.Y, region.Width, region.Height, options.Tolerance, reader.ProviderName);
        return OperationResult.Ok($"Color {expected} found at {match.Point.X.ToString(CultureInfo.InvariantCulture)},{match.Point.Y.ToString(CultureInfo.InvariantCulture)}.", data);
    }

    private async Task<OperationResult> SearchImageAsync(ScreenOptions options, CancellationToken cancellationToken)
    {
        var request = CreateImageRequest(options, out var invalid);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!TryGetImageAutomation(out var automation, out var unsupported))
        {
            return unsupported;
        }

        var result = await automation.SearchAsync(request!, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return ImageFound("Image found", result, options, request!.Region, automation.ProviderName);
        }

        if (result.ErrorKind is ScreenReadErrorKind.CaptureTimeout)
        {
            return OperationResult.Ok("Image was not found.", CreateSearchImageData(found: false, x: null, y: null, score: null, options, request!.Region, automation.ProviderName), [result.ErrorMessage ?? "No matching image was found."]);
        }
        if (result.ErrorKind is ScreenReadErrorKind.InvalidArguments)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, result.ErrorMessage ?? "Invalid image search arguments.", [result.ErrorMessage ?? "Invalid image search arguments."]);
        }
        return ToFailure("Failed while searching for screen image.", result);
    }

    private async Task<OperationResult> WaitImageAsync(ScreenOptions options, CancellationToken cancellationToken)
    {
        var request = CreateImageRequest(options, out var invalid);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!TryGetImageAutomation(out var automation, out var unsupported))
        {
            return unsupported;
        }

        var result = await automation.WaitAsync(request!, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return ImageFound("Image appeared", result, options, request!.Region, automation.ProviderName);
        }

        if (result.ErrorKind is ScreenReadErrorKind.CaptureTimeout)
        {
            return OperationResult.Ok("Image did not appear before timeout.", CreateSearchImageData(found: false, x: null, y: null, score: null, options, request!.Region, automation.ProviderName), [result.ErrorMessage ?? "No matching image was found."]);
        }
        if (result.ErrorKind is ScreenReadErrorKind.InvalidArguments)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, result.ErrorMessage ?? "Invalid image wait arguments.", [result.ErrorMessage ?? "Invalid image wait arguments."]);
        }
        return ToFailure("Failed while waiting for screen image.", result);
    }

    private async Task<OperationResult> ImageClickAsync(ScreenOptions options, CancellationToken cancellationToken)
    {
        var request = CreateImageRequest(options, out var invalid);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!TryGetImageAutomation(out var automation, out var unsupported))
        {
            return unsupported;
        }

        var result = await automation.ClickAsync(request!, ToMouseButtonCode(options.Button), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            if (result.ErrorKind is ScreenReadErrorKind.Unsupported)
            {
                var message = result.ErrorMessage ?? "Screen image click movement is unavailable in the current session.";
                return OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, message, [message]);
            }
            if (result.ErrorKind is ScreenReadErrorKind.InvalidArguments)
            {
                return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, result.ErrorMessage ?? "Invalid image click arguments.", [result.ErrorMessage ?? "Invalid image click arguments."]);
            }
            return ToFailure("Failed while searching for screen image to click.", result);
        }
        var point = result.Point!.Value;
        var data = new ScreenImageClickData(point.X, point.Y, result.Score!.Value, Path.GetFullPath(options.ImagePath!), request!.Region?.X, request.Region?.Y, request.Region?.Width, request.Region?.Height, options.Similarity, ScreenImageMatchModeCodec.Format(options.MatchMode), options.Button.ToString(), automation.ProviderName);
        return OperationResult.Ok($"Image clicked at {point.X.ToString(CultureInfo.InvariantCulture)},{point.Y.ToString(CultureInfo.InvariantCulture)} with score {result.Score.Value.ToString("0.###", CultureInfo.InvariantCulture)}.", data);
    }

    private static ScreenImageAutomationRequest? CreateImageRequest(ScreenOptions options, out OperationResult? error)
    {
        if (!TryCreateRegion(options, out var region, out error))
        {
            return null;
        }

        if (!double.IsFinite(options.Similarity) || options.Similarity is < 0.0 or > 1.0)
        {
            error = InvalidOptions(options.Action);
            return null;
        }
        return new ScreenImageAutomationRequest(
            options.ImagePath!,
            region,
            options.Similarity,
            options.MatchMode,
            options.TimeoutMs is { } timeout ? TimeSpan.FromMilliseconds(timeout) : null);
    }

    private bool TryGetImageAutomation([NotNullWhen(true)] out IScreenImageAutomation? automation, [NotNullWhen(false)] out OperationResult? error)
    {
        if (_imageAutomation is null || !_imageAutomation.IsSupported)
        {
            automation = null;
            error = OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "Screen image matching is not supported in this runtime.", ["No supported IScreenImageAutomation is available for the current platform/session."]);
            return false;
        }
        automation = _imageAutomation;
        error = null;
        return true;
    }

    private bool TryGetScreenPixelReader([NotNullWhen(true)] out IScreenPixelReader? reader, [NotNullWhen(false)] out OperationResult? error)
    {
        if (_screenPixelReader is null || !_screenPixelReader.IsSupported)
        {
            reader = null;
            error = OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "Screen pixel reading is not supported in this runtime.", ["No supported IScreenPixelReader is available for the current platform/session."]);
            return false;
        }
        reader = _screenPixelReader;
        error = null;
        return true;
    }

    private async Task<PointResolutionResult> ResolvePointAsync(ScreenOptions options, CancellationToken cancellationToken)
    {
        if (!options.Relative)
        {
            return PointResolutionResult.Ok(new ScreenPoint(options.X, options.Y));
        }

        if (_mousePositionProvider is null || !_mousePositionProvider.IsSupported)
        {
            return PointResolutionResult.Fail(OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "Relative screen pixel reads are not supported in this runtime.", ["No supported IMousePositionProvider is available for the current platform/session."]));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
        return position is null
            ? PointResolutionResult.Fail(OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "Relative screen pixel reads are not supported in this runtime.", ["The current mouse position is unavailable."]))
            : PointResolutionResult.Ok(new ScreenPoint(checked(position.Value.X + options.X), checked(position.Value.Y + options.Y)));
    }

    private static bool TryCreateRegion(ScreenOptions options, out ScreenRect? region, out OperationResult? error)
    {
        error = null;
        if (ScreenRegionRules.TryCreate(options.RegionX, options.RegionY, options.RegionWidth, options.RegionHeight, "screen --region", out region, out var message))
        {
            return true;
        }

        error = OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, message);
        return false;
    }

    private static OperationResult ImageFound(string prefix, ScreenImageAutomationResult result, ScreenOptions options, ScreenRect? region, string providerName)
    {
        var point = result.Point!.Value;
        return OperationResult.Ok($"{prefix} at {point.X.ToString(CultureInfo.InvariantCulture)},{point.Y.ToString(CultureInfo.InvariantCulture)} with score {result.Score!.Value.ToString("0.###", CultureInfo.InvariantCulture)}.", CreateSearchImageData(found: true, point.X, point.Y, result.Score, options, region, providerName));
    }

    private static ScreenSearchImageData CreateSearchImageData(bool found, int? x, int? y, double? score, ScreenOptions options, ScreenRect? region, string providerName) =>
        new(found, x, y, score, Path.GetFullPath(options.ImagePath!), region?.X, region?.Y, region?.Width, region?.Height, options.Similarity, ScreenImageMatchModeCodec.Format(options.MatchMode), providerName);

    private static int ToMouseButtonCode(MacroMouseButton button)
    {
        if (button is MacroMouseButton.Right)
        {
            return MouseButtonCode.Right;
        }

        return button is MacroMouseButton.Middle ? MouseButtonCode.Middle : MouseButtonCode.Left;
    }

    private static ScreenReadOptions CreateWaitingOptions(ScreenOptions options, CancellationToken token) => new(
        options.TimeoutMs is { } timeout ? TimeSpan.FromMilliseconds(timeout) : ScreenReadOptions.DefaultTimeout,
        ScreenReadOptions.DefaultPollInterval,
        pollUntilMatch: true,
        cancellationToken: token);
    private static OperationResult InvalidOptions(ScreenAction action) => OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, $"Invalid options for screen action '{action}'.");

    private static OperationResult ToFailure<T>(string message, ScreenReadResult<T> result) =>
        ToFailure(message, result.ErrorKind, result.ErrorMessage);

    private static OperationResult ToFailure(string message, ScreenImageAutomationResult result) =>
        ToFailure(message, result.ErrorKind, result.ErrorMessage);

    private static OperationResult ToFailure(string message, ScreenReadErrorKind? errorKind, string? errorMessage) =>
        OperationResult.Fail(
            ScreenFailureOutcomes.ForReadError(errorKind),
            message,
            [errorMessage ?? errorKind?.ToString() ?? "Unknown screen read error."]);

    private readonly record struct PointResolutionResult(ScreenPoint Point, OperationResult? Error)
    {
        public static PointResolutionResult Ok(ScreenPoint point) => new(point, Error: null);
        public static PointResolutionResult Fail(OperationResult error) => new(default, error);
    }
}
