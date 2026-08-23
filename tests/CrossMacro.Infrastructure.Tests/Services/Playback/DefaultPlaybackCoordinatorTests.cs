
namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class DefaultPlaybackCoordinatorTests
{
    [Fact]
    public async Task InitializeAsync_AbsoluteMode_DoesNotPreMoveFirstEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove, X = 120, Y = 90 },
            },
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        _ = coordinator.CurrentX.Should().Be(0);
        _ = coordinator.CurrentY.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_AbsoluteMode_WithPositionProvider_TracksCurrentPositionWithoutPreMove()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((50, 40)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove, X = 120, Y = 90 },
            },
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _ = coordinator.CurrentX.Should().Be(50);
        _ = coordinator.CurrentY.Should().Be(40);
    }

    [Fact]
    public async Task TrySynchronizePositionAsync_WhenPositionWasInvalidated_RefreshesOnce()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((75, 60)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);

        coordinator.InvalidatePosition();
        var synchronized = await coordinator.TrySynchronizePositionAsync(CancellationToken.None);
        var alreadySynchronized = await coordinator.TrySynchronizePositionAsync(CancellationToken.None);

        _ = synchronized.Should().BeTrue();
        _ = alreadySynchronized.Should().BeTrue();
        _ = coordinator.CurrentX.Should().Be(75);
        _ = coordinator.CurrentY.Should().Be(60);
        _ = coordinator.HasKnownPosition.Should().BeTrue();
        _ = await positionProvider.Received(1).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task TrySynchronizePositionAsync_AfterRawMovement_WaitsForProviderToObserveMovement()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(
            Task.FromResult<(int X, int Y)?>((50, 40)),
            Task.FromResult<(int X, int Y)?>((75, 60)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.UpdatePosition(50, 40);

        coordinator.InvalidatePosition(movementMayBePending: true);
        var synchronized = await coordinator.TrySynchronizePositionAsync(CancellationToken.None);

        _ = synchronized.Should().BeTrue();
        _ = coordinator.CurrentX.Should().Be(75);
        _ = coordinator.CurrentY.Should().Be(60);
        _ = await positionProvider.Received(2).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task TrySynchronizePositionAsync_WhenProviderCannotQueryPosition_LeavesPositionUnknown()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: false);
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);

        var synchronized = await coordinator.TrySynchronizePositionAsync(CancellationToken.None);

        _ = synchronized.Should().BeFalse();
        _ = coordinator.HasKnownPosition.Should().BeFalse();
        _ = await positionProvider.DidNotReceive().GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task TrySynchronizePositionAsync_AfterRawMovementWithoutPositionCapability_DoesNotRetry()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: false);
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.InvalidatePosition(movementMayBePending: true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var synchronized = await coordinator.TrySynchronizePositionAsync(cancellation.Token);

        _ = synchronized.Should().BeFalse();
        _ = coordinator.HasKnownPosition.Should().BeFalse();
        _ = await positionProvider.DidNotReceive().GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task TrySynchronizePositionAsync_AfterRawMovementWaitsForTransientPositionAvailability()
    {
        var positionProvider = Substitute.For<IMousePositionProvider, IMousePositionAvailability>();
        var availability = (IMousePositionAvailability)positionProvider;
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        var availabilityChecks = 0;
        _ = availability.IsPositionAvailable.Returns(_ => ++availabilityChecks > 1);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((75, 60)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.UpdatePosition(50, 40);
        coordinator.InvalidatePosition(movementMayBePending: true);

        var synchronized = await coordinator.TrySynchronizePositionAsync(CancellationToken.None);

        _ = synchronized.Should().BeTrue();
        _ = coordinator.CurrentX.Should().Be(75);
        _ = coordinator.CurrentY.Should().Be(60);
        _ = availabilityChecks.Should().BeGreaterThan(1);
        _ = await positionProvider.Received(1).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task WaitForPositionAsync_WhenCallerIsCanceled_PropagatesCancellation()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(true);
        _ = positionProvider.SupportsAbsolutePosition.Returns(true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((0, 0)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.WaitForPositionAsync(100, 100, cancellation.Token));
    }

    [Fact]
    public async Task WaitForPositionAsync_WhenObservationIsWithinTolerance_PreservesCommandPosition()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((99, 199)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.UpdatePosition(100, 200);

        var settled = await coordinator.WaitForPositionAsync(100, 200, CancellationToken.None);

        _ = settled.Should().BeTrue();
        _ = coordinator.CurrentX.Should().Be(100);
        _ = coordinator.CurrentY.Should().Be(200);
    }

    [Fact]
    public async Task WaitForPositionAsync_WhenCompositorPublishesAfterSeveralFrames_WaitsBeyondLegacyShortBudget()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);

        var observations = new Queue<(int X, int Y)?>([
            (0, 0),
            (0, 0),
            (0, 0),
            (0, 0),
            (0, 0),
            (0, 0),
            (0, 0),
            (0, 0),
            (100, 200),
        ]);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(_ =>
            Task.FromResult(observations.Count > 0
                ? observations.Dequeue()
                : ((int X, int Y)?)(100, 200)));

        var coordinator = new DefaultPlaybackCoordinator(positionProvider);

        var settled = await coordinator.WaitForPositionAsync(100, 200, CancellationToken.None);

        _ = settled.Should().BeTrue();
        _ = await positionProvider.Received(9).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task WaitForPositionAsync_WhenPositionProviderDoesNotComplete_StopsAtSettleBudget()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(new TaskCompletionSource<(int X, int Y)?>(
            TaskCreationOptions.RunContinuationsAsynchronously).Task);
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);

        var settled = await coordinator.WaitForPositionAsync(100, 200, CancellationToken.None);

        _ = settled.Should().BeFalse();
    }

    [Fact]
    public async Task PrepareIterationAsync_AbsoluteMode_DoesNotPreMoveFirstEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.UpdatePosition(50, 40);

        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove, X = 200, Y = 150 },
            },
        };

        // Act
        await coordinator.PrepareIterationAsync(1, macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _ = coordinator.CurrentX.Should().Be(50);
        _ = coordinator.CurrentY.Should().Be(40);
    }

    [Fact]
    public async Task InitializeAsync_RelativeCurrentPositionMacro_DoesNotCornerResetEvenWhenSkipIsFalse()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true,
                },
            },
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task InitializeAsync_RelativeMacroWithLaterCurrentPositionEvent_StillPerformsCornerReset()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                },
            },
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.Received(2).MoveRelative(-20000, 0);
        simulator.Received(2).MoveRelative(0, -20000);
    }

    [Fact]
    public async Task InitializeAsync_RelativeCornerReset_RefreshesActualNegativeDesktopPosition()
    {
        var simulator = Substitute.For<
            IInputSimulator,
            IInputSimulatorCapabilities,
            IInputSimulatorAbsoluteBounds>();
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        _ = ((IInputSimulatorAbsoluteBounds)simulator).UsesZeroBasedScreenBounds.Returns(returnThis: true);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(
            Task.FromResult<(int X, int Y)?>((50, 40)),
            Task.FromResult<(int X, int Y)?>((-1920, -200)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.ConfigureDesktopBounds(new ScreenRect(-1920, -200, 4480, 1640));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 4480, 1640, CancellationToken.None);

        Received.InOrder(() =>
        {
            simulator.MoveAbsolute(1, 1);
            simulator.MoveAbsolute(0, 0);
        });
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _ = coordinator.CurrentX.Should().Be(-1920);
        _ = coordinator.CurrentY.Should().Be(-200);
        _ = coordinator.HasKnownPosition.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_RelativeCornerReset_AcceptsCompositorEdgeClamp()
    {
        var simulator = Substitute.For<
            IInputSimulator,
            IInputSimulatorCapabilities,
            IInputSimulatorAbsoluteBounds>();
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        _ = ((IInputSimulatorAbsoluteBounds)simulator).UsesZeroBasedScreenBounds.Returns(returnThis: true);
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(
            Task.FromResult<(int X, int Y)?>((50, 40)),
            Task.FromResult<(int X, int Y)?>((-1919, -199)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.ConfigureDesktopBounds(new ScreenRect(-1920, -200, 4480, 1640));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 4480, 1640, CancellationToken.None);

        _ = coordinator.CurrentX.Should().Be(-1919);
        _ = coordinator.CurrentY.Should().Be(-199);
        _ = await positionProvider.Received(2).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task InitializeAsync_RelativeCornerReset_WaitsForDelayedProviderUpdate()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(
            Task.FromResult<(int X, int Y)?>((640, 480)),
            Task.FromResult<(int X, int Y)?>((640, 480)),
            Task.FromResult<(int X, int Y)?>((-1920, -200)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.ConfigureDesktopBounds(new ScreenRect(-1920, -200, 4480, 1640));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 4480, 1640, CancellationToken.None);

        _ = coordinator.CurrentX.Should().Be(-1920);
        _ = coordinator.CurrentY.Should().Be(-200);
    }

    [Fact]
    public async Task InitializeAsync_RelativeCornerReset_UsesDesktopOriginWhenProviderRemainsStale()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((640, 480)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.ConfigureDesktopBounds(new ScreenRect(-1920, -200, 4480, 1640));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 4480, 1640, CancellationToken.None);

        _ = coordinator.CurrentX.Should().Be(-1920);
        _ = coordinator.CurrentY.Should().Be(-200);
        _ = coordinator.HasKnownPosition.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_RelativeCornerReset_DoesNotAcceptUnvalidatedFirstPosition()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(
            Task.FromResult<(int X, int Y)?>(null),
            Task.FromResult<(int X, int Y)?>((640, 480)),
            Task.FromResult<(int X, int Y)?>((-1920, -200)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.ConfigureDesktopBounds(new ScreenRect(-1920, -200, 4480, 1640));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 4480, 1640, CancellationToken.None);

        _ = coordinator.CurrentX.Should().Be(-1920);
        _ = coordinator.CurrentY.Should().Be(-200);
    }

    [Fact]
    public async Task InitializeAsync_RelativeCornerReset_DoesNotTreatAnotherMonitorTopEdgeAsDesktopCorner()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(
            Task.FromResult<(int X, int Y)?>((1200, 600)),
            Task.FromResult<(int X, int Y)?>((0, -200)),
            Task.FromResult<(int X, int Y)?>((-1920, -200)));
        var coordinator = new DefaultPlaybackCoordinator(positionProvider);
        coordinator.ConfigureDesktopBounds(new ScreenRect(-1920, -200, 4480, 1640));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 4480, 1640, CancellationToken.None);

        _ = coordinator.CurrentX.Should().Be(-1920);
        _ = coordinator.CurrentY.Should().Be(-200);
        _ = await positionProvider.Received(3).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task InitializeAsync_RelativeCornerReset_UsesDesktopOriginWhenPositionCannotBeQueried()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.ConfigureDesktopBounds(new ScreenRect(-1920, -200, 4480, 1640));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 4480, 1640, CancellationToken.None);

        _ = coordinator.CurrentX.Should().Be(-1920);
        _ = coordinator.CurrentY.Should().Be(-200);
        _ = coordinator.HasKnownPosition.Should().BeTrue();
    }

    [Fact]
    public async Task PrepareIterationAsync_RelativeCurrentPositionMacro_DoesNotCornerResetEvenWhenSkipIsFalse()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true,
                },
            },
        };

        // Act
        await coordinator.PrepareIterationAsync(1, macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task InitializeAsync_AbsoluteCurrentPositionClick_DoesNotMoveToStoredCoordinates()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 800,
                    Y = 600,
                    UseCurrentPosition = true,
                },
            },
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task InitializeAsync_AbsoluteLeadingCurrentPositionClick_DoesNotPreMoveToLaterAbsoluteEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true,
                },
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 900,
                    Y = 700,
                },
            },
        };

        // Act
        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task PrepareIterationAsync_AbsoluteLeadingCurrentPositionClick_DoesNotPreMoveToLaterAbsoluteEvent()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.UpdatePosition(300, 200);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true,
                },
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 900,
                    Y = 700,
                },
            },
        };

        // Act
        await coordinator.PrepareIterationAsync(1, macro, simulator, 1920, 1080, CancellationToken.None);

        // Assert
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task InitializeAsync_LegacyRelativeMacroWithExplicitAbsoluteFirstEvent_DoesNotPreMoveFirstEvent()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 300,
                    Y = 250,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _ = coordinator.CurrentX.Should().Be(0);
        _ = coordinator.CurrentY.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_LegacyAbsoluteMacroWithExplicitRelativeFirstEvent_UsesRelativePreparation()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 1920, 1080, CancellationToken.None);

        simulator.Received(2).MoveRelative(-20000, 0);
        simulator.Received(2).MoveRelative(0, -20000);
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task InitializeAsync_RelativeMacro_UsesDesktopOriginResetCapabilityBeforeRelativeFallback()
    {
        var simulator = Substitute.For<IInputSimulator, IDesktopOriginResetSimulator>();
        var originReset = (IDesktopOriginResetSimulator)simulator;
        _ = originReset.TryResetToDesktopOrigin().Returns(true);
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.ConfigureDesktopBounds(new ScreenRect(0, 0, 3840, 1080));
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = -5,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        await coordinator.InitializeAsync(macro, simulator, 3840, 1080, CancellationToken.None);

        _ = originReset.Received(1).TryResetToDesktopOrigin();
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task PrepareIterationAsync_LegacyRelativeMacroWithExplicitAbsoluteFirstEvent_DoesNotPreMoveFirstEvent()
    {
        var simulator = Substitute.For<IInputSimulator>();
        var coordinator = new DefaultPlaybackCoordinator();
        coordinator.UpdatePosition(20, 30);
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 300,
                    Y = 250,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        await coordinator.PrepareIterationAsync(1, macro, simulator, 1920, 1080, CancellationToken.None);

        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _ = coordinator.CurrentX.Should().Be(20);
        _ = coordinator.CurrentY.Should().Be(30);
    }
}
