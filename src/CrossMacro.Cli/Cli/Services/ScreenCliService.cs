using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Infrastructure.Services.Playback;
using CrossMacro.Infrastructure.Services.ScreenReading;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Cli.Services;

public sealed class ScreenCliService : IScreenCliService
{
    private readonly IScreenPixelReader? _screenPixelReader;
    private readonly IMousePositionProvider? _mousePositionProvider;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;
    private readonly InputSimulatorPool? _simulatorPool;
    private readonly IImageClickMovementResolver _imageClickMovementResolver;
    private readonly IImageAssetCodec _imageAssetCodec;

    public ScreenCliService(
        IScreenPixelReader? screenPixelReader,
        IMousePositionProvider? mousePositionProvider,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        InputSimulatorPool? simulatorPool = null,
        IImageClickMovementResolver? imageClickMovementResolver = null,
        IImageAssetCodec? imageAssetCodec = null)
    {
        _screenPixelReader = screenPixelReader;
        _mousePositionProvider = mousePositionProvider;
        _inputSimulatorFactory = inputSimulatorFactory;
        _simulatorPool = simulatorPool;
        _imageClickMovementResolver = imageClickMovementResolver ?? new ImageClickMovementResolver(mousePositionProvider);
        _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();
    }

    public ScreenCliService(
        IScreenPixelReader? screenPixelReader,
        IMousePositionProvider? mousePositionProvider,
        IInputSimulator? inputSimulator,
        IImageClickMovementResolver? imageClickMovementResolver = null)
        : this(
            screenPixelReader,
            mousePositionProvider,
            inputSimulator is null ? null : () => inputSimulator,
            null,
            imageClickMovementResolver,
            null)
    {
    }

