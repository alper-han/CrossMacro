
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenPixelReader(IScreenFrameProvider frameProvider) : IScreenPixelReader, IScreenImageSearchReader
{
    private readonly IScreenFrameProvider _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
    private readonly ScreenImageMatcher _imageMatcher = new();
    private bool _disposed;

    public string ProviderName => _frameProvider.ProviderName;

    public bool IsSupported => _frameProvider.IsSupported;

    internal int TemplateNormalizationCount => _imageMatcher.TemplateNormalizationCount;

    public async Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var region = new ScreenRect(point.X, point.Y, 1, 1);
        var capture = await CaptureFrameAsync(region, options).ConfigureAwait(false);
        if (!capture.IsSuccess)
        {
            return ScreenReadResultFactory.Failure<ScreenPixelColor>(
                capture.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                capture.ErrorMessage ?? "Screen frame capture failed.");
        }

        using var frame = capture.Value ?? throw new InvalidOperationException("Successful screen frame capture did not include a frame.");
        return frame.TryGetPixel(point, out var color)
            ? ScreenReadResultFactory.Success<ScreenPixelColor>(color)
            : ScreenReadResultFactory.Failure<ScreenPixelColor>(
                ScreenReadErrorKind.OutOfBounds,
                $"Point {point} is outside captured frame bounds {frame.LogicalBounds}.");
    }

    public async Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
        ScreenPoint point,
        ScreenPixelColor expected,
        ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var timeout = options.Timeout ?? ScreenReadOptions.DefaultTimeout;
        var pollInterval = options.PollInterval ?? ScreenReadOptions.DefaultPollInterval;
        return await ScreenReadPolling.PollUntilMatchAsync(
            (remaining, token) => WaitForPixelOnceAsync(
                point,
                expected,
                new ScreenReadOptions(remaining, pollInterval, pollUntilMatch: false, token)),
            timeout,
            pollInterval,
            "Screen pixel wait was canceled.",
            () => ScreenReadResultFactory.Failure<ScreenPixelColor>(
                ScreenReadErrorKind.CaptureTimeout,
                $"Timed out waiting for pixel {point} to become {expected}."),
            options.CancellationToken).ConfigureAwait(false);
    }

    private async Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelOnceAsync(
        ScreenPoint point,
        ScreenPixelColor expected,
        ScreenReadOptions options)
    {
        var result = await GetPixelAsync(point, options).ConfigureAwait(false);
        return !result.IsSuccess || result.Value == expected
            ? result
            : ScreenReadResultFactory.Failure<ScreenPixelColor>(
                ScreenReadErrorKind.CaptureTimeout,
                $"Pixel {point} does not match {expected}.");
    }

    public async Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
        ScreenRect region,
        ScreenPixelColor expected,
        int tolerance,
        ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var timeout = options.Timeout ?? ScreenReadOptions.DefaultTimeout;
        var pollInterval = options.PollInterval ?? ScreenReadOptions.DefaultPollInterval;
        return await ScreenReadPolling.PollUntilMatchAsync(
            (remaining, token) => SearchPixelOnceAsync(
                region,
                expected,
                tolerance,
                new ScreenReadOptions(remaining, pollInterval, pollUntilMatch: false, token)),
            timeout,
            pollInterval,
            "Screen pixel search was canceled.",
            timeoutFailure: null,
            cancellationToken: options.CancellationToken).ConfigureAwait(false);
    }

    private async Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelOnceAsync(
        ScreenRect region,
        ScreenPixelColor expected,
        int tolerance,
        ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (tolerance is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Screen pixel tolerance must be between 0 and 255.");
        }

        var capture = await CaptureFrameAsync(region, options).ConfigureAwait(false);
        if (!capture.IsSuccess)
        {
            return ScreenReadResultFactory.Failure<ScreenPixelSearchMatch>(
                capture.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                capture.ErrorMessage ?? "Screen frame capture failed.");
        }

        using var frame = capture.Value ?? throw new InvalidOperationException("Successful screen frame capture did not include a frame.");
        if (!frame.ContainsAnyValidPixel(region))
        {
            return ScreenReadResultFactory.Failure<ScreenPixelSearchMatch>(
                ScreenReadErrorKind.OutOfBounds,
                $"Search region {region} does not contain any valid captured screen pixels.");
        }

        var match = frame.SearchPixel(region, expected, tolerance);
        return match is { } found
            ? ScreenReadResultFactory.Success<ScreenPixelSearchMatch>(found)
            : ScreenReadResultFactory.Failure<ScreenPixelSearchMatch>(
                ScreenReadErrorKind.CaptureTimeout,
                $"No pixel matching {expected} was found in region {region}.");
    }

    public async Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
        ScreenRect? region,
        ScreenFrame imageTemplate,
        ScreenImageMatchOptions options,
        ScreenReadOptions readOptions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(imageTemplate);
        ArgumentNullException.ThrowIfNull(options);

        if (!readOptions.PollUntilMatch)
        {
            return await SearchImageOnceAsync(region, imageTemplate, options, readOptions).ConfigureAwait(false);
        }

        var timeout = readOptions.Timeout ?? ScreenReadOptions.DefaultTimeout;
        var pollInterval = readOptions.PollInterval ?? ScreenReadOptions.DefaultPollInterval;
        return await ScreenReadPolling.PollImageUntilConsistentAsync(
            (remaining, token) => SearchImageOnceAsync(
                region,
                imageTemplate,
                options,
                new ScreenReadOptions(remaining, pollInterval, pollUntilMatch: false, token)),
            timeout,
            pollInterval,
            readOptions.CancellationToken).ConfigureAwait(false);
    }

    private async Task<ScreenReadResult<ScreenImageMatch>> SearchImageOnceAsync(
        ScreenRect? region,
        ScreenFrame imageTemplate,
        ScreenImageMatchOptions options,
        ScreenReadOptions readOptions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(imageTemplate);
        ArgumentNullException.ThrowIfNull(options);

        using var timeoutCancellation = readOptions.Timeout is { } timeout && timeout > TimeSpan.Zero
            ? new CancellationTokenSource(timeout)
            : null;
        using var linkedCancellation = timeoutCancellation is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(readOptions.CancellationToken, timeoutCancellation.Token);
        ScreenReadOptions effectiveOptions;
        if (linkedCancellation is not null)
        {
            effectiveOptions = new ScreenReadOptions(readOptions.Timeout, readOptions.PollInterval, linkedCancellation.Token);
        }
        else
        {
            effectiveOptions = readOptions;
        }

        var capture = await CaptureFrameAsync(region, effectiveOptions).ConfigureAwait(false);
        if (!capture.IsSuccess)
        {
            if (capture.ErrorKind is ScreenReadErrorKind.Canceled && IsImageSearchTimeout(timeoutCancellation, readOptions.CancellationToken))
            {
                return ScreenReadResultFactory.Failure<ScreenImageMatch>(
                    ScreenReadErrorKind.CaptureTimeout,
                    "Timed out while capturing screen frame for image search.");
            }

            return ScreenReadResultFactory.Failure<ScreenImageMatch>(
                capture.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                capture.ErrorMessage ?? "Screen frame capture failed.");
        }

        using var frame = capture.Value ?? throw new InvalidOperationException("Successful screen frame capture did not include a frame.");
        var effectiveMatchOptions = options with { SearchRegion = region ?? options.SearchRegion };
        var effectiveRegion = effectiveMatchOptions.SearchRegion ?? frame.LogicalBounds;
        if (!frame.ContainsAnyValidPixel(effectiveRegion))
        {
            return ScreenReadResultFactory.Failure<ScreenImageMatch>(
                ScreenReadErrorKind.OutOfBounds,
                $"Image search region {effectiveRegion} does not contain any valid captured screen pixels.");
        }

        try
        {
            var matcherCancellationToken = effectiveOptions.CancellationToken;
            matcherCancellationToken.ThrowIfCancellationRequested();
            var match = _imageMatcher.FindMatch(frame, imageTemplate, effectiveMatchOptions, matcherCancellationToken);
            return match is { } found
                ? ScreenReadResultFactory.Success<ScreenImageMatch>(found)
                : ScreenReadResultFactory.Failure<ScreenImageMatch>(
                    ScreenReadErrorKind.CaptureTimeout,
                    $"No image matching the template was found in region {effectiveMatchOptions.SearchRegion ?? frame.LogicalBounds}.");
        }
        catch (ScreenImageMatcherResourceLimitException ex)
        {
            return ScreenReadResultFactory.Failure<ScreenImageMatch>(
                ScreenReadErrorKind.ResourceLimitExceeded,
                ex.Message);
        }
        catch (OperationCanceledException)
        {
            return IsImageSearchTimeout(timeoutCancellation, readOptions.CancellationToken)
                ? ScreenReadResultFactory.Failure<ScreenImageMatch>(
                    ScreenReadErrorKind.CaptureTimeout,
                    $"Timed out while searching for screen image in region {effectiveMatchOptions.SearchRegion ?? frame.LogicalBounds}.")
                : ScreenReadResultFactory.Failure<ScreenImageMatch>(
                    ScreenReadErrorKind.Canceled,
                    "Screen image search was canceled.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _imageMatcher.Dispose();
        _frameProvider.Dispose();
    }

    private async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        try
        {
            var effectiveOptions = options.Timeout == TimeSpan.Zero
                ? new ScreenReadOptions(
                    ScreenReadOptions.DefaultTimeout,
                    options.PollInterval,
                    options.PollUntilMatch,
                    options.CancellationToken)
                : options;
            effectiveOptions.CancellationToken.ThrowIfCancellationRequested();
            return await _frameProvider.CaptureFrameAsync(region, effectiveOptions).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.Canceled,
                "Screen frame capture was canceled.");
        }
    }

    private static bool IsImageSearchTimeout(CancellationTokenSource? timeoutCancellation, CancellationToken callerToken)
    {
        return timeoutCancellation is { IsCancellationRequested: true } && !callerToken.IsCancellationRequested;
    }
}
