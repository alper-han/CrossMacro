
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class RunScriptScreenReadRuntimeTests
{






















































    private static MacroPlayer CreatePlayer(
        IMousePositionProvider positionProvider,
        IScreenPixelReader screenReader,
        IInputSimulator? inputSimulator = null,
        IPlaybackTimingService? timingService = null,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IClipboardService? clipboardService = null,
        IKeyCodeMapper? keyCodeMapper = null)
    {
        keyCodeMapper ??= CreateKeyCodeMapper();
        return new MacroPlayer(
            new PlaybackValidator(keyCodeMapper, positionProvider),
            CreateDependencies(
            positionProvider,
            keyCodeMapper,
            inputSimulatorFactory ?? (() => inputSimulator ?? Substitute.For<IInputSimulator>()),
            timingService,
            screenReader,
            clipboardService));
    }

    private static MacroPlayerDependencies CreateDependencies(
        IMousePositionProvider positionProvider,
        IKeyCodeMapper keyCodeMapper,
        Func<IInputSimulator> inputSimulatorFactory,
        IPlaybackTimingService? timingService,
        IScreenPixelReader screenPixelReader,
        IClipboardService? clipboardService)
    {
        return new MacroPlayerDependencies(positionProvider, timingService ?? new SystemPlaybackTimingService(), (_, _) => Task.CompletedTask,
            CreateElapsedMillisecondsProvider, () => new DefaultPlaybackCoordinator(positionProvider), () => new ButtonStateTracker(),
            () => new KeyStateTracker(), new DefaultPlaybackMouseButtonMapper(), inputSimulatorFactory, simulatorPool: null,
            screenPixelReader, keyCodeMapper, new NullWindowManager(), clipboardService, shellCommandRunner: null,
            screenshotCaptureService: null, new ImageClickMovementResolver(positionProvider), new ImageAssetCodec(), new PlaybackDelayResolver());
    }

    private static Func<double> CreateElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        _ = keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        return keyCodeMapper;
    }

    private static IMousePositionProvider CreatePositionProvider((int X, int Y) position)
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.ProviderName.Returns("fake-position");
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(position));
        _ = positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        return positionProvider;
    }

    private static readonly ScreenPixelColor Black = new(0x00, 0x00, 0x00);
    private static readonly ScreenPixelColor Red = new(0xFF, 0x00, 0x00);
    private static readonly ScreenPixelColor Green = new(0x00, 0xFF, 0x00);
    private static readonly ScreenPixelColor Blue = new(0x00, 0x00, 0xFF);
    private static readonly ScreenPixelColor White = new(0xFF, 0xFF, 0xFF);

    private static ScreenPixelColor[][] Solid(int width, int height, ScreenPixelColor color)
    {
        var rows = new ScreenPixelColor[height][];
        for (var y = 0; y < height; y++)
        {
            rows[y] = new ScreenPixelColor[width];
            Array.Fill(rows[y], color);
        }

        return rows;
    }

    private static ScreenFrame CreateRgbFrame(ScreenRect bounds, ScreenPixelColor[][] pixels, byte[]? validPixelMask = null)
    {
        var stride = bounds.Width * 3;
        var bytes = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var offset = (y * stride) + (x * 3);
                bytes[offset] = pixels[y][x].R;
                bytes[offset + 1] = pixels[y][x].G;
                bytes[offset + 2] = pixels[y][x].B;
            }
        }

        var mask = validPixelMask is null ? ReadOnlyMemory<byte>.Empty : validPixelMask;
        return new ScreenFrame(bounds, stride, ScreenPixelFormat.Rgb24, bytes, validPixelMask: mask);
    }

    private static async Task<string> EncodePngBase64Async(ScreenFrame frame)
    {
        using var stream = new MemoryStream();
        await ScreenFramePngEncoder.EncodeAsync(frame, stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static byte[] CreateOversizedPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x1E, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08,
            0x02,
            0x00,
            0x00,
            0x00,
            0x6C, 0xF7, 0xBC, 0x13,
        ];
    }

    private sealed class FakeScreenPixelReader : IScreenPixelReader, IScreenImageSearchReader
    {
        private int _getPixelCallCount;

        public string ProviderName => "fake-screen-reader";

        public bool IsSupported => true;

        public ScreenPixelColor PixelColor { get; init; } = new(0x00, 0x00, 0x00);

        public ScreenPixelColor RelativePixelColor { get; init; } = new(0x00, 0x00, 0x00);

        public ScreenPixelSearchMatch SearchMatch { get; init; } = new(new ScreenPoint(0, 0), new ScreenPixelColor(0x00, 0x00, 0x00));

        public ScreenReadResult<ScreenPixelColor>? WaitResult { get; init; }

        public ScreenReadResult<ScreenPixelSearchMatch>? SearchResult { get; init; }

        public ScreenReadResult<ScreenImageMatch>? ImageSearchResult { get; init; }

        public ScreenReadOptions LastImageReadOptions { get; private set; }

        public ScreenImageMatchOptions LastImageOptions { get; private set; } = ScreenImageMatchOptions.Default;

        public TaskCompletionSource<object?> ImageSearchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ScreenPoint> GetPixelPoints { get; } = [];

        public List<(ScreenPoint Point, ScreenPixelColor Expected, ScreenReadOptions Options)> WaitCalls { get; } = [];

        public List<(ScreenRect Region, ScreenPixelColor Expected, int Tolerance, ScreenReadOptions Options)> SearchCalls { get; } = [];

        public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            GetPixelPoints.Add(point);
            var color = _getPixelCallCount++ is 0 ? PixelColor : RelativePixelColor;
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(color));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
            ScreenPoint point,
            ScreenPixelColor expected,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            WaitCalls.Add((point, expected, options));
            return Task.FromResult(WaitResult ?? ScreenReadResultFactory.Success<ScreenPixelColor>(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
            ScreenRect region,
            ScreenPixelColor expected,
            int tolerance,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            SearchCalls.Add((region, expected, tolerance, options));
            return Task.FromResult(SearchResult ?? ScreenReadResultFactory.Success<ScreenPixelSearchMatch>(SearchMatch));
        }

        public Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
            ScreenRect? region,
            ScreenFrame imageTemplate,
            ScreenImageMatchOptions options,
            ScreenReadOptions readOptions)
        {
            readOptions.CancellationToken.ThrowIfCancellationRequested();
            _ = ImageSearchStarted.TrySetResult(null);
            LastImageOptions = options;
            LastImageReadOptions = readOptions;
            return Task.FromResult(ImageSearchResult ?? ScreenReadResultFactory.Success<ScreenImageMatch>(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0)));
        }

        public void Dispose()
        {
        }
    }

    private sealed class SingleFrameProvider(ScreenFrame frame) : IScreenFrameProvider
    {
        private readonly ScreenFrame _frame = frame;

        public string ProviderName => "single-frame-provider";

        public bool IsSupported => true;

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenFrame>(_frame));
        }

        public void Dispose()
        {
        }
    }

    private sealed class DelayedFrameProvider(ScreenFrame frame) : IScreenFrameProvider
    {
        private readonly ScreenFrame _frame = frame;

        public string ProviderName => "delayed-frame-provider";

        public bool IsSupported => true;

        public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            return ScreenReadResultFactory.Success<ScreenFrame>(_frame);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingScreenPixelReader(List<string> activity) : IScreenPixelReader
    {
        private readonly List<string> _activity = activity;

        public string ProviderName => "recording-screen-reader";

        public bool IsSupported => true;

        public ScreenPixelColor PixelColor { get; init; } = new(0x12, 0x34, 0x56);

        public ScreenReadResult<ScreenPixelColor>? WaitResult { get; init; }

        public ScreenReadResult<ScreenPixelSearchMatch>? SearchResult { get; init; }

        public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add($"screen:pixelcolor:{point.X},{point.Y}");
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(PixelColor));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
            ScreenPoint point,
            ScreenPixelColor expected,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add($"screen:waitcolor:{point.X},{point.Y}");
            return Task.FromResult(WaitResult ?? ScreenReadResultFactory.Success<ScreenPixelColor>(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
            ScreenRect region,
            ScreenPixelColor expected,
            int tolerance,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add($"screen:pixelsearch:{region.X},{region.Y}");
            return Task.FromResult(SearchResult ?? ScreenReadResultFactory.Success<ScreenPixelSearchMatch>(new ScreenPixelSearchMatch(new ScreenPoint(region.X, region.Y), expected)));
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingInputSimulator(List<string> activity) : IInputSimulator, IInputSimulatorCapabilities
    {
        private readonly List<string> _activity = activity;

        public string ProviderName => "recording-input-simulator";

        public bool IsSupported => true;

        public bool SupportsAbsoluteCoordinates { get; init; } = true;

        public int InitializedWidth { get; private set; }

        public int InitializedHeight { get; private set; }

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
            InitializedWidth = screenWidth;
            InitializedHeight = screenHeight;
        }

        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(screenWidth, screenHeight);
            return Task.CompletedTask;
        }

        public void MoveAbsolute(int x, int y)
        {
            _activity.Add($"input:move-abs:{x},{y}");
        }

        public void MoveRelative(int dx, int dy)
        {
            _activity.Add($"input:move:{dx},{dy}");
        }

        public void MouseButton(int button, bool pressed)
        {
            if (pressed)
            {
                var name = button switch
                {
                    MouseButtonCode.Right => "right",
                    MouseButtonCode.Middle => "middle",
                    _ => "left",
                };
                _activity.Add($"input:click:{name}");
            }
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
        }

        public void KeyPress(int keyCode, bool pressed)
        {
            _activity.Add($"input:key:{keyCode}:{(pressed ? "down" : "up")}");
        }

        public void Sync()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingTimingService : IPlaybackTimingService
    {
        public List<double> WaitCalls { get; } = [];

        public async Task WaitAsync(double delayMilliseconds, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
        {
            WaitCalls.Add(delayMilliseconds);
            if (pauseToken.IsPaused)
            {
                await pauseToken.WaitIfPausedAsync(cancellationToken);
            }
        }
    }

    private sealed class DisposalTrackingFrameProvider(params ScreenPixelColor[] colors) : IScreenFrameProvider
    {
        private readonly Queue<ScreenPixelColor> _colors = new Queue<ScreenPixelColor>(colors);

        public string ProviderName => "disposal-tracking-frame-provider";

        public bool IsSupported => true;

        public int CaptureCalls { get; private set; }

        public Action? AfterCapture { get; init; }

        public List<CountingDisposable> Owners { get; } = [];

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            CaptureCalls++;

            var bounds = region ?? new ScreenRect(1, 2, 1, 1);
            var owner = new CountingDisposable();
            Owners.Add(owner);
            var frame = CreateFrame(bounds, _colors.Dequeue(), owner);
            AfterCapture?.Invoke();
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenFrame>(frame));
        }

        public void Dispose()
        {
        }

        private static ScreenFrame CreateFrame(ScreenRect bounds, ScreenPixelColor color, IDisposable owner)
        {
            var pixels = new[] { color.B, color.G, color.R, (byte)0x00 };
            return new ScreenFrame(bounds, 4, ScreenPixelFormat.Xrgb8888, pixels, owner);
        }
    }

    private sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
