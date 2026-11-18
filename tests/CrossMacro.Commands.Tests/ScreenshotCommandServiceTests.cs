
namespace CrossMacro.Commands.Tests;

public sealed class ScreenshotCommandServiceTests
{
    private readonly IScreenshotCaptureService _capture = Substitute.For<IScreenshotCaptureService>();
    private readonly ScreenshotCommandService _service;

    public ScreenshotCommandServiceTests()
    {
        _service = new ScreenshotCommandService(_capture);
    }

    [Fact]
    public async Task Capture_ToFile_ForwardsThePathAndRegionAndMapsTheCaptureData()
    {
        _ = _capture.CaptureAsync(Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<ScreenRect?>(), Arg.Any<CancellationToken>())
            .Returns(ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(Path.GetFullPath("shot.png"), 2, 1, "png", "fake-frame", IsRegion: true, CopiedToClipboard: false)));

        var result = await _service.ExecuteAsync(new ScreenshotOptions("shot.png", RegionX: 1, RegionY: 2, RegionWidth: 2, RegionHeight: 1), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _capture.Received(1).CaptureAsync("shot.png", copyToClipboard: false, new ScreenRect(1, 2, 2, 1), Arg.Any<CancellationToken>());
        var data = Assert.IsType<ScreenshotData>(result.Data);
        Assert.Equal(Path.GetFullPath("shot.png"), data.OutputPath);
        Assert.Equal((2, 1, "png", "fake-frame"), (data.Width, data.Height, data.Format, data.ProviderName));
        Assert.True(data.IsRegion);
        Assert.False(data.CopiedToClipboard);
        Assert.Contains("saved to", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Capture_ToClipboard_ReportsTheClipboardCopy()
    {
        _ = _capture.CaptureAsync(Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<ScreenRect?>(), Arg.Any<CancellationToken>())
            .Returns(ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(OutputPath: null, 2, 1, "png", "fake-frame", IsRegion: false, CopiedToClipboard: true)));

        var result = await _service.ExecuteAsync(new ScreenshotOptions(Clipboard: true), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _capture.Received(1).CaptureAsync(outputPath: null, copyToClipboard: true, region: null, Arg.Any<CancellationToken>());
        var data = Assert.IsType<ScreenshotData>(result.Data);
        Assert.Null(data.OutputPath);
        Assert.True(data.CopiedToClipboard);
        Assert.Contains("copied to clipboard", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ScreenshotCaptureFailureKind.ProviderUnsupported, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenshotCaptureFailureKind.ClipboardUnsupported, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenshotCaptureFailureKind.ClipboardWriteFailed, ExecutionOutcomeCode.RuntimeError)]
    [InlineData(ScreenshotCaptureFailureKind.FileWriteFailed, ExecutionOutcomeCode.FileError)]
    public async Task Capture_WhenTheCaptureFails_MapsTheFailureKindAndKeepsTheDetails(ScreenshotCaptureFailureKind kind, ExecutionOutcomeCode expected)
    {
        _ = _capture.CaptureAsync(Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<ScreenRect?>(), Arg.Any<CancellationToken>())
            .Returns(ScreenshotCaptureResult.Fail(kind, "capture failed", ["detail"]));

        var result = await _service.ExecuteAsync(new ScreenshotOptions("shot.png"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expected, result.Outcome);
        Assert.Equal("capture failed", result.Message);
        Assert.Equal(["detail"], result.Errors);
    }

    [Fact]
    public async Task Capture_WhenNoDestinationIsGiven_ReturnsInvalidArgumentsBeforeCapturing()
    {
        var result = await _service.ExecuteAsync(new ScreenshotOptions(), CancellationToken.None);

        AssertRejectedBeforeCapture(result);
    }

    [Fact]
    public async Task Capture_WhenTheRegionIsPartial_ReturnsInvalidArgumentsBeforeCapturing()
    {
        var result = await _service.ExecuteAsync(new ScreenshotOptions("shot.png", RegionX: 1, RegionY: 2), CancellationToken.None);

        AssertRejectedBeforeCapture(result);
    }

    [Fact]
    public async Task Capture_WhenTheRegionSizeIsInvalid_ReturnsInvalidArgumentsBeforeCapturing()
    {
        var result = await _service.ExecuteAsync(new ScreenshotOptions("shot.png", RegionX: 1, RegionY: 2, RegionWidth: 0, RegionHeight: 1), CancellationToken.None);

        AssertRejectedBeforeCapture(result);
    }

    private void AssertRejectedBeforeCapture(OperationResult result)
    {
        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        Assert.Empty(_capture.ReceivedCalls());
    }
}
