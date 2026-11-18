
namespace CrossMacro.Commands.Tests;

public sealed class ScreenCommandServiceTests
{
    private readonly IScreenPixelReader _reader = Substitute.For<IScreenPixelReader>();
    private readonly IMousePositionProvider _mouse = Substitute.For<IMousePositionProvider>();
    private readonly IScreenImageAutomation _automation = Substitute.For<IScreenImageAutomation>();
    private readonly ScreenCommandService _service;

    public ScreenCommandServiceTests()
    {
        _ = _reader.IsSupported.Returns(returnThis: true);
        _ = _reader.ProviderName.Returns("fake-screen");
        _ = _reader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(ScreenReadResultFactory.Success(new ScreenPixelColor(0x12, 0x34, 0x56)));
        _ = _mouse.IsSupported.Returns(returnThis: true);
        _ = _mouse.GetAbsolutePositionAsync().Returns((100, 200));
        _ = _automation.IsSupported.Returns(returnThis: true);
        _ = _automation.ProviderName.Returns("fake-automation");
        _service = new ScreenCommandService(_reader, _mouse, _automation);
    }

    [Fact]
    public async Task Pixel_WhenRelative_ReadsAtTheOffsetFromTheMousePosition()
    {
        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.Pixel, 5, -10, Relative: true), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _reader.Received(1).GetPixelAsync(new ScreenPoint(105, 190), Arg.Is<ScreenReadOptions>(options => options.Timeout == ScreenReadOptions.DefaultTimeout));
        var data = Assert.IsType<ScreenPixelData>(result.Data);
        Assert.Equal("123456", data.Color);
        Assert.Equal((105, 190), (data.X, data.Y));
        Assert.True(data.Relative);
    }

    [Fact]
    public async Task Pixel_WhenAbsolute_DoesNotConsultTheMousePosition()
    {
        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.Pixel, 5, 6), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _mouse.DidNotReceive().GetAbsolutePositionAsync();
        _ = await _reader.Received(1).GetPixelAsync(new ScreenPoint(5, 6), Arg.Any<ScreenReadOptions>());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Pixel_WhenRelativeAndThePositionCannotBeResolved_ReturnsEnvironmentError(bool providerSupported, bool positionAvailable)
    {
        _ = _mouse.IsSupported.Returns(providerSupported);
        (int X, int Y)? position = positionAvailable ? (100, 200) : null;
        _ = _mouse.GetAbsolutePositionAsync().Returns(position);

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.Pixel, 1, 1, Relative: true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        _ = await _reader.DidNotReceive().GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task WaitColor_ForwardsTheTimeoutAndReportsTheMatch()
    {
        _ = _reader.WaitForPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenPixelColor>(), Arg.Any<ScreenReadOptions>())
            .Returns(ScreenReadResultFactory.Success(new ScreenPixelColor(0, 255, 0)));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.WaitColor, 1, 2, new ScreenPixelColor(0, 255, 0), TimeoutMs: 250), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _reader.Received(1).WaitForPixelAsync(
            new ScreenPoint(1, 2),
            new ScreenPixelColor(0, 255, 0),
            Arg.Is<ScreenReadOptions>(options => options.Timeout == TimeSpan.FromMilliseconds(250)));
        var data = Assert.IsType<ScreenWaitColorData>(result.Data);
        Assert.True(data.Matched);
        Assert.Equal(250, data.TimeoutMs);
    }

    [Theory]
    [InlineData(0, 0, 10, 20, 0, 0, 10, 20)]
    [InlineData(10, 20, 0, 0, 0, 0, 10, 20)]
    public async Task SearchColor_NormalizesTheCornersIntoARegion(int x, int y, int x2, int y2, int left, int top, int width, int height)
    {
        _ = _reader.SearchPixelAsync(Arg.Any<ScreenRect>(), Arg.Any<ScreenPixelColor>(), Arg.Any<int>(), Arg.Any<ScreenReadOptions>())
            .Returns(ScreenReadResultFactory.Success(new ScreenPixelSearchMatch(new ScreenPoint(3, 4), new ScreenPixelColor(255, 0, 0))));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchColor, x, y, new ScreenPixelColor(255, 0, 0), X2: x2, Y2: y2, Tolerance: 26, TimeoutMs: 275), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _reader.Received(1).SearchPixelAsync(
            new ScreenRect(left, top, width, height),
            new ScreenPixelColor(255, 0, 0),
            26,
            Arg.Is<ScreenReadOptions>(options => options.Timeout == TimeSpan.FromMilliseconds(275)));
        var data = Assert.IsType<ScreenSearchColorData>(result.Data);
        Assert.Equal((3, 4), (data.X, data.Y));
        Assert.Equal(26, data.Tolerance);
    }

    [Fact]
    public async Task InvalidActionOptions_ReturnInvalidArguments()
    {
        var missingWaitColor = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.WaitColor, 1, 2), CancellationToken.None);
        var missingSearchBounds = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchColor, 0, 0, new ScreenPixelColor(255, 0, 0)), CancellationToken.None);
        var zeroWidthSearch = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchColor, 0, 0, new ScreenPixelColor(255, 0, 0), X2: 0, Y2: 10), CancellationToken.None);

        AssertFailure(missingWaitColor, ExecutionOutcomeCode.InvalidArguments);
        AssertFailure(missingSearchBounds, ExecutionOutcomeCode.InvalidArguments);
        AssertFailure(zeroWidthSearch, ExecutionOutcomeCode.InvalidArguments);
    }

    [Fact]
    public async Task Pixel_WhenTheReaderIsUnsupported_ReturnsEnvironmentError()
    {
        _ = _reader.IsSupported.Returns(returnThis: false);

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.Pixel, 1, 2), CancellationToken.None);

        AssertFailure(result, ExecutionOutcomeCode.EnvironmentError);
    }

    [Fact]
    public async Task SearchImage_WhenFound_ForwardsTheRequestAndReturnsTheMatch()
    {
        _ = _automation.SearchAsync(Arg.Any<ScreenImageAutomationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ScreenImageAutomationResult.FoundAt(new ScreenPoint(7, 8), 0.95));

        var result = await _service.ExecuteAsync(new ScreenOptions(
            ScreenAction.SearchImage,
            ImagePath: "template.png",
            RegionX: 1,
            RegionY: 2,
            RegionWidth: 30,
            RegionHeight: 40,
            Similarity: 0.9,
            MatchMode: ScreenImageMatchMode.Best), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _automation.Received(1).SearchAsync(
            new ScreenImageAutomationRequest("template.png", new ScreenRect(1, 2, 30, 40), 0.9, ScreenImageMatchMode.Best, Timeout: null),
            Arg.Any<CancellationToken>());
        var data = Assert.IsType<ScreenSearchImageData>(result.Data);
        Assert.True(data.Found);
        Assert.Equal((7, 8, 0.95), (data.X, data.Y, data.Score));
        Assert.Equal(Path.GetFullPath("template.png"), data.ImagePath);
        Assert.Equal("best", data.MatchMode);
        Assert.Equal("fake-automation", data.ProviderName);
    }

    [Fact]
    public async Task SearchImage_WhenNothingMatches_SucceedsWithFoundFalse()
    {
        _ = _automation.SearchAsync(Arg.Any<ScreenImageAutomationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ScreenImageAutomationResult.NotFound("No image matching the template was found."));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchImage, ImagePath: "template.png"), CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<ScreenSearchImageData>(result.Data);
        Assert.False(data.Found);
        Assert.Null(data.X);
        Assert.Null(data.Y);
        Assert.Null(data.Score);
        Assert.Equal(["No image matching the template was found."], result.Warnings);
    }

    [Theory]
    [InlineData(ScreenReadErrorKind.Canceled, ExecutionOutcomeCode.Cancelled)]
    [InlineData(ScreenReadErrorKind.InvalidArguments, ExecutionOutcomeCode.InvalidArguments)]
    public async Task SearchImage_WhenTheAutomationFails_MapsTheFailureKindToTheExitCode(ScreenReadErrorKind kind, ExecutionOutcomeCode expected)
    {
        _ = _automation.SearchAsync(Arg.Any<ScreenImageAutomationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ScreenImageAutomationResult.Failure(kind, "automation failed"));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchImage, ImagePath: "template.png"), CancellationToken.None);

        AssertFailure(result, expected);
        Assert.Contains("automation failed", result.Errors, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task ImageActions_WhenSimilarityIsOutOfRange_ReturnInvalidArgumentsWithoutSearching(double similarity)
    {
        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.SearchImage, ImagePath: "template.png", Similarity: similarity), CancellationToken.None);

        AssertFailure(result, ExecutionOutcomeCode.InvalidArguments);
        Assert.Contains("Invalid options", result.Message, StringComparison.Ordinal);
        _ = await _automation.DidNotReceive().SearchAsync(Arg.Any<ScreenImageAutomationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImageActions_WhenTheRegionIsPartial_ReturnInvalidArguments()
    {
        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.WaitImage, ImagePath: "template.png", RegionX: 1, RegionY: 2), CancellationToken.None);

        AssertFailure(result, ExecutionOutcomeCode.InvalidArguments);
    }

    [Theory]
    [InlineData(ScreenAction.SearchImage)]
    [InlineData(ScreenAction.WaitImage)]
    [InlineData(ScreenAction.ImageClick)]
    public async Task ImageActions_WhenTheAutomationIsUnsupported_ReturnEnvironmentError(ScreenAction action)
    {
        _ = _automation.IsSupported.Returns(returnThis: false);

        var result = await _service.ExecuteAsync(new ScreenOptions(action, ImagePath: "template.png"), CancellationToken.None);

        AssertFailure(result, ExecutionOutcomeCode.EnvironmentError);
    }

    [Fact]
    public async Task WaitImage_ForwardsTheTimeoutAndReturnsTheMatch()
    {
        _ = _automation.WaitAsync(Arg.Any<ScreenImageAutomationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ScreenImageAutomationResult.FoundAt(new ScreenPoint(9, 10), 0.91));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.WaitImage, ImagePath: "template.png", TimeoutMs: 1500), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _automation.Received(1).WaitAsync(
            Arg.Is<ScreenImageAutomationRequest>(request => request.Timeout == TimeSpan.FromMilliseconds(1500)),
            Arg.Any<CancellationToken>());
        var data = Assert.IsType<ScreenSearchImageData>(result.Data);
        Assert.Equal((true, 9, 10, 0.91), (data.Found, data.X, data.Y, data.Score));
    }

    [Fact]
    public async Task WaitImage_WhenTheTimeoutExpires_SucceedsWithFoundFalse()
    {
        _ = _automation.WaitAsync(Arg.Any<ScreenImageAutomationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ScreenImageAutomationResult.NotFound("timed out"));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.WaitImage, ImagePath: "template.png", TimeoutMs: 0), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(Assert.IsType<ScreenSearchImageData>(result.Data).Found);
    }

    [Theory]
    [InlineData(MacroMouseButton.Left, MouseButtonCode.Left)]
    [InlineData(MacroMouseButton.Right, MouseButtonCode.Right)]
    [InlineData(MacroMouseButton.Middle, MouseButtonCode.Middle)]
    public async Task ImageClick_ClicksWithTheRequestedButtonAndReportsTheClickedPoint(MacroMouseButton button, int expectedButtonCode)
    {
        _ = _automation.ClickAsync(Arg.Any<ScreenImageAutomationRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ScreenImageAutomationResult.FoundAt(new ScreenPoint(21, 32), 0.97));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.ImageClick, ImagePath: "template.png", Button: button), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _automation.Received(1).ClickAsync(Arg.Any<ScreenImageAutomationRequest>(), expectedButtonCode, Arg.Any<CancellationToken>());
        var data = Assert.IsType<ScreenImageClickData>(result.Data);
        Assert.Equal((21, 32, 0.97, button.ToString()), (data.X, data.Y, data.Score, data.Button));
    }

    [Theory]
    [InlineData(ScreenReadErrorKind.Unsupported, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenReadErrorKind.InvalidArguments, ExecutionOutcomeCode.InvalidArguments)]
    [InlineData(ScreenReadErrorKind.Canceled, ExecutionOutcomeCode.Cancelled)]
    public async Task ImageClick_WhenTheAutomationFails_MapsTheFailureKindToTheExitCode(ScreenReadErrorKind kind, ExecutionOutcomeCode expected)
    {
        _ = _automation.ClickAsync(Arg.Any<ScreenImageAutomationRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ScreenImageAutomationResult.Failure(kind, "click failed"));

        var result = await _service.ExecuteAsync(new ScreenOptions(ScreenAction.ImageClick, ImagePath: "template.png"), CancellationToken.None);

        AssertFailure(result, expected);
        Assert.Contains("click failed", result.Errors, StringComparer.Ordinal);
    }

    private static void AssertFailure(OperationResult result, ExecutionOutcomeCode expected)
    {
        Assert.False(result.Success);
        Assert.Equal(expected, result.Outcome);
    }
}
