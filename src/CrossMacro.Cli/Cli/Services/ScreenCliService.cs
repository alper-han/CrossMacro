
namespace CrossMacro.Cli.Services;

public sealed class ScreenCliService : IScreenCliService
{
    private readonly IScreenPixelReader? _screenPixelReader;
    private readonly IMousePositionProvider? _mousePositionProvider;
    private readonly IScreenImageAutomation? _imageAutomation;

    public ScreenCliService(
        IScreenPixelReader? screenPixelReader,
        IMousePositionProvider? mousePositionProvider,
        IScreenImageAutomation? imageAutomation = null)
    {
        _screenPixelReader = screenPixelReader;
        _mousePositionProvider = mousePositionProvider;
        _imageAutomation = imageAutomation ?? screenPixelReader as IScreenImageAutomation;
    }

    public async Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken)
    {
        if (options.Action is ScreenCliAction.SearchImage && options.ImagePath is { Length: > 0 })
        {
            return await SearchImageAsync(options, cancellationToken).ConfigureAwait(false);
        }
        if (options.Action is ScreenCliAction.WaitImage && options.ImagePath is { Length: > 0 })
        {
            return await WaitImageAsync(options, cancellationToken).ConfigureAwait(false);
        }
        if (options.Action is ScreenCliAction.ImageClick && options.ImagePath is { Length: > 0 })
        {
            return await ImageClickAsync(options, cancellationToken).ConfigureAwait(false);
        }

        if (!TryGetScreenPixelReader(out var reader, out var unsupported))
        {
            return unsupported;
        }

        return options.Action switch
        {
            ScreenCliAction.Pixel => await PixelAsync(reader, options, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.WaitColor when options.ExpectedColor is ScreenPixelColor expected => await WaitColorAsync(reader, options, expected, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.SearchColor when options.ExpectedColor is ScreenPixelColor expected && options.X2 is int x2 && options.Y2 is int y2 && options.X != x2 && options.Y != y2 => await SearchColorAsync(reader, options, expected, x2, y2, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.WaitColor or ScreenCliAction.SearchColor or ScreenCliAction.SearchImage or ScreenCliAction.WaitImage or ScreenCliAction.ImageClick => InvalidOptions(options.Action),
            _ => CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown screen action."),
        };
    }

    private async Task<CliCommandExecutionResult> PixelAsync(IScreenPixelReader reader, ScreenCliOptions options, CancellationToken cancellationToken)
    {
        var point = await ResolvePointAsync(options, cancellationToken).ConfigureAwait(false);
        if (point.Error is { } error) return error;
        var result = await reader.GetPixelAsync(point.Point, CreateOptions(options.TimeoutMs, cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess) return ToFailure("Failed to read screen pixel.", result);
        var data = new ScreenPixelData(point.Point.X, point.Point.Y, result.Value.ToString(), reader.ProviderName, options.Relative);
        return CliCommandExecutionResult.Ok($"Pixel {point.Point.X},{point.Point.Y}: {data.Color}", data);
    }

    private static async Task<CliCommandExecutionResult> WaitColorAsync(IScreenPixelReader reader, ScreenCliOptions options, ScreenPixelColor expected, CancellationToken cancellationToken)
    {
        var point = new ScreenPoint(options.X, options.Y);
        var result = await reader.WaitForPixelAsync(point, expected, CreateOptions(options.TimeoutMs, cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess) return ToFailure("Failed while waiting for screen color.", result);
        var data = new ScreenWaitColorData(point.X, point.Y, expected.ToString(), result.Value.ToString(), reader.ProviderName, Matched: true, options.TimeoutMs);
        return CliCommandExecutionResult.Ok($"Pixel {point.X},{point.Y} matched {expected}.", data);
    }

    private static async Task<CliCommandExecutionResult> SearchColorAsync(IScreenPixelReader reader, ScreenCliOptions options, ScreenPixelColor expected, int x2, int y2, CancellationToken cancellationToken)
    {
        var left = Math.Min(options.X, x2);
        var top = Math.Min(options.Y, y2);
        var region = new ScreenRect(left, top, checked(Math.Max(options.X, x2) - left), checked(Math.Max(options.Y, y2) - top));
        var result = await reader.SearchPixelAsync(region, expected, options.Tolerance, CreateOptions(options.TimeoutMs, cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess) return ToFailure("Failed while searching for screen color.", result);
        var match = result.Value;
        var data = new ScreenSearchColorData(Found: true, match.Point.X, match.Point.Y, match.Color.ToString(), expected.ToString(), region.X, region.Y, region.Width, region.Height, options.Tolerance, reader.ProviderName);
        return CliCommandExecutionResult.Ok($"Color {expected} found at {match.Point.X},{match.Point.Y}.", data);
    }

    private async Task<CliCommandExecutionResult> SearchImageAsync(ScreenCliOptions options, CancellationToken cancellationToken)
    {
        var request = CreateImageRequest(options, out var invalid);
        if (invalid is not null) return invalid;
        if (!TryGetImageAutomation(out var automation, out var unsupported)) return unsupported;
        var result = await automation.SearchAsync(request!, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) return ImageFound("Image found", result, options, request!.Region, automation.ProviderName);
        if (result.ErrorKind is ScreenReadErrorKind.CaptureTimeout)
        {
            return CliCommandExecutionResult.Ok("Image was not found.", CreateSearchImageData(found: false, x: null, y: null, score: null, options, request!.Region, automation.ProviderName), [result.ErrorMessage ?? "No matching image was found."]);
        }
        if (result.ErrorKind is ScreenReadErrorKind.InvalidArguments)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, result.ErrorMessage ?? "Invalid image search arguments.", [result.ErrorMessage ?? "Invalid image search arguments."]);
        }
        return ToFailure("Failed while searching for screen image.", result);
    }

    private async Task<CliCommandExecutionResult> WaitImageAsync(ScreenCliOptions options, CancellationToken cancellationToken)
    {
        var request = CreateImageRequest(options, out var invalid);
        if (invalid is not null) return invalid;
        if (!TryGetImageAutomation(out var automation, out var unsupported)) return unsupported;
        var result = await automation.WaitAsync(request!, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) return ImageFound("Image appeared", result, options, request!.Region, automation.ProviderName);
        if (result.ErrorKind is ScreenReadErrorKind.CaptureTimeout)
        {
            return CliCommandExecutionResult.Ok("Image did not appear before timeout.", CreateSearchImageData(found: false, x: null, y: null, score: null, options, request!.Region, automation.ProviderName), [result.ErrorMessage ?? "No matching image was found."]);
        }
        if (result.ErrorKind is ScreenReadErrorKind.InvalidArguments)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, result.ErrorMessage ?? "Invalid image wait arguments.", [result.ErrorMessage ?? "Invalid image wait arguments."]);
        }
        return ToFailure("Failed while waiting for screen image.", result);
    }

    private async Task<CliCommandExecutionResult> ImageClickAsync(ScreenCliOptions options, CancellationToken cancellationToken)
    {
        var request = CreateImageRequest(options, out var invalid);
        if (invalid is not null) return invalid;
        if (!TryGetImageAutomation(out var automation, out var unsupported)) return unsupported;
        var result = await automation.ClickAsync(request!, ToMouseButtonCode(options.Button), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            if (result.ErrorKind is ScreenReadErrorKind.Unsupported)
            {
                return CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Screen image click requires absolute coordinate support.", [result.ErrorMessage ?? "Image click movement could not be resolved."]);
            }
            if (result.ErrorKind is ScreenReadErrorKind.InvalidArguments)
            {
                return CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, result.ErrorMessage ?? "Invalid image click arguments.", [result.ErrorMessage ?? "Invalid image click arguments."]);
            }
            return ToFailure("Failed while searching for screen image to click.", result);
        }
        var point = result.Point!.Value;
        var data = new ScreenImageClickData(point.X, point.Y, result.Score!.Value, Path.GetFullPath(options.ImagePath!), request!.Region?.X, request.Region?.Y, request.Region?.Width, request.Region?.Height, options.Similarity, options.Downsample, options.MatchMode is ScreenImageMatchMode.Best ? "best" : "first", options.ScaleAware, options.Button.ToString(), automation.ProviderName);
        return CliCommandExecutionResult.Ok($"Image clicked at {point.X},{point.Y} with score {result.Score:0.###}.", data);
    }

    private ScreenImageAutomationRequest? CreateImageRequest(ScreenCliOptions options, out CliCommandExecutionResult? error)
    {
        error = null;
        if (!TryCreateRegion(options, out var region, out error)) return null;
        if (!double.IsFinite(options.Similarity) || options.Similarity is < 0.0 or > 1.0 || options.Downsample < 1)
        {
            error = InvalidOptions(options.Action);
            return null;
        }
        return new ScreenImageAutomationRequest(options.ImagePath!, region, options.Similarity, options.Downsample, options.MatchMode, options.ScaleAware, options.TimeoutMs is { } timeout ? TimeSpan.FromMilliseconds(timeout) : null);
    }

    private bool TryGetImageAutomation([NotNullWhen(true)] out IScreenImageAutomation? automation, [NotNullWhen(false)] out CliCommandExecutionResult? error)
    {
        if (_imageAutomation is null || !_imageAutomation.IsSupported)
        {
            automation = null;
            error = CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Screen image matching is not supported in this runtime.", ["No supported IScreenImageAutomation is available for the current platform/session."]);
            return false;
        }
        automation = _imageAutomation;
        error = null;
        return true;
    }

    private bool TryGetScreenPixelReader([NotNullWhen(true)] out IScreenPixelReader? reader, [NotNullWhen(false)] out CliCommandExecutionResult? error)
    {
        if (_screenPixelReader is null || !_screenPixelReader.IsSupported)
        {
            reader = null;
            error = CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Screen pixel reading is not supported in this runtime.", ["No supported IScreenPixelReader is available for the current platform/session."]);
            return false;
        }
        reader = _screenPixelReader;
        error = null;
        return true;
    }

    private async Task<PointResolutionResult> ResolvePointAsync(ScreenCliOptions options, CancellationToken cancellationToken)
    {
        if (!options.Relative) return PointResolutionResult.Ok(new ScreenPoint(options.X, options.Y));
        if (_mousePositionProvider is null || !_mousePositionProvider.IsSupported)
        {
            return PointResolutionResult.Fail(CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Relative screen pixel reads are not supported in this runtime.", ["No supported IMousePositionProvider is available for the current platform/session."]));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
        return position is null
            ? PointResolutionResult.Fail(CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Relative screen pixel reads are not supported in this runtime.", ["The current mouse position is unavailable."]))
            : PointResolutionResult.Ok(new ScreenPoint(checked(position.Value.X + options.X), checked(position.Value.Y + options.Y)));
    }

    private static bool TryCreateRegion(ScreenCliOptions options, out ScreenRect? region, out CliCommandExecutionResult? error)
    {
        region = null;
        error = null;
        var any = options.RegionX.HasValue || options.RegionY.HasValue || options.RegionWidth.HasValue || options.RegionHeight.HasValue;
        if (!any) return true;
        if (options.RegionX is not int x || options.RegionY is not int y || options.RegionWidth is not int width || options.RegionHeight is not int height || width <= 0 || height <= 0)
        {
            error = CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "screen search-image --region requires <x> <y> <width> <height> with positive width and height.");
            return false;
        }
        try { region = new ScreenRect(x, y, width, height); return true; }
        catch (OverflowException) { error = CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "screen search-image --region endpoint exceeds the supported screen coordinate range."); return false; }
    }

    private static CliCommandExecutionResult ImageFound(string prefix, ScreenImageAutomationResult result, ScreenCliOptions options, ScreenRect? region, string providerName)
    {
        var point = result.Point!.Value;
        return CliCommandExecutionResult.Ok($"{prefix} at {point.X},{point.Y} with score {result.Score:0.###}.", CreateSearchImageData(found: true, point.X, point.Y, result.Score, options, region, providerName));
    }

    private static ScreenSearchImageData CreateSearchImageData(bool found, int? x, int? y, double? score, ScreenCliOptions options, ScreenRect? region, string providerName) =>
        new(found, x, y, score, Path.GetFullPath(options.ImagePath!), region?.X, region?.Y, region?.Width, region?.Height, options.Similarity, options.Downsample, options.MatchMode is ScreenImageMatchMode.Best ? "best" : "first", options.ScaleAware, providerName);

    private static int ToMouseButtonCode(MouseButton button) => button switch { MouseButton.Right => MouseButtonCode.Right, MouseButton.Middle => MouseButtonCode.Middle, _ => MouseButtonCode.Left };
    private static ScreenReadOptions CreateOptions(int? timeout, CancellationToken token) => new(timeout is { } value ? TimeSpan.FromMilliseconds(value) : null, ScreenReadOptions.Default.PollInterval, token);
    private static CliCommandExecutionResult InvalidOptions(ScreenCliAction action) => CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, $"Invalid options for screen action '{action}'.");

    private static CliCommandExecutionResult ToFailure<T>(string message, ScreenReadResult<T> result)
    {
        var code = result.ErrorKind switch { ScreenReadErrorKind.InvalidArguments => CliExitCode.InvalidArguments, ScreenReadErrorKind.Unsupported or ScreenReadErrorKind.PermissionDenied or ScreenReadErrorKind.BackendUnavailable => CliExitCode.EnvironmentError, ScreenReadErrorKind.Canceled => CliExitCode.Cancelled, _ => CliExitCode.RuntimeError };
        return CliCommandExecutionResult.Fail(code, message, [result.ErrorMessage ?? result.ErrorKind?.ToString() ?? "Unknown screen read error."]);
    }

    private static CliCommandExecutionResult ToFailure(string message, ScreenImageAutomationResult result)
    {
        var code = result.ErrorKind switch { ScreenReadErrorKind.InvalidArguments => CliExitCode.InvalidArguments, ScreenReadErrorKind.Unsupported or ScreenReadErrorKind.PermissionDenied or ScreenReadErrorKind.BackendUnavailable => CliExitCode.EnvironmentError, ScreenReadErrorKind.Canceled => CliExitCode.Cancelled, _ => CliExitCode.RuntimeError };
        return CliCommandExecutionResult.Fail(code, message, [result.ErrorMessage ?? result.ErrorKind?.ToString() ?? "Unknown screen read error."]);
    }

    private readonly record struct PointResolutionResult(ScreenPoint Point, CliCommandExecutionResult? Error)
    {
        public static PointResolutionResult Ok(ScreenPoint point) => new(point, Error: null);
        public static PointResolutionResult Fail(CliCommandExecutionResult error) => new(default, error);
    }
}