    public async Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken)
    {
        if (!TryGetScreenPixelReader(out var screenPixelReader, out var unsupported))
        {
            return unsupported;
        }

        return options.Action switch
        {
            ScreenCliAction.Pixel => await PixelAsync(screenPixelReader, options, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.WaitColor when options.ExpectedColor is ScreenPixelColor expected => await WaitColorAsync(screenPixelReader, options, expected, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.SearchColor when options.ExpectedColor is ScreenPixelColor expected && options.X2 is int x2 && options.Y2 is int y2 && options.X != x2 && options.Y != y2 => await SearchColorAsync(screenPixelReader, options, expected, x2, y2, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.SearchImage when options.ImagePath is { Length: > 0 } => await SearchImageAsync(screenPixelReader, options, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.WaitImage when options.ImagePath is { Length: > 0 } => await WaitImageAsync(screenPixelReader, options, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.ImageClick when options.ImagePath is { Length: > 0 } => await ImageClickAsync(screenPixelReader, options, cancellationToken).ConfigureAwait(false),
            ScreenCliAction.WaitColor or ScreenCliAction.SearchColor => InvalidOptions(options.Action),
            ScreenCliAction.SearchImage or ScreenCliAction.WaitImage or ScreenCliAction.ImageClick => InvalidOptions(options.Action),
            _ => CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown screen action.")
        };
    }

    private async Task<CliCommandExecutionResult> PixelAsync(IScreenPixelReader screenPixelReader, ScreenCliOptions options, CancellationToken cancellationToken)
    {
        var pointResult = await ResolvePointAsync(options, cancellationToken).ConfigureAwait(false);
        if (pointResult.Error is { } error)
        {
            return error;
        }

        var point = pointResult.Point;
        var result = await screenPixelReader.GetPixelAsync(point, CreateOptions(options.TimeoutMs is null ? null : TimeSpan.FromMilliseconds(options.TimeoutMs.Value), cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ToFailure("Failed to read screen pixel.", result);
        }

        var data = new ScreenPixelData(point.X, point.Y, result.Value.ToString(), screenPixelReader.ProviderName, options.Relative);
        return CliCommandExecutionResult.Ok($"Pixel {point.X},{point.Y}: {data.Color}", data);
    }

    private static async Task<CliCommandExecutionResult> WaitColorAsync(IScreenPixelReader screenPixelReader, ScreenCliOptions options, ScreenPixelColor expected, CancellationToken cancellationToken)
    {
        var point = new ScreenPoint(options.X, options.Y);
        var result = await screenPixelReader.WaitForPixelAsync(
            point,
            expected,
            CreateOptions(options.TimeoutMs is null ? null : TimeSpan.FromMilliseconds(options.TimeoutMs.Value), cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ToFailure("Failed while waiting for screen color.", result);
        }

        var data = new ScreenWaitColorData(point.X, point.Y, expected.ToString(), result.Value.ToString(), screenPixelReader.ProviderName, true, options.TimeoutMs);
        return CliCommandExecutionResult.Ok($"Pixel {point.X},{point.Y} matched {expected}.", data);
    }

    private static async Task<CliCommandExecutionResult> SearchColorAsync(IScreenPixelReader screenPixelReader, ScreenCliOptions options, ScreenPixelColor expected, int x2, int y2, CancellationToken cancellationToken)
    {
        var left = Math.Min(options.X, x2);
        var top = Math.Min(options.Y, y2);
        var right = Math.Max(options.X, x2);
        var bottom = Math.Max(options.Y, y2);
        var region = new ScreenRect(left, top, checked(right - left), checked(bottom - top));
        var result = await screenPixelReader.SearchPixelAsync(region, expected, options.Tolerance, CreateOptions(options.TimeoutMs is null ? null : TimeSpan.FromMilliseconds(options.TimeoutMs.Value), cancellationToken)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ToFailure("Failed while searching for screen color.", result);
        }

        var match = result.Value;
        var data = new ScreenSearchColorData(
            true,
            match.Point.X,
            match.Point.Y,
            match.Color.ToString(),
            expected.ToString(),
            region.X,
            region.Y,
            region.Width,
            region.Height,
            options.Tolerance,
            screenPixelReader.ProviderName);
        return CliCommandExecutionResult.Ok($"Color {expected} found at {match.Point.X},{match.Point.Y}.", data);
    }

    private async Task<CliCommandExecutionResult> SearchImageAsync(IScreenPixelReader screenPixelReader, ScreenCliOptions options, CancellationToken cancellationToken)
    {
        var setup = await PrepareImageSearchAsync(screenPixelReader, options, cancellationToken).ConfigureAwait(false);
        if (setup.Error is { } error)
        {
            return error;
        }

        using (setup.Template)
        {
            var result = await setup.ImageSearchReader!.SearchImageAsync(setup.Region, setup.Template!, setup.MatchOptions!, CreateOptions(options.TimeoutMs is null ? null : TimeSpan.FromMilliseconds(options.TimeoutMs.Value), cancellationToken)).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                var match = result.Value;
                var foundData = CreateSearchImageData(true, match.Point.X, match.Point.Y, match.Score, options, setup.Region, screenPixelReader.ProviderName);
                return CliCommandExecutionResult.Ok($"Image found at {match.Point.X},{match.Point.Y} with score {match.Score:0.###}.", foundData);
            }

            if (result.ErrorKind == ScreenReadErrorKind.CaptureTimeout)
            {
                var notFoundData = CreateSearchImageData(false, null, null, null, options, setup.Region, screenPixelReader.ProviderName);
                return CliCommandExecutionResult.Ok("Image was not found.", notFoundData, [result.ErrorMessage ?? "No matching image was found."]);
            }

            return ToFailure("Failed while searching for screen image.", result);
        }
    }

    private async Task<CliCommandExecutionResult> WaitImageAsync(IScreenPixelReader screenPixelReader, ScreenCliOptions options, CancellationToken cancellationToken)
    {
        var setup = await PrepareImageSearchAsync(screenPixelReader, options, cancellationToken).ConfigureAwait(false);
        if (setup.Error is { } error)
        {
            return error;
        }

        using (setup.Template)
        {
            var timeout = TimeSpan.FromMilliseconds(options.TimeoutMs ?? 5000);
            var pollInterval = ScreenReadOptions.Default.PollInterval ?? TimeSpan.FromMilliseconds(50);
            var deadline = DateTimeOffset.UtcNow + timeout;
            while (true)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

                var result = await setup.ImageSearchReader!.SearchImageAsync(setup.Region, setup.Template!, setup.MatchOptions!, CreateOptions(remaining, cancellationToken)).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    var match = result.Value;
                    var data = CreateSearchImageData(true, match.Point.X, match.Point.Y, match.Score, options, setup.Region, screenPixelReader.ProviderName);
                    return CliCommandExecutionResult.Ok($"Image appeared at {match.Point.X},{match.Point.Y} with score {match.Score:0.###}.", data);
                }

                if (result.ErrorKind != ScreenReadErrorKind.CaptureTimeout)
                {
                    return ToFailure("Failed while waiting for screen image.", result);
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    var data = CreateSearchImageData(false, null, null, null, options, setup.Region, screenPixelReader.ProviderName);
                    return CliCommandExecutionResult.Ok("Image did not appear before timeout.", data, [result.ErrorMessage ?? "No matching image was found."]);
                }

                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<CliCommandExecutionResult> ImageClickAsync(IScreenPixelReader screenPixelReader, ScreenCliOptions options, CancellationToken cancellationToken)
    {
        if (_inputSimulatorFactory is null)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Screen image click is not supported in this runtime.", ["No supported IInputSimulator is available for the current platform/session."]);
        }

        var resolution = _mousePositionProvider is null ? null : await _mousePositionProvider.GetScreenResolutionAsync().ConfigureAwait(false);
        var simulatorWidth = resolution?.Width ?? 0;
        var simulatorHeight = resolution?.Height ?? 0;
        var inputSimulator = _simulatorPool?.Acquire(simulatorWidth, simulatorHeight) ?? _inputSimulatorFactory();
        var releaseWithPool = _simulatorPool is not null;

        try
        {
            if (!inputSimulator.IsSupported)
            {
                return CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Screen image click is not supported in this runtime.", ["No supported IInputSimulator is available for the current platform/session."]);
            }

            var setup = await PrepareImageSearchAsync(screenPixelReader, options, cancellationToken).ConfigureAwait(false);
            if (setup.Error is { } error)
            {
                return error;
            }

            using (setup.Template)
            {
                var result = await setup.ImageSearchReader!.SearchImageAsync(setup.Region, setup.Template!, setup.MatchOptions!, CreateOptions(options.TimeoutMs is null ? null : TimeSpan.FromMilliseconds(options.TimeoutMs.Value), cancellationToken)).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return ToFailure("Failed while searching for screen image to click.", result);
                }

                var point = new ScreenPoint(
                    checked(result.Value.Point.X + setup.Template!.LogicalBounds.Width / 2),
                    checked(result.Value.Point.Y + setup.Template.LogicalBounds.Height / 2));
                inputSimulator.Initialize(simulatorWidth, simulatorHeight);
                var movement = await _imageClickMovementResolver.ResolveAsync(inputSimulator, point, cancellationToken).ConfigureAwait(false);
                if (!movement.IsSuccess)
                {
                    return CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Screen image click requires absolute coordinate support.", [movement.ErrorMessage ?? "Image click movement could not be resolved."]);
                }

                if (movement.CoordinateMode == MouseCoordinateMode.Absolute) inputSimulator.MoveAbsolute(movement.X, movement.Y);
                else inputSimulator.MoveRelative(movement.X, movement.Y);
                inputSimulator.MouseButton(ToMouseButtonCode(options.Button), true);
                inputSimulator.MouseButton(ToMouseButtonCode(options.Button), false);
                inputSimulator.Sync();
            var data = new ScreenImageClickData(point.X, point.Y, result.Value.Score, Path.GetFullPath(options.ImagePath ?? string.Empty), setup.Region?.X, setup.Region?.Y, setup.Region?.Width, setup.Region?.Height, options.Similarity, options.Downsample, options.MatchMode == ScreenImageMatchSelectionMode.BestMatch ? "best" : "first", options.ScaleAware, options.Button.ToString(), screenPixelReader.ProviderName);
                return CliCommandExecutionResult.Ok($"Image clicked at {point.X},{point.Y} with score {result.Value.Score:0.###}.", data);
            }
        }
        finally
        {
            if (releaseWithPool) _simulatorPool!.Release(inputSimulator, simulatorWidth, simulatorHeight);
            else inputSimulator.Dispose();
        }
    }

    private async Task<PointResolutionResult> ResolvePointAsync(ScreenCliOptions options, CancellationToken cancellationToken)
    {
        if (!options.Relative)
        {
            return PointResolutionResult.Ok(new ScreenPoint(options.X, options.Y));
        }

        if (_mousePositionProvider is null || !_mousePositionProvider.IsSupported)
        {
            return PointResolutionResult.Fail(CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Relative screen pixel reads are not supported in this runtime.",
                ["No supported IMousePositionProvider is available for the current platform/session."]));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
        if (position is null)
        {
            return PointResolutionResult.Fail(CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Relative screen pixel reads are not supported in this runtime.",
                ["The current mouse position is unavailable."]));
        }

        return PointResolutionResult.Ok(new ScreenPoint(checked(position.Value.X + options.X), checked(position.Value.Y + options.Y)));
    }

    private bool TryGetScreenPixelReader(
        [NotNullWhen(true)] out IScreenPixelReader? screenPixelReader,
        [NotNullWhen(false)] out CliCommandExecutionResult? result)
    {
        if (_screenPixelReader is null || !_screenPixelReader.IsSupported)
        {
            screenPixelReader = null;
            result = CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Screen pixel reading is not supported in this runtime.",
                ["No supported IScreenPixelReader is available for the current platform/session."]);
            return false;
        }

        screenPixelReader = _screenPixelReader;
        result = null;
        return true;
    }

    private async Task<ImageSearchSetup> PrepareImageSearchAsync(IScreenPixelReader screenPixelReader, ScreenCliOptions options, CancellationToken cancellationToken)
    {
        if (screenPixelReader is not IScreenImageSearchReader imageSearchReader)
        {
            return ImageSearchSetup.Fail(CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Screen image matching is not supported in this runtime.",
                [$"Provider '{screenPixelReader.ProviderName}' does not expose image search."]));
        }

        if (!TryCreateSearchImageRegion(options, out var region, out var invalidRegion))
        {
            return ImageSearchSetup.Fail(invalidRegion);
        }

        if (!double.IsFinite(options.Similarity) || options.Similarity is < 0.0 or > 1.0 || options.Downsample < 1)
        {
            return ImageSearchSetup.Fail(InvalidOptions(options.Action));
        }

        var imagePath = options.ImagePath ?? string.Empty;
        ScreenFrame template;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            template = await _imageAssetCodec.DecodeFileAsync(imagePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return ImageSearchSetup.Fail(CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Image file was not found.", [ex.Message]));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return ImageSearchSetup.Fail(CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Failed to read image file.", [ex.Message]));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            return ImageSearchSetup.Fail(CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Image file is not a supported PNG.", [ex.Message]));
        }

        var matchOptions = ScreenImageMatchOptions.Create(region, options.Similarity, options.Downsample, options.MatchMode, options.ScaleAware);
        return ImageSearchSetup.Ok(imageSearchReader, template, region, matchOptions);
    }

    private static CliCommandExecutionResult InvalidOptions(ScreenCliAction action) =>
        CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, $"Invalid options for screen action '{action}'.");

    private static bool TryCreateSearchImageRegion(ScreenCliOptions options, out ScreenRect? region, out CliCommandExecutionResult error)
    {
        region = null;
        error = null!;
        var hasAnyRegionValue = options.RegionX.HasValue || options.RegionY.HasValue || options.RegionWidth.HasValue || options.RegionHeight.HasValue;
        if (!hasAnyRegionValue)
        {
            return true;
        }

        if (options.RegionX is not int x || options.RegionY is not int y || options.RegionWidth is not int width || options.RegionHeight is not int height || width <= 0 || height <= 0)
        {
            error = CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "screen search-image --region requires <x> <y> <width> <height> with positive width and height.");
            return false;
        }

        try
        {
            region = new ScreenRect(x, y, width, height);
        }
        catch (OverflowException)
        {
            error = CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "screen search-image --region endpoint exceeds the supported screen coordinate range.");
            return false;
        }

        return true;
    }

    private static ScreenSearchImageData CreateSearchImageData(
        bool found,
        int? x,
        int? y,
        double? score,
        ScreenCliOptions options,
        ScreenRect? region,
        string providerName) =>
        new(
            found,
            x,
            y,
            score,
            Path.GetFullPath(options.ImagePath ?? string.Empty),
            region?.X,
            region?.Y,
            region?.Width,
            region?.Height,
            options.Similarity,
            options.Downsample,
            options.MatchMode == ScreenImageMatchSelectionMode.BestMatch ? "best" : "first",
            options.ScaleAware,
            providerName);

    private static int ToMouseButtonCode(MouseButton button)
    {
        return button switch
        {
            MouseButton.Right => MouseButtonCode.Right,
            MouseButton.Middle => MouseButtonCode.Middle,
            _ => MouseButtonCode.Left
        };
    }

    private static ScreenReadOptions CreateOptions(TimeSpan? timeout, CancellationToken cancellationToken) =>
        new(timeout, ScreenReadOptions.Default.PollInterval, cancellationToken);

    private static CliCommandExecutionResult ToFailure<T>(string message, ScreenReadResult<T> result)
    {
        var code = result.ErrorKind switch
        {
            ScreenReadErrorKind.Unsupported or ScreenReadErrorKind.PermissionDenied or ScreenReadErrorKind.BackendUnavailable => CliExitCode.EnvironmentError,
            ScreenReadErrorKind.Canceled => CliExitCode.Cancelled,
            ScreenReadErrorKind.ResourceLimitExceeded => CliExitCode.RuntimeError,
            _ => CliExitCode.RuntimeError
        };
        return CliCommandExecutionResult.Fail(code, message, [result.ErrorMessage ?? result.ErrorKind?.ToString() ?? "Unknown screen read error."]);
    }

    private readonly record struct PointResolutionResult(ScreenPoint Point, CliCommandExecutionResult? Error)
    {
        public static PointResolutionResult Ok(ScreenPoint point) => new(point, null);

        public static PointResolutionResult Fail(CliCommandExecutionResult error) => new(default, error);
    }

    private readonly record struct ImageSearchSetup(
        IScreenImageSearchReader? ImageSearchReader,
        ScreenFrame? Template,
        ScreenRect? Region,
        ScreenImageMatchOptions? MatchOptions,
        CliCommandExecutionResult? Error)
    {
        public static ImageSearchSetup Ok(IScreenImageSearchReader reader, ScreenFrame template, ScreenRect? region, ScreenImageMatchOptions matchOptions) =>
            new(reader, template, region, matchOptions, null);

        public static ImageSearchSetup Fail(CliCommandExecutionResult error) =>
            new(null, null, null, null, error);
    }
}
