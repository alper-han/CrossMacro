namespace CrossMacro.Infrastructure.Tests.Services.ScreenReading;

public sealed class ScreenImageAutomationTests
{
    [Fact]
    public async Task SearchAsync_UsesOneCaptureWithTheInternalSafetyDeadline()
    {
        var reader = Substitute.For<IScreenPixelReader, IScreenImageSearchReader>();
        _ = reader.ProviderName.Returns("test-screen");
        _ = reader.IsSupported.Returns(returnThis: true);
        _ = ((IScreenImageSearchReader)reader).SearchImageAsync(
                Arg.Any<ScreenRect?>(),
                Arg.Any<ScreenFrame>(),
                Arg.Any<ScreenImageMatchOptions>(),
                Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResultFactory.Success(new ScreenImageMatch(new ScreenPoint(4, 5), 1.0))));

        var codec = Substitute.For<IImageAssetCodec>();
        using var template = new ScreenFrame(new ScreenRect(0, 0, 1, 1), 3, ScreenPixelFormat.Rgb24, new byte[3]);
        _ = codec.DecodeFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(template);
        var automation = new ScreenImageAutomation(
            reader,
            codec,
            mousePositionProvider: null,
            inputSimulatorFactory: null,
            simulatorPool: null,
            movementResolver: Substitute.For<IImageClickMovementResolver>());

        var result = await automation.SearchAsync(new ScreenImageAutomationRequest("target.png"), CancellationToken.None);

