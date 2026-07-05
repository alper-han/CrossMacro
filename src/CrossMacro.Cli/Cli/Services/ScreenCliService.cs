using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Cli.Services;

public sealed class ScreenCliService : IScreenCliService
{
    private readonly IScreenPixelReader? _screenPixelReader;
    private readonly IMousePositionProvider? _mousePositionProvider;

    public ScreenCliService(IScreenPixelReader? screenPixelReader, IMousePositionProvider? mousePositionProvider)
    {
        _screenPixelReader = screenPixelReader;
        _mousePositionProvider = mousePositionProvider;
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
            ScreenCliAction.WaitColor or ScreenCliAction.SearchColor => InvalidOptions(options.Action),
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
        var result = await screenPixelReader.GetPixelAsync(point, CreateOptions(null, cancellationToken)).ConfigureAwait(false);
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
        var result = await screenPixelReader.SearchPixelAsync(region, expected, options.Tolerance, CreateOptions(null, cancellationToken)).ConfigureAwait(false);
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

    private static CliCommandExecutionResult InvalidOptions(ScreenCliAction action) =>
        CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, $"Invalid options for screen action '{action}'.");

    private static ScreenReadOptions CreateOptions(TimeSpan? timeout, CancellationToken cancellationToken) =>
        new(timeout, ScreenReadOptions.Default.PollInterval, cancellationToken);

    private static CliCommandExecutionResult ToFailure<T>(string message, ScreenReadResult<T> result)
    {
        var code = result.ErrorKind is ScreenReadErrorKind.Unsupported or ScreenReadErrorKind.PermissionDenied or ScreenReadErrorKind.BackendUnavailable
            ? CliExitCode.EnvironmentError
            : CliExitCode.RuntimeError;
        return CliCommandExecutionResult.Fail(code, message, [result.ErrorMessage ?? result.ErrorKind?.ToString() ?? "Unknown screen read error."]);
    }

    private readonly record struct PointResolutionResult(ScreenPoint Point, CliCommandExecutionResult? Error)
    {
        public static PointResolutionResult Ok(ScreenPoint point) => new(point, null);

        public static PointResolutionResult Fail(CliCommandExecutionResult error) => new(default, error);
    }
}
