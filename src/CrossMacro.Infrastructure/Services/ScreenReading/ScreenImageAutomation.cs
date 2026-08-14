
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenImageAutomation(
    IScreenPixelReader screenPixelReader,
    IImageAssetCodec imageAssetCodec,
    IMousePositionProvider? mousePositionProvider,
    Func<IInputSimulator>? inputSimulatorFactory,
    IInputSimulatorPool? simulatorPool,
    IImageClickMovementResolver movementResolver) : IScreenImageAutomation
{
    private readonly IScreenPixelReader _screenPixelReader = screenPixelReader ?? throw new ArgumentNullException(nameof(screenPixelReader));
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec ?? throw new ArgumentNullException(nameof(imageAssetCodec));
    private readonly IMousePositionProvider? _mousePositionProvider = mousePositionProvider;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory = inputSimulatorFactory;
    private readonly IInputSimulatorPool? _simulatorPool = simulatorPool;
    private readonly IImageClickMovementResolver _movementResolver = movementResolver ?? throw new ArgumentNullException(nameof(movementResolver));

    public string ProviderName => _screenPixelReader.ProviderName;

    public bool IsSupported => _screenPixelReader.IsSupported && _screenPixelReader is IScreenImageSearchReader;

    public async Task<ScreenImageAutomationResult> SearchAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var setup = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        if (setup.Error is { } error)
        {
            return error;
        }

        using (setup.Template)
        {
            return ToResult(await SearchOnceAsync(setup, ScreenReadOptions.DefaultTimeout, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<ScreenImageAutomationResult> WaitAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var setup = await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
        if (setup.Error is { } error)
        {
            return error;
        }

        using (setup.Template)
        {
            var timeout = request.Timeout ?? ScreenReadOptions.DefaultTimeout;
            return ToResult(await SearchUntilConsistentAsync(setup, timeout, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<ScreenImageAutomationResult> ClickAsync(ScreenImageAutomationRequest request, int buttonCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_inputSimulatorFactory is null && _simulatorPool is null)
        {
            return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, "No supported IInputSimulator is available for the current platform/session.");
        }

        var geometry = await ResolveDesktopGeometryAsync(cancellationToken).ConfigureAwait(false);
        var width = geometry.Bounds?.Width ?? geometry.Width;
        var height = geometry.Bounds?.Height ?? geometry.Height;
        var pool = _simulatorPool;
        var factory = _inputSimulatorFactory;
        var leasedFromPool = pool is not null;
        IInputSimulator simulator;
        if (pool is not null)
        {
            simulator = await pool.AcquireAsync(width, height, cancellationToken).ConfigureAwait(false);
        }
        else if (factory is not null)
        {
            simulator = factory();
        }
        else
        {
            return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, "No supported IInputSimulator is available for the current platform/session.");
        }
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
                var timeout = request.Timeout ?? ScreenReadOptions.DefaultTimeout;
                var result = await SearchUntilConsistentAsync(setup, timeout, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return ToResult(result);
                }

                var template = setup.Template ?? throw new InvalidOperationException("Template is not initialized in a success setup.");
                var point = new ScreenPoint(
                    checked(result.Value.Point.X + ((result.Value.MatchedWidth > 0 ? result.Value.MatchedWidth : template.LogicalBounds.Width) / 2)),
                    checked(result.Value.Point.Y + ((result.Value.MatchedHeight > 0 ? result.Value.MatchedHeight : template.LogicalBounds.Height) / 2)));
                if (!leasedFromPool)
                {
                    await simulator.InitializeAsync(width, height, cancellationToken).ConfigureAwait(false);
                }
                var movement = await _movementResolver.ResolveAsync(simulator, point, cancellationToken).ConfigureAwait(false);
                if (!movement.IsSuccess)
                {
                    return ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, movement.ErrorMessage ?? "Image click movement could not be resolved.");
                }

                if (movement.CoordinateMode is MouseCoordinateMode.Absolute)
                {
                    var devicePoint = AbsoluteInputCoordinateMapper.ToDeviceCoordinates(
                        simulator,
                        geometry.Bounds,
                        movement.X,
                        movement.Y);
                    simulator.MoveAbsolute(devicePoint.X, devicePoint.Y);

                    var settleResult = await AbsoluteCursorPositionSynchronizer.WaitAsync(
                        _mousePositionProvider,
                        movement.X,
                        movement.Y,
                        cancellationToken).ConfigureAwait(false);
                    if (!settleResult.IsSettled)
                    {
                        return ScreenImageAutomationResult.Failure(
                            ScreenReadErrorKind.CaptureFailed,
                            $"Absolute cursor move did not settle at ({movement.X.ToString(CultureInfo.InvariantCulture)},{movement.Y.ToString(CultureInfo.InvariantCulture)}).");
                    }
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
            if (pool is not null)
            {
                pool.Release(simulator, width, height);
            }
            else
            {
                simulator.Dispose();
            }
        }
    }

    private async Task<(ScreenRect? Bounds, int Width, int Height)> ResolveDesktopGeometryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_mousePositionProvider is null)
        {
            return (Bounds: null, Width: 0, Height: 0);
        }

        var bounds = await _mousePositionProvider.GetDesktopBoundsAsync().ConfigureAwait(false);
        if (bounds is { Width: > 0, Height: > 0 })
        {
            return (bounds, bounds.Value.Width, bounds.Value.Height);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var resolution = await _mousePositionProvider.GetScreenResolutionAsync().ConfigureAwait(false);
        return resolution is { Width: > 0, Height: > 0 }
            ? (Bounds: new ScreenRect(0, 0, resolution.Value.Width, resolution.Value.Height), resolution.Value.Width, resolution.Value.Height)
            : (Bounds: null, Width: 0, Height: 0);
    }

    private async Task<ImageSearchSetup> PrepareAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
    {
        if (!IsSupported)
        {
            return ImageSearchSetup.Failure(ScreenImageAutomationResult.Failure(ScreenReadErrorKind.Unsupported, "Screen image matching is not supported in this runtime."));
        }

        if (!double.IsFinite(request.Similarity)
            || request.Similarity is < 0.0 or > 1.0
            || !Enum.IsDefined(request.MatchMode)
            || (request.Timeout is { } timeout && timeout < TimeSpan.Zero))
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
                request.MatchMode switch
                {
                    ScreenImageMatchMode.Automatic => ScreenImageMatchSelectionMode.Automatic,
                    ScreenImageMatchMode.Best => ScreenImageMatchSelectionMode.BestMatch,
                    ScreenImageMatchMode.First => ScreenImageMatchSelectionMode.FirstThresholdMatch,
                    _ => throw new InvalidOperationException("Image match mode is invalid."),
                });
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

    private static async Task<ScreenReadResult<ScreenImageMatch>> SearchOnceAsync(
        ImageSearchSetup setup,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var reader = setup.Reader ?? throw new InvalidOperationException("Reader is not initialized in a success setup.");
        var template = setup.Template ?? throw new InvalidOperationException("Template is not initialized in a success setup.");
        var options = setup.Options ?? throw new InvalidOperationException("Options is not initialized in a success setup.");
        return await reader.SearchImageAsync(
            setup.Region,
            template,
            options,
            new ScreenReadOptions(
                timeout,
                pollInterval: null,
                pollUntilMatch: false,
                cancellationToken)).ConfigureAwait(false);
    }

    private static Task<ScreenReadResult<ScreenImageMatch>> SearchUntilConsistentAsync(
        ImageSearchSetup setup,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return ScreenReadPolling.PollImageUntilConsistentAsync(
            (remaining, token) => SearchOnceAsync(setup, remaining, token),
            timeout,
            ScreenReadOptions.DefaultPollInterval,
            cancellationToken);
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
