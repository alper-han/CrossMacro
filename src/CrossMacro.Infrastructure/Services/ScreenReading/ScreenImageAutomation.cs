
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenImageAutomation : IScreenImageAutomation
{
    private readonly IScreenPixelReader _screenPixelReader;
    private readonly IImageAssetCodec _imageAssetCodec;
    private readonly IMousePositionProvider? _mousePositionProvider;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;
    private readonly IInputSimulatorPool? _simulatorPool;
    private readonly IImageClickMovementResolver _movementResolver;

    public ScreenImageAutomation(
        IScreenPixelReader screenPixelReader,
        IImageAssetCodec imageAssetCodec,
        IMousePositionProvider? mousePositionProvider,
        Func<IInputSimulator>? inputSimulatorFactory,
        IInputSimulatorPool? simulatorPool,
        IImageClickMovementResolver movementResolver)
    {
        _screenPixelReader = screenPixelReader ?? throw new ArgumentNullException(nameof(screenPixelReader));
        _imageAssetCodec = imageAssetCodec ?? throw new ArgumentNullException(nameof(imageAssetCodec));
        _mousePositionProvider = mousePositionProvider;
        _inputSimulatorFactory = inputSimulatorFactory;
        _simulatorPool = simulatorPool;
        _movementResolver = movementResolver ?? throw new ArgumentNullException(nameof(movementResolver));
    }

    public string ProviderName => _screenPixelReader.ProviderName;

    public bool IsSupported => _screenPixelReader.IsSupported && _screenPixelReader is IScreenImageSearchReader;

    public async Task<ScreenImageAutomationResult> SearchAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
    {
        var setup = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        if (setup.Error is { } error)
        {
            return error;
        }

        using (setup.Template)
        {
            return ToResult(await SearchOnceAsync(setup, request.Timeout, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<ScreenImageAutomationResult> WaitAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
    {
        var setup = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        if (setup.Error is { } error)
        {
            return error;
        }

        using (setup.Template)
        {
            var timeout = request.Timeout ?? TimeSpan.FromSeconds(5);
            var deadline = DateTimeOffset.UtcNow + timeout;
            var pollInterval = ScreenReadOptions.Default.PollInterval ?? TimeSpan.FromMilliseconds(50);
            while (true)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }

                var result = await SearchOnceAsync(setup, remaining, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    return ToResult(result);
                }

                if (result.ErrorKind is not ScreenReadErrorKind.CaptureTimeout)
                {
                    return ToResult(result);
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    return ToResult(result);
                }

                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<ScreenImageAutomationResult> ClickAsync(ScreenImageAutomationRequest request, int buttonCode, CancellationToken cancellationToken)
    {
        if (_inputSimulatorFactory is null)
        {
            return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, "No supported IInputSimulator is available for the current platform/session.");
        }

        var resolution = _mousePositionProvider is null ? null : await _mousePositionProvider.GetScreenResolutionAsync().ConfigureAwait(false);
        var width = resolution?.Width ?? 0;
        var height = resolution?.Height ?? 0;
        var simulator = _simulatorPool?.Acquire(width, height) ?? _inputSimulatorFactory();
        var pooled = _simulatorPool is not null;
        try
        {
            if (!simulator.IsSupported)
            {
                return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, "No supported IInputSimulator is available for the current platform/session.");
            }

            var setup = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
            if (setup.Error is { } error)
            {
                return error;
            }

            using (setup.Template)
            {
                var result = await SearchOnceAsync(setup, request.Timeout, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return ToResult(result);
                }

                var point = new ScreenPoint(
                    checked(result.Value.Point.X + (setup.Template!.LogicalBounds.Width / 2)),
                    checked(result.Value.Point.Y + (setup.Template.LogicalBounds.Height / 2)));
                simulator.Initialize(width, height);
                var movement = await _movementResolver.ResolveAsync(simulator, point, cancellationToken).ConfigureAwait(false);
                if (!movement.IsSuccess)
                {
                    return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, movement.ErrorMessage ?? "Image click movement could not be resolved.");
                }

                if (movement.CoordinateMode is MouseCoordinateMode.Absolute)
                {
                    simulator.MoveAbsolute(movement.X, movement.Y);
                }
                else
                {
                    simulator.MoveRelative(movement.X, movement.Y);
                }

                simulator.MouseButton(buttonCode, pressed: true);
                simulator.MouseButton(buttonCode, pressed: false);
                simulator.Sync();
                return ScreenImageAutomationResult.FoundAt(point, result.Value.Score);
            }
        }
        finally
        {
            if (pooled)
            {
                _simulatorPool!.Release(simulator, width, height);
            }
            else
            {
                simulator.Dispose();
            }
        }
    }

    private async Task<ImageSearchSetup> PrepareAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            return ImageSearchSetup.Failure(ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, "Screen image matching is not supported in this runtime."));
        }

        if (!double.IsFinite(request.Similarity) || request.Similarity is < 0.0 or > 1.0 || request.Downsample < 1)
        {
            return ImageSearchSetup.Failure(ScreenImageAutomationResult.Failure(ScreenReadErrorKind.InvalidArguments, "Invalid image search options."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var template = await _imageAssetCodec.DecodeFileAsync(request.ImagePath, cancellationToken).ConfigureAwait(false);
            var options = ScreenImageMatchOptions.Create(
                request.Region,
                request.Similarity,
                request.Downsample,
                request.MatchMode is ScreenImageMatchMode.Best
                    ? ScreenImageMatchSelectionMode.BestMatch
                    : ScreenImageMatchSelectionMode.FirstThresholdMatch,
                request.ScaleAware);
            return ImageSearchSetup.Success((IScreenImageSearchReader)_screenPixelReader, template, options);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return ImageSearchSetup.Failure(ScreenImageAutomationResult.Failure(ScreenReadErrorKind.InvalidArguments, $"Image file was not found: {ex.Message}"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return ImageSearchSetup.Failure(ScreenImageAutomationResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            return ImageSearchSetup.Failure(ScreenImageAutomationResult.Failure(ScreenReadErrorKind.InvalidArguments, $"Image file is not a supported PNG: {ex.Message}"));
        }
    }

    private static async Task<ScreenReadResult<ScreenImageMatch>> SearchOnceAsync(ImageSearchSetup setup, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        return await setup.Reader!.SearchImageAsync(
            setup.Region,
            setup.Template!,
            setup.Options!,
            new ScreenReadOptions(timeout, ScreenReadOptions.Default.PollInterval, cancellationToken)).ConfigureAwait(false);
    }

    private static ScreenImageAutomationResult ToResult(ScreenReadResult<ScreenImageMatch> result) =>
        result.IsSuccess
            ? ScreenImageAutomationResult.FoundAt(result.Value.Point, result.Value.Score)
            : ScreenImageAutomationResult.Failure(result.ErrorKind ?? ScreenReadErrorKind.CaptureFailed, result.ErrorMessage ?? "Screen image search failed.");

    private sealed record ImageSearchSetup(
        IScreenImageSearchReader? Reader,
        ScreenFrame? Template,
        ScreenRect? Region,
        ScreenImageMatchOptions? Options,
        ScreenImageAutomationResult? Error)
    {
        public static ImageSearchSetup Success(IScreenImageSearchReader reader, ScreenFrame template, ScreenImageMatchOptions options) =>
            new(reader, template, options.SearchRegion, options, Error: null);

        public static ImageSearchSetup Failure(ScreenImageAutomationResult error) =>
            new(Reader: null, Template: null, Region: null, Options: null, error);
    }
}
