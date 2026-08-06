// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class RunScriptScreenReadRuntimeTests
{

    [Fact]
    public async Task SearchImageAsync_WhenCapturedRegionHasOnlyInvalidMaskedPixels_ReturnsOutOfBounds()
    {
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 1),
            [[Black, Black]],
            validPixelMask: [0, 0]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var reader = new ScreenPixelReader(new SingleFrameProvider(frame));

        var result = await reader.SearchImageAsync(
            new ScreenRect(0, 0, 2, 1),
            template,
            ScreenImageMatchOptions.Default,
            ScreenReadOptions.Default);

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.OutOfBounds);
    }

    [Fact]
    public async Task SearchImageAsync_WhenCanceledBeforeCapture_ReturnsCanceled()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var reader = new ScreenPixelReader(new SingleFrameProvider(frame));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await reader.SearchImageAsync(
region: null,
            template,
            ScreenImageMatchOptions.Default,
            new ScreenReadOptions(cancellationToken: cts.Token));

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.Canceled);
    }

    [Fact]
    public async Task SearchImageAsync_WhenTimeoutExpiresBeforeBestMatch_ReturnsCaptureTimeout()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var reader = new ScreenPixelReader(new DelayedFrameProvider(frame));

        var result = await reader.SearchImageAsync(
region: null,
            template,
            ScreenImageMatchOptions.Create(searchRegion: null, 1.0, 1, ScreenImageMatchSelectionMode.BestMatch),
            new ScreenReadOptions(timeout: TimeSpan.FromMilliseconds(1)));

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.CaptureTimeout);
        _ = result.ErrorMessage.Should().Contain("Timed out");
    }

    [Fact]
    public async Task SearchImageAsync_WhenCallerCancellationWinsOverTimeout_ReturnsCanceled()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var provider = new CancellationAwareFrameProvider(frame);
        using var reader = new ScreenPixelReader(provider);
        using var cts = new CancellationTokenSource();

        var searchTask = reader.SearchImageAsync(
            region: null,
            template,
            ScreenImageMatchOptions.Default,
            new ScreenReadOptions(timeout: TimeSpan.FromSeconds(1), cancellationToken: cts.Token));
        _ = await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);
        await cts.CancelAsync();

        var result = await searchTask;

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.Canceled);
    }

    [Fact]
    public async Task PlayAsync_WhenWaitImageIsCanceledBeforeTimeout_PropagatesCancellation()
    {
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        var screenReader = new FakeScreenPixelReader
        {
            ImageSearchResult = ScreenReadResultFactory.Failure<ScreenImageMatch>(
                ScreenReadErrorKind.CaptureTimeout,
                "No image matching the template was found."),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        using var cts = new CancellationTokenSource();
        var macro = new MacroSequence
        {
            ScriptSteps = { "waitimage Target found found_x found_y timeout 5000" },
            Images = { ["Target"] = await EncodePngBase64Async(template) },
        };

        var playbackTask = player.PlayAsync(macro, cancellationToken: cts.Token);
        _ = await screenReader.ImageSearchStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);
        await cts.CancelAsync();

        var act = async () => await playbackTask;
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
        _ = screenReader.LastImageReadOptions.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task PlayAsync_WhenWaitImageTimeoutExpires_ForwardsRemainingDeadlineToImageSearch()
    {
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        var screenReader = new FakeScreenPixelReader
        {
            ImageSearchResult = ScreenReadResultFactory.Failure<ScreenImageMatch>(
                ScreenReadErrorKind.CaptureTimeout,
                "No image matching the template was found."),
        };
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        var macro = new MacroSequence
        {
            ScriptSteps = { "waitimage Target found found_x found_y timeout 0" },
            Images = { ["Target"] = await EncodePngBase64Async(template) },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = screenReader.LastImageReadOptions.Timeout.Should().Be(TimeSpan.Zero);
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("found", "false");
        _ = variables.Should().Contain("found_x", "-1");
        _ = variables.Should().Contain("found_y", "-1");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchFindsTemplate_StoresFoundFlagAndCoordinates()
    {
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 4, 3),
            [
                [Black, Black, Black, Black],
                [Black, Red, Green, Black],
                [Black, Blue, White, Black],
            ]);
        using var template = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 2),
            [
                [Red, Green],
                [Blue, White],
            ]);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(frame)));
        var macro = new MacroSequence
        {
            ScriptSteps = { "imagesearch 0 0 4 3 Target found found_x found_y similarity 1 downsample 1" },
            Images = { ["Target"] = await EncodePngBase64Async(template) },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("found", "true");
        _ = variables.Should().Contain("found_x", "1");
        _ = variables.Should().Contain("found_y", "1");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchUsesBestMode_ForwardsSelectionMode()
    {
        var screenReader = new FakeScreenPixelReader();
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), screenReader);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        var macro = new MacroSequence
        {
            ScriptSteps = { "imagesearch Target found found_x found_y matchmode best" },
            Images = { ["Target"] = await EncodePngBase64Async(template) },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = screenReader.LastImageOptions.SelectionMode.Should().Be(ScreenImageMatchSelectionMode.BestMatch);
    }

    [Fact]
    public async Task SearchImageAsync_WhenCaptureExceedsTimeout_ReturnsCaptureTimeout()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var reader = new ScreenPixelReader(new DelayedFrameProvider(frame));

        var result = await reader.SearchImageAsync(
region: null,
            template,
            ScreenImageMatchOptions.Create(searchRegion: null, 1.0, 1, ScreenImageMatchSelectionMode.FirstThresholdMatch),
            new ScreenReadOptions(timeout: TimeSpan.FromMilliseconds(1)));

        _ = result.IsSuccess.Should().BeFalse($"{result.ErrorKind}: {result.ErrorMessage}");
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.CaptureTimeout);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenImageSearchHasUnknownOption_RejectsTheStep()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        var executor = new RunScriptScreenReadExecutor(new ScreenPixelReader(new SingleFrameProvider(frame)), mousePositionProvider: null);

        var act = async () => await executor.ExecuteStepAsync(
            "imagesearch Target found found_x found_y unsupported 1",
            3,
            new Dictionary<string, string>(StringComparer.Ordinal),
            CancellationToken.None,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Target"] = await EncodePngBase64Async(template) });

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 3: imagesearch failed: Invalid imagesearch syntax.*");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchDoesNotFindTemplate_StoresFalseAndNegativeCoordinates()
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 2, 2), Solid(2, 2, Black));
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Red]]);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(frame)));
        var macro = new MacroSequence
        {
            ScriptSteps = { "imagesearch MissingPixel found found_x found_y" },
            Images = { ["MissingPixel"] = await EncodePngBase64Async(template) },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("found", "false");
        _ = variables.Should().Contain("found_x", "-1");
        _ = variables.Should().Contain("found_y", "-1");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchImageAssetIsMissing_ThrowsStepNumberedFailure()
    {
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(
            CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]))));
        var macro = new MacroSequence
        {
            ScriptSteps = { "imagesearch Target found found_x found_y" },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 1: imagesearch failed: image asset 'Target' is not defined.");
    }

    [Theory]
    [InlineData("not-base64", "not valid Base64")]
    [InlineData("bm90IGEgcG5n", "not a supported PNG")]
    public async Task PlayAsync_WhenImageSearchImageAssetIsInvalid_ThrowsStepNumberedFailure(string asset, string expectedMessage)
    {
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(
            CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]))));
        var macro = new MacroSequence
        {
            ScriptSteps = { "imagesearch Target found found_x found_y" },
            Images = { ["Target"] = asset },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Step 1: imagesearch failed: image asset 'Target' is {expectedMessage}*");
    }

    [Fact]
    public async Task PlayAsync_WhenImageSearchImageAssetExceedsSupportedDimensions_ThrowsStepNumberedFailure()
    {
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new ScreenPixelReader(new SingleFrameProvider(
            CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]))));
        var macro = new MacroSequence
        {
            ScriptSteps = { "imagesearch Target found found_x found_y" },
            Images = { ["Target"] = Convert.ToBase64String(CreateOversizedPngBytes()) },
        };

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 1: imagesearch failed: image asset 'Target' is not a supported PNG: *maximum supported size of 7680x4320*");
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task ExecuteStepAsync_WhenImageSearchSimilarityIsNotFinite_ThrowsStepNumberedFailure(string similarity)
    {
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Black]]);
        var executor = new RunScriptScreenReadExecutor(new ScreenPixelReader(new SingleFrameProvider(frame)), mousePositionProvider: null);
        var runtimeVariables = new Dictionary<string, string>(StringComparer.Ordinal);
        var step = $"imagesearch Target found found_x found_y similarity {similarity}";

        var act = async () => await executor.ExecuteStepAsync(
            step,
            1,
            runtimeVariables,
            CancellationToken.None,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Target"] = await EncodePngBase64Async(template) });

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 1: imagesearch failed: Invalid imagesearch similarity. Expected number between 0.0 and 1.0.");
    }

    [Fact]
    public async Task PlayAsync_WhenImageClickFindsTemplate_ClicksTemplateCenter()
    {
        var activity = new List<string>();
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 4, 3),
            [
                [Black, Black, Black, Black],
                [Black, Red, Green, Black],
                [Black, Blue, White, Black],
            ]);
        using var template = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 2),
            [
                [Red, Green],
                [Blue, White],
            ]);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            new ScreenPixelReader(new SingleFrameProvider(frame)),
            inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps = { "imageclick Target clicked click_x click_y button right similarity 1 downsample 1" },
            Images = { ["Target"] = await EncodePngBase64Async(template) },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal("input:move-abs:2,2", "input:click:right");
        _ = inputSimulator.InitializedWidth.Should().Be(1920);
        _ = inputSimulator.InitializedHeight.Should().Be(1080);
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("clicked", "true");
        _ = variables.Should().Contain("click_x", "2");
        _ = variables.Should().Contain("click_y", "2");
    }

    [Fact]
    public async Task PlayAsync_WhenImageClickLacksAbsoluteCoordinates_UsesRelativeMovementAndPreservesSignedVariables()
    {
        var activity = new List<string>();
        using var frame = CreateRgbFrame(
            new ScreenRect(0, 0, 4, 4),
            [
                [Black, Black, Black, Black],
                [Black, Red, Green, Black],
                [Black, Blue, White, Black],
                [Black, Black, Black, Black],
            ]);
        using var template = CreateRgbFrame(
            new ScreenRect(0, 0, 2, 2),
            [
                [Red, Green],
                [Blue, White],
            ]);
        var inputSimulator = new RecordingInputSimulator(activity) { SupportsAbsoluteCoordinates = false };
        using var player = CreatePlayer(
            CreatePositionProvider((10, 20)),
            new ScreenPixelReader(new SingleFrameProvider(frame)),
            inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps = { "imageclick Target clicked click_x click_y" },
            Images = { ["Target"] = await EncodePngBase64Async(template) },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().Equal("input:move:-8,-18", "input:click:left");
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("clicked", "true");
        _ = variables.Should().Contain("click_x", "2");
        _ = variables.Should().Contain("click_y", "2");
    }

    [Fact]
    public async Task PlayAsync_WhenImageClickDoesNotFindTemplateAndHasVariables_StoresFalseAndDoesNotClick()
    {
        var activity = new List<string>();
        using var frame = CreateRgbFrame(new ScreenRect(0, 0, 2, 2), Solid(2, 2, Black));
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Red]]);
        var inputSimulator = new RecordingInputSimulator(activity);
        using var player = CreatePlayer(
            CreatePositionProvider((0, 0)),
            new ScreenPixelReader(new SingleFrameProvider(frame)),
            inputSimulator);
        var macro = new MacroSequence
        {
            ScriptSteps = { "imageclick MissingPixel clicked click_x click_y" },
            Images = { ["MissingPixel"] = await EncodePngBase64Async(template) },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().BeEmpty();
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("clicked", "false");
        _ = variables.Should().Contain("click_x", "-1");
        _ = variables.Should().Contain("click_y", "-1");
    }

    [Fact]
    public async Task PlayAsync_WhenWaitImageFindsTemplateAfterPolling_StoresFoundFlagAndCoordinates()
    {
        using var template = CreateRgbFrame(new ScreenRect(0, 0, 1, 1), [[Red]]);
        using var provider = new DisposalTrackingFrameProvider(Black, Red);
        using var reader = new ScreenPixelReader(provider);
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), reader);
        var macro = new MacroSequence
        {
            ScriptSteps = { "waitimage Target found found_x found_y timeout 1000 similarity 1" },
            Images = { ["Target"] = await EncodePngBase64Async(template) },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = provider.CaptureCalls.Should().Be(2);
        _ = reader.TemplateNormalizationCount.Should().Be(1);
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("found", "true");
        _ = variables.Should().Contain("found_x", "1");
        _ = variables.Should().Contain("found_y", "2");
    }

    private sealed class CancellationAwareFrameProvider(ScreenFrame frame) : IScreenFrameProvider
    {
        private readonly ScreenFrame _frame = frame;

        public string ProviderName => "cancellation-aware-frame-provider";

        public bool IsSupported => true;

        public TaskCompletionSource<object?> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            _ = Started.TrySetResult(null);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, options.CancellationToken);
                return ScreenReadResultFactory.Success<ScreenFrame>(_frame);
            }
            catch (OperationCanceledException)
            {
                return ScreenReadResultFactory.Failure<ScreenFrame>(
                    ScreenReadErrorKind.Canceled,
                    "Screen frame capture was canceled.");
            }
        }

        public void Dispose()
        {
        }
    }
}
