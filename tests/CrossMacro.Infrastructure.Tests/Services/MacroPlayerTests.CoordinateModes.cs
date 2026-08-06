// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class MacroPlayerTests
{

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClick_UsesLiveCursorWithoutSyntheticMove()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();

        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, UseCurrentPosition = true },
            },
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        _ = simulator.InitializedWidth.Should().Be(0);
        _ = simulator.InitializedHeight.Should().Be(0);
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
        _ = simulator.ButtonTransitions.Should().HaveCount(2);
        _ = simulator.Operations[0].Should().Be("btn:down");
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClickLoops_DoesNotInjectSyntheticMovement()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();

        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, UseCurrentPosition = true },
            },
        };

        var options = new PlaybackOptions
        {
            Loop = true,
            RepeatCount = 2,
            RepeatDelayMs = 0,
        };

        // Act
        await player.PlayAsync(macro, options);

        // Assert
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
        _ = simulator.ButtonTransitions.Should().HaveCount(4);
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionEventHasStoredCoordinates_DoesNotMoveToStoredPosition()
    {
        // Arrange
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 999,
                    Y = 777,
                    UseCurrentPosition = true,
                },
            },
        };

        // Act
        await player.PlayAsync(macro);

        // Assert
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenMacroHasMixedCoordinateModes_ExecutesEachEventWithEffectiveMode()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().ContainInOrder("abs:100,200", "rel:10,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenMacroCombinesCurrentAbsoluteAndRelativeEvents_UsesPerEventMovementSemantics()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Right,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().Equal(
            "btn:down",
            "btn:up",
            "abs:100,200",
            "rel:10,-5",
            "btn:down",
            "btn:up");
        _ = simulator.AbsoluteMoves.Should().Equal((100, 200));
        _ = simulator.ButtonTransitions.Should().HaveCount(4);
    }

    [Fact]
    public async Task PlayAsync_WhenLegacyAbsoluteMacroHasExplicitRelativeEvent_UsesRelativeEventMode()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(0);
        _ = simulator.InitializedHeight.Should().Be(0);
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
        _ = simulator.Operations.Should().Contain("rel:10,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteMacroUsesRelativeOnlySimulator_ThrowsBeforeInjectingInput()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        var act = async () => await player.PlayAsync(macro);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support absolute coordinate playback*");
        _ = simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteMacroUsesResolutionOnlyProvider_CachesResolutionAndCreatesAbsoluteDevice()
    {
        var resolutionOnlyProvider = Substitute.For<IMousePositionProvider>();
        _ = resolutionOnlyProvider.IsSupported.Returns(returnThis: false);
        _ = resolutionOnlyProvider.ProviderName.Returns("Niri IPC (Resolution Only)");
        _ = resolutionOnlyProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));

        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            new PlaybackValidator(_keyCodeMapper, resolutionOnlyProvider),
            CreateDependencies(resolutionOnlyProvider, () => simulator, timingService: null, (_, _) => Task.CompletedTask, playbackElapsedMillisecondsFactory: null, _keyCodeMapper));

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = await resolutionOnlyProvider.Received(1).GetScreenResolutionAsync();
        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().Contain("abs:100,200");
    }

    [Fact]
    public async Task PlayAsync_WhenDesktopTopologyChangesBetweenRuns_RefreshesAbsoluteDeviceBounds()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.ProviderName.Returns("Mutable desktop");
        _ = positionProvider.GetAbsolutePositionAsync()
            .Returns(
                Task.FromResult<(int X, int Y)?>((0, 0)),
                Task.FromResult<(int X, int Y)?>((0, 0)),
                Task.FromResult<(int X, int Y)?>((100, 200)),
                Task.FromResult<(int X, int Y)?>((100, 200)));
        var currentBounds = new ScreenRect(0, 0, 1920, 1080);
        _ = positionProvider.GetDesktopBoundsAsync()
            .Returns(_ => Task.FromResult<ScreenRect?>(currentBounds));

        var firstSimulator = new TrackingInputSimulator();
        var secondSimulator = new TrackingInputSimulator();
        var simulators = new Queue<TrackingInputSimulator>([firstSimulator, secondSimulator]);
        var player = new MacroPlayer(
            new PlaybackValidator(_keyCodeMapper, positionProvider),
            CreateDependencies(
                positionProvider,
                () => simulators.Dequeue(),
                timingService: null,
                (_, _) => Task.CompletedTask,
                playbackElapsedMillisecondsFactory: null,
                _keyCodeMapper));
        var macro = new MacroSequence
        {
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        await player.PlayAsync(macro);
        currentBounds = new ScreenRect(-2560, -400, 6400, 2560);
        await player.PlayAsync(macro);

        Assert.Equal((1920, 1080), (firstSimulator.InitializedWidth, firstSimulator.InitializedHeight));
        Assert.Equal((6400, 2560), (secondSimulator.InitializedWidth, secondSimulator.InitializedHeight));
        _ = await positionProvider.Received(2).GetDesktopBoundsAsync();
    }

    [Fact]
    public async Task PlayAsync_WhenRelativeMacroUsesResolutionOnlyProvider_PreparesCornerResetAndPlaysRelativeOnly()
    {
        var resolutionOnlyProvider = Substitute.For<IMousePositionProvider>();
        _ = resolutionOnlyProvider.IsSupported.Returns(returnThis: false);
        _ = resolutionOnlyProvider.ProviderName.Returns("COSMIC RandR (Resolution Only)");
        _ = resolutionOnlyProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((2560, 1440)));

        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = new MacroPlayer(
            new PlaybackValidator(_keyCodeMapper, resolutionOnlyProvider),
            CreateDependencies(resolutionOnlyProvider, () => simulator, timingService: null, (_, _) => Task.CompletedTask, playbackElapsedMillisecondsFactory: null, _keyCodeMapper));

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = await resolutionOnlyProvider.Received(1).GetScreenResolutionAsync();
        _ = simulator.InitializedWidth.Should().Be(2560);
        _ = simulator.InitializedHeight.Should().Be(1440);
        _ = simulator.AbsoluteMoves.Should().BeEmpty();
        _ = simulator.Operations.Should().Contain("rel:-20000,0");
        _ = simulator.Operations.Should().Contain("rel:0,-20000");
        _ = simulator.Operations.Should().Contain("rel:3,3");
    }

    [Fact]
    public async Task PlayAsync_WhenMixedMacroUsesRelativeOnlySimulator_ThrowsBeforeInjectingInput()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        var act = async () => await player.PlayAsync(macro);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support absolute coordinate playback*");
        _ = simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenRelativeMacroUsesRelativeOnlySimulator_PlaysNormally()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Contain("rel:3,3");
    }

    [Fact]
    public async Task PlayAsync_WhenLogicalRelativeEventHasKnownPosition_UsesAbsoluteLogicalTarget()
    {
        _ = _positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = _positionProvider.GetAbsolutePositionAsync()
            .Returns(Task.FromResult<(int X, int Y)?>((100, 200)));
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().Contain("abs:103,195");
        _ = simulator.Operations.Should().NotContain("rel:3,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenLogicalRelativeEventUsesRelativeOnlySimulator_ThrowsBeforeInjectingInput()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        var act = async () => await player.PlayAsync(macro);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not support absolute coordinate playback*");
        _ = simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenLogicalRelativePositionCannotBeAnchored_ThrowsWithoutRawFallback()
    {
        var resolutionOnlyProvider = Substitute.For<IMousePositionProvider>();
        _ = resolutionOnlyProvider.IsSupported.Returns(returnThis: false);
        _ = resolutionOnlyProvider.SupportsAbsolutePosition.Returns(returnThis: false);
        _ = resolutionOnlyProvider.ProviderName.Returns("Resolution Only");
        _ = resolutionOnlyProvider.GetScreenResolutionAsync()
            .Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        var simulator = new TrackingInputSimulator();
        var player = new MacroPlayer(
            new PlaybackValidator(_keyCodeMapper, resolutionOnlyProvider),
            CreateDependencies(
                resolutionOnlyProvider,
                () => simulator,
                timingService: null,
                (_, _) => Task.CompletedTask,
                playbackElapsedMillisecondsFactory: null,
                _keyCodeMapper));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        var act = async () => await player.PlayAsync(macro);

        _ = await act.Should().ThrowAsync<LogicalRelativePositionUnavailableException>();
        _ = simulator.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteMoveAnchorsLogicalRelativeMove_PreservesMixedPath()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Equal("abs:100,200", "abs:103,195");
    }

    [Fact]
    public async Task PlayAsync_WhenRawMovePrecedesLogicalRelativeMove_AnchorsToObservedCursorPosition()
    {
        _ = _positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = _positionProvider.GetAbsolutePositionAsync().Returns(
            Task.FromResult<(int X, int Y)?>((100, 100)),
            Task.FromResult<(int X, int Y)?>((100, 100)),
            Task.FromResult<(int X, int Y)?>((120, 110)));
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 5,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.RawDevice,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = -2,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Equal("abs:100,100", "rel:5,5", "abs:123,108");
        _ = await _positionProvider.Received(3).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeScriptUsesLegacyRelativeMove_DoesNotRequireAbsoluteDevice()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            ScriptSteps = { "set dx 3", "move rel $dx -5" },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(0);
        _ = simulator.InitializedHeight.Should().Be(0);
        _ = simulator.Operations.Should().ContainSingle().Which.Should().Be("rel:3,-5");
    }

    [Fact]
    public async Task PlayAsync_WhenRuntimeScriptUsesLogicalRelativeMove_UsesAbsoluteLogicalTarget()
    {
        _ = _positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = _positionProvider.GetAbsolutePositionAsync()
            .Returns(Task.FromResult<(int X, int Y)?>((100, 200)));
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);
        var macro = new MacroSequence
        {
            ScriptSteps = { "set dx 3", "move rel-logical $dx -5" },
        };

        await player.PlayAsync(macro);

        _ = simulator.InitializedWidth.Should().Be(1920);
        _ = simulator.InitializedHeight.Should().Be(1080);
        _ = simulator.Operations.Should().Equal("abs:100,200", "abs:103,195");
    }

    [Fact]
    public async Task PlayAsync_WhenCurrentPositionClickUsesRelativeOnlySimulator_PlaysNormally()
    {
        var simulator = new TrackingInputSimulator(forceRelativeOnly: true);
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                    X = 100,
                    Y = 200,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Equal("btn:down", "btn:up");
    }

    [Fact]
    public async Task PlayAsync_WhenAbsoluteThenRelativeMacroPlays_ExecutesExactMovementSequence()
    {
        var simulator = new TrackingInputSimulator();
        var player = CreatePlayer(inputSimulatorFactory: () => simulator);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 1000,
                    Y = 1000,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 3,
                    Y = 3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await player.PlayAsync(macro);

        _ = simulator.Operations.Should().Equal("abs:1000,1000", "rel:3,3");
    }
}
