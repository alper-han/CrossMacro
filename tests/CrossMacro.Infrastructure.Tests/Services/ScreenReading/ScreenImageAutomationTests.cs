namespace CrossMacro.Infrastructure.Tests.Services.ScreenReading;

public sealed class ScreenImageAutomationTests
{
    [Fact]
    public async Task ClickAsync_MapsGlobalTargetToZeroBasedDeviceAndUsesMatchedDimensions()
    {
        var reader = Substitute.For<IScreenPixelReader, IScreenImageSearchReader>();
        _ = reader.ProviderName.Returns("test-screen");
        _ = reader.IsSupported.Returns(true);

        var match = new ScreenImageMatch(
            new ScreenPoint(-100, -50),
            Score: 0.95,
            MatchedWidth: 4,
            MatchedHeight: 6);
        _ = ((IScreenImageSearchReader)reader).SearchImageAsync(
                Arg.Any<ScreenRect?>(),
                Arg.Any<ScreenFrame>(),
                Arg.Any<ScreenImageMatchOptions>(),
                Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResultFactory.Success(match)));

        var codec = Substitute.For<IImageAssetCodec>();
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 1, 1),
            stride: 3,
            ScreenPixelFormat.Rgb24,
            new byte[3]);
        _ = codec.DecodeFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(template);

        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities, IInputSimulatorAbsoluteBounds>();
        _ = simulator.ProviderName.Returns("test-input");
        _ = simulator.IsSupported.Returns(true);
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(true);
        _ = ((IInputSimulatorAbsoluteBounds)simulator).UsesZeroBasedScreenBounds.Returns(true);

        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.ProviderName.Returns("test-position");
        _ = positionProvider.IsSupported.Returns(true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(true);
        var bounds = new ScreenRect(-1920, -200, 5120, 1440);
        _ = positionProvider.GetDesktopBoundsAsync().Returns(Task.FromResult<ScreenRect?>(bounds));
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((-98, -47)));

        var resolver = Substitute.For<IImageClickMovementResolver>();
        _ = resolver.ResolveAsync(simulator, new ScreenPoint(-98, -47), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ImageClickMovementResolution.Absolute(new ScreenPoint(-98, -47))));

        var automation = new ScreenImageAutomation(
            reader,
            codec,
            positionProvider,
            () => simulator,
            simulatorPool: null,
            resolver);

        var result = await automation.ClickAsync(
            new ScreenImageAutomationRequest("button.png"),
            MouseButtonCode.Left,
            CancellationToken.None);

        _ = result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        _ = result.Point.Should().Be(new ScreenPoint(-98, -47));
        await simulator.Received(1).InitializeAsync(5120, 1440, Arg.Any<CancellationToken>());
        simulator.Received(1).MoveAbsolute(1822, 153);
        simulator.Received(1).MouseButton(MouseButtonCode.Left, pressed: true);
        simulator.Received(1).MouseButton(MouseButtonCode.Left, pressed: false);
    }
}
