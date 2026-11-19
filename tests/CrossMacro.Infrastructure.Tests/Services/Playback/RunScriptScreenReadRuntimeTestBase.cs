
using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public abstract class RunScriptScreenReadRuntimeTestBase
{
    internal static MacroPlayer CreatePlayer(
        IMousePositionProvider positionProvider,
        IScreenImageSearchReader screenReader,
        IInputSimulator? inputSimulator = null,
        IPlaybackTimingService? timingService = null,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        IClipboardService? clipboardService = null,
        IKeyCodeMapper? keyCodeMapper = null)
    {
        keyCodeMapper ??= MacroPlayerTestFactory.CreateKeyCodeMapper();
        return new MacroPlayer(
            new PlaybackValidator(keyCodeMapper, positionProvider),
            MacroPlayerTestFactory.CreateDependencies(
                positionProvider,
                keyCodeMapper,
                inputSimulatorFactory ?? (() => inputSimulator ?? Substitute.For<IInputSimulator>()),
                timingService,
                screenPixelReader: screenReader,
                clipboardService: clipboardService));
    }

    internal static IMousePositionProvider CreatePositionProvider((int X, int Y) position)
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.ProviderName.Returns("fake-position");
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(position));
        _ = positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        return positionProvider;
    }


    internal static async Task<string> EncodePngBase64Async(ScreenFrame frame)
    {
        using var stream = new MemoryStream();
        await ScreenFramePngEncoder.EncodeAsync(frame, stream, TestContext.Current.CancellationToken);
        return Convert.ToBase64String(stream.ToArray());
    }

    internal sealed class FakeScreenPixelReader : IScreenImageSearchReader
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

        public List<ScreenReadOptions> ImageReadOptions { get; } = [];

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
            ImageReadOptions.Add(readOptions);
            return Task.FromResult(ImageSearchResult ?? ScreenReadResultFactory.Success<ScreenImageMatch>(new ScreenImageMatch(new ScreenPoint(0, 0), 1.0)));
        }

        public void Dispose()
        {
        }
    }

    internal sealed class SingleFrameProvider(ScreenFrame frame) : IScreenFrameProvider
    {
        private readonly ScreenFrame _frame = frame;

        public string ProviderName => "single-frame-provider";

        public bool IsSupported => true;

        public int CaptureCalls { get; private set; }

        public ScreenReadOptions LastOptions { get; private set; }

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            CaptureCalls++;
            LastOptions = options;
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenFrame>(_frame));
        }

        public void Dispose()
        {
        }
    }

    internal sealed class SuccessfulFrameAfterCancellationProvider(ScreenFrame frame) : IScreenFrameProvider
    {
        private readonly ScreenFrame _frame = frame;

        public string ProviderName => "successful-frame-after-cancellation-provider";

        public bool IsSupported => true;

        public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            var canceled = new AsyncSignal();
            using var registration = options.CancellationToken.Register(canceled.Signal);
            await canceled.WaitAsync(TimeSpan.FromSeconds(1));
            return ScreenReadResultFactory.Success<ScreenFrame>(_frame);
        }

        public void Dispose()
        {
        }
    }

    internal sealed class DisposalTrackingFrameProvider(params ScreenPixelColor[] colors) : IScreenFrameProvider
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

    internal sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
