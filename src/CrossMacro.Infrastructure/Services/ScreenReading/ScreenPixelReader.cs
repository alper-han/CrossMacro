using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenPixelReader : IScreenPixelReader
{
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(50);

    private readonly IScreenFrameProvider _frameProvider;
    private bool _disposed;

    public ScreenPixelReader(IScreenFrameProvider frameProvider)
    {
        _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
    }

    public string ProviderName => _frameProvider.ProviderName;

    public bool IsSupported => _frameProvider.IsSupported;

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
        var match = frame.SearchPixel(region, expected, tolerance);
        return match is { } found
            ? ScreenReadResult<ScreenPixelSearchMatch>.Success(found)
            : ScreenReadResult<ScreenPixelSearchMatch>.Failure(
                ScreenReadErrorKind.CaptureTimeout,
                $"No pixel matching {expected} was found in region {region}.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
}
