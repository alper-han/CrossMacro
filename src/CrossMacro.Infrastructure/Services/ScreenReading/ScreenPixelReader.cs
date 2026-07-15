
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenPixelReader : IScreenPixelReader, IScreenImageSearchReader
{
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(50);

    private readonly IScreenFrameProvider _frameProvider;
    private readonly ScreenImageMatcher _imageMatcher = new();
    private bool _disposed;

    public ScreenPixelReader(IScreenFrameProvider frameProvider)
    {
        _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
    }

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
            return ScreenReadResult<ScreenPixelColor>.Failure(
                capture.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                capture.ErrorMessage ?? "Screen frame capture failed.");
        }

        using var frame = capture.Value ?? throw new InvalidOperationException("Successful screen frame capture did not include a frame.");
        return frame.TryGetPixel(point, out var color)
            ? ScreenReadResult<ScreenPixelColor>.Success(color)
            : ScreenReadResult<ScreenPixelColor>.Failure(
                ScreenReadErrorKind.OutOfBounds,
                $"Point {point} is outside captured frame bounds {frame.LogicalBounds}.");
    }

    public async Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
        ScreenPoint point,
        ScreenPixelColor expected,
        ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var timeout = options.Timeout ?? DefaultWaitTimeout;
        var pollInterval = options.PollInterval ?? DefaultPollInterval;
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            var result = await GetPixelAsync(point, options).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return result;
            }

            if (result.Value == expected)
            {
                return result;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return ScreenReadResult<ScreenPixelColor>.Failure(
                    ScreenReadErrorKind.CaptureTimeout,
                    $"Timed out waiting for pixel {point} to become {expected}.");
            }

            try
            {
                await Task.Delay(pollInterval, options.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ScreenReadResult<ScreenPixelColor>.Failure(
                    ScreenReadErrorKind.Canceled,
                    "Screen pixel wait was canceled.");
            }
        }
    }

    public async Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
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
            return ScreenReadResult<ScreenPixelSearchMatch>.Failure(
                capture.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                capture.ErrorMessage ?? "Screen frame capture failed.");
        }

        using var frame = capture.Value ?? throw new InvalidOperationException("Successful screen frame capture did not include a frame.");
        if (!frame.ContainsAnyValidPixel(region))
        {
            return ScreenReadResult<ScreenPixelSearchMatch>.Failure(
                ScreenReadErrorKind.OutOfBounds,
                $"Search region {region} does not contain any valid captured screen pixels.");
        }

        var match = frame.SearchPixel(region, expected, tolerance);
        return match is { } found
            ? ScreenReadResult<ScreenPixelSearchMatch>.Success(found)
            : ScreenReadResult<ScreenPixelSearchMatch>.Failure(
                ScreenReadErrorKind.CaptureTimeout,
                $"No pixel matching {expected} was found in region {region}.");
    }

    public async Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
        ScreenRect? region,
        ScreenFrame template,
        ScreenImageMatchOptions matchOptions,
        ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(matchOptions);

        using var timeoutCancellation = options.Timeout is { } timeout
            ? new CancellationTokenSource(timeout)
            : null;
        using var linkedCancellation = timeoutCancellation is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken, timeoutCancellation.Token);
        var effectiveOptions = linkedCancellation is null
            ? options
            : new ScreenReadOptions(options.Timeout, options.PollInterval, linkedCancellation.Token);

        var capture = await CaptureFrameAsync(region, effectiveOptions).ConfigureAwait(false);
        if (!capture.IsSuccess)
        {
            if (capture.ErrorKind is ScreenReadErrorKind.Canceled && IsImageSearchTimeout(options.CancellationToken, timeoutCancellation))
            {
                return ScreenReadResult<ScreenImageMatch>.Failure(
                    ScreenReadErrorKind.CaptureTimeout,
                    "Timed out while capturing screen frame for image search.");
            }

            return ScreenReadResult<ScreenImageMatch>.Failure(
                capture.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                capture.ErrorMessage ?? "Screen frame capture failed.");
        }

        using var frame = capture.Value ?? throw new InvalidOperationException("Successful screen frame capture did not include a frame.");
        var effectiveMatchOptions = matchOptions with { SearchRegion = region ?? matchOptions.SearchRegion };
        var effectiveRegion = effectiveMatchOptions.SearchRegion ?? frame.LogicalBounds;
        if (!frame.ContainsAnyValidPixel(effectiveRegion))
        {
            return ScreenReadResult<ScreenImageMatch>.Failure(
                ScreenReadErrorKind.OutOfBounds,
                $"Image search region {effectiveRegion} does not contain any valid captured screen pixels.");
        }

        try
        {
            var matcherCancellationToken = effectiveMatchOptions.SelectionMode is ScreenImageMatchSelectionMode.FirstThresholdMatch
                ? options.CancellationToken
                : effectiveOptions.CancellationToken;
            matcherCancellationToken.ThrowIfCancellationRequested();
            var match = _imageMatcher.FindMatch(frame, template, effectiveMatchOptions, matcherCancellationToken);
            return match is { } found
                ? ScreenReadResult<ScreenImageMatch>.Success(found)
                : ScreenReadResult<ScreenImageMatch>.Failure(
                    ScreenReadErrorKind.CaptureTimeout,
                    $"No image matching the template was found in region {effectiveMatchOptions.SearchRegion ?? frame.LogicalBounds}.");
        }
        catch (ScreenImageMatcherResourceLimitException ex)
        {
            return ScreenReadResult<ScreenImageMatch>.Failure(
                ScreenReadErrorKind.ResourceLimitExceeded,
                ex.Message);
        }
        catch (OperationCanceledException)
        {
            return IsImageSearchTimeout(options.CancellationToken, timeoutCancellation)
                ? ScreenReadResult<ScreenImageMatch>.Failure(
                    ScreenReadErrorKind.CaptureTimeout,
                    $"Timed out while searching for screen image in region {effectiveMatchOptions.SearchRegion ?? frame.LogicalBounds}.")
                : ScreenReadResult<ScreenImageMatch>.Failure(
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
            options.CancellationToken.ThrowIfCancellationRequested();
            return await _frameProvider.CaptureFrameAsync(region, options).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.Canceled,
                "Screen frame capture was canceled.");
        }
    }

    private static bool IsImageSearchTimeout(CancellationToken callerToken, CancellationTokenSource? timeoutCancellation)
    {
        return timeoutCancellation is { IsCancellationRequested: true } && !callerToken.IsCancellationRequested;
    }
}