        _ = result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        await ((IScreenImageSearchReader)reader).Received(1).SearchImageAsync(
            Arg.Any<ScreenRect?>(),
            Arg.Any<ScreenFrame>(),
            Arg.Any<ScreenImageMatchOptions>(),
            Arg.Is<ScreenReadOptions>(options =>
                options.Timeout == ScreenReadOptions.DefaultTimeout
                && !options.PollUntilMatch));
    }

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
        await ((IScreenImageSearchReader)reader).Received(2).SearchImageAsync(
            Arg.Any<ScreenRect?>(),
            Arg.Any<ScreenFrame>(),
            Arg.Is<ScreenImageMatchOptions>(options =>
                options.SelectionMode == ScreenImageMatchSelectionMode.Automatic
                && options.MinimumSimilarity == 0.95),
            Arg.Any<ScreenReadOptions>());
        await simulator.Received(1).InitializeAsync(5120, 1440, Arg.Any<CancellationToken>());
        simulator.Received(1).MoveAbsolute(1822, 153);
        simulator.Received(1).MouseButton(MouseButtonCode.Left, pressed: true);
        simulator.Received(1).MouseButton(MouseButtonCode.Left, pressed: false);
    }

    [Fact]
    public async Task ClickAsync_WithPooledSimulator_DoesNotReinitializeTheReadyLease()
    {
        var reader = Substitute.For<IScreenPixelReader, IScreenImageSearchReader>();
        _ = reader.ProviderName.Returns("test-screen");
        _ = reader.IsSupported.Returns(returnThis: true);
        _ = ((IScreenImageSearchReader)reader).SearchImageAsync(
                Arg.Any<ScreenRect?>(),
                Arg.Any<ScreenFrame>(),
                Arg.Any<ScreenImageMatchOptions>(),
                Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResultFactory.Success(new ScreenImageMatch(
                Point: new ScreenPoint(40, 50),
                Score: 1.0,
                MatchedWidth: 4,
                MatchedHeight: 6))));

        var codec = Substitute.For<IImageAssetCodec>();
        using var template = new ScreenFrame(new ScreenRect(0, 0, 1, 1), 3, ScreenPixelFormat.Rgb24, new byte[3]);
        _ = codec.DecodeFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(template);

        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities, IInputSimulatorAbsoluteBounds>();
        _ = simulator.IsSupported.Returns(returnThis: true);
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        _ = ((IInputSimulatorAbsoluteBounds)simulator).UsesZeroBasedScreenBounds.Returns(returnThis: true);
        var pool = Substitute.For<IInputSimulatorPool>();
        _ = pool.AcquireAsync(1920, 1080, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IInputSimulator>(simulator));

        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetDesktopBoundsAsync().Returns(Task.FromResult<ScreenRect?>(new ScreenRect(0, 0, 1920, 1080)));
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(new(42, 53)));

        var resolver = Substitute.For<IImageClickMovementResolver>();
        _ = resolver.ResolveAsync(simulator, new ScreenPoint(42, 53), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ImageClickMovementResolution.Absolute(new ScreenPoint(42, 53))));

        var automation = new ScreenImageAutomation(
            screenPixelReader: reader,
            imageAssetCodec: codec,
            mousePositionProvider: positionProvider,
            inputSimulatorFactory: null,
            simulatorPool: pool,
            movementResolver: resolver);

        var result = await automation.ClickAsync(new ScreenImageAutomationRequest("button.png"), MouseButtonCode.Left, CancellationToken.None);

        _ = result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        await pool.Received(1).AcquireAsync(1920, 1080, Arg.Any<CancellationToken>());
        await simulator.DidNotReceive().InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        pool.Received(1).Release(simulator, 1920, 1080);
    }

    [Fact]
    public async Task ClickAsync_WhenPolling_RequiresTwoConsecutiveConsistentMatchesBeforeClicking()
    {
        var reader = Substitute.For<IScreenPixelReader, IScreenImageSearchReader>();
        _ = reader.ProviderName.Returns("test-screen");
        _ = reader.IsSupported.Returns(true);
        var outcomes = new Queue<ScreenReadResult<ScreenImageMatch>>(
        [
            ScreenReadResultFactory.Success(new ScreenImageMatch(new ScreenPoint(4, 5), 1.0, 4, 6)),
            ScreenReadResultFactory.Failure<ScreenImageMatch>(ScreenReadErrorKind.CaptureTimeout, "not present"),
            ScreenReadResultFactory.Success(new ScreenImageMatch(new ScreenPoint(40, 50), 1.0, 4, 6)),
            ScreenReadResultFactory.Success(new ScreenImageMatch(new ScreenPoint(41, 50), 1.0, 4, 6)),
        ]);
        _ = ((IScreenImageSearchReader)reader).SearchImageAsync(
                Arg.Any<ScreenRect?>(),
                Arg.Any<ScreenFrame>(),
                Arg.Any<ScreenImageMatchOptions>(),
                Arg.Any<ScreenReadOptions>())
            .Returns(_ => Task.FromResult(outcomes.Dequeue()));

        var codec = Substitute.For<IImageAssetCodec>();
        using var template = new ScreenFrame(
            new ScreenRect(0, 0, 1, 1),
            stride: 3,
            ScreenPixelFormat.Rgb24,
            new byte[3]);
        _ = codec.DecodeFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(template);

        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities, IInputSimulatorAbsoluteBounds>();
        _ = simulator.ProviderName.Returns("test-input");
        _ = simulator.IsSupported.Returns(true);
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(true);
        _ = ((IInputSimulatorAbsoluteBounds)simulator).UsesZeroBasedScreenBounds.Returns(true);

        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.ProviderName.Returns("test-position");
        _ = positionProvider.IsSupported.Returns(true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(true);
        var bounds = new ScreenRect(0, 0, 1920, 1080);
        _ = positionProvider.GetDesktopBoundsAsync().Returns(Task.FromResult<ScreenRect?>(bounds));
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((43, 53)));

        var resolver = Substitute.For<IImageClickMovementResolver>();
        _ = resolver.ResolveAsync(simulator, new ScreenPoint(43, 53), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ImageClickMovementResolution.Absolute(new ScreenPoint(43, 53))));

        var automation = new ScreenImageAutomation(
            reader,
            codec,
            positionProvider,
            () => simulator,
            simulatorPool: null,
            resolver);

        var result = await automation.ClickAsync(
            new ScreenImageAutomationRequest(
                "button.png",
                Timeout: TimeSpan.FromSeconds(1)),
            MouseButtonCode.Left,
            CancellationToken.None);

        _ = result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        _ = result.Point.Should().Be(new ScreenPoint(43, 53));
        _ = outcomes.Should().BeEmpty();
        await ((IScreenImageSearchReader)reader).Received(4).SearchImageAsync(
            Arg.Any<ScreenRect?>(),
            Arg.Any<ScreenFrame>(),
            Arg.Any<ScreenImageMatchOptions>(),
            Arg.Any<ScreenReadOptions>());
        simulator.Received(1).MouseButton(MouseButtonCode.Left, pressed: true);
        simulator.Received(1).MouseButton(MouseButtonCode.Left, pressed: false);
    }

    [Fact]
    public async Task ClickAsync_WhenAbsoluteMoveDoesNotSettle_ReleasesLeaseWithoutClicking()
    {
        var reader = Substitute.For<IScreenPixelReader, IScreenImageSearchReader>();
        _ = reader.ProviderName.Returns("test-screen");
        _ = reader.IsSupported.Returns(returnThis: true);
        _ = ((IScreenImageSearchReader)reader).SearchImageAsync(
                Arg.Any<ScreenRect?>(),
                Arg.Any<ScreenFrame>(),
                Arg.Any<ScreenImageMatchOptions>(),
                Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResultFactory.Success(new ScreenImageMatch(
                Point: new ScreenPoint(40, 50),
                Score: 1.0,
                MatchedWidth: 4,
                MatchedHeight: 6))));

        var codec = Substitute.For<IImageAssetCodec>();
        using var template = new ScreenFrame(new ScreenRect(0, 0, 1, 1), 3, ScreenPixelFormat.Rgb24, new byte[3]);
        _ = codec.DecodeFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(template);

        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities, IInputSimulatorAbsoluteBounds>();
        _ = simulator.IsSupported.Returns(returnThis: true);
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        _ = ((IInputSimulatorAbsoluteBounds)simulator).UsesZeroBasedScreenBounds.Returns(returnThis: true);
        var pool = Substitute.For<IInputSimulatorPool>();
        _ = pool.AcquireAsync(1920, 1080, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IInputSimulator>(simulator));

        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetDesktopBoundsAsync().Returns(Task.FromResult<ScreenRect?>(new ScreenRect(0, 0, 1920, 1080)));
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(new(900, 700)));

        var resolver = Substitute.For<IImageClickMovementResolver>();
        _ = resolver.ResolveAsync(simulator, new ScreenPoint(42, 53), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ImageClickMovementResolution.Absolute(new ScreenPoint(42, 53))));
        var automation = new ScreenImageAutomation(
            screenPixelReader: reader,
            imageAssetCodec: codec,
            mousePositionProvider: positionProvider,
            inputSimulatorFactory: null,
            simulatorPool: pool,
            movementResolver: resolver);

        var result = await automation.ClickAsync(new ScreenImageAutomationRequest("button.png"), MouseButtonCode.Left, CancellationToken.None);

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Absolute cursor move did not settle at (42,53).");
        simulator.Received(1).MoveAbsolute(42, 53);
        simulator.DidNotReceive().MouseButton(Arg.Any<int>(), Arg.Any<bool>());
        pool.Received(1).Release(simulator, 1920, 1080);
    }
}
