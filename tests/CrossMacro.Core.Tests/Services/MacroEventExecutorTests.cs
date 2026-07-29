
namespace CrossMacro.Core.Tests.Services;

public sealed class MacroEventExecutorTests : IDisposable
{
    private readonly IInputSimulator _simulator;
    private readonly IButtonStateTracker _buttonTracker;
    private readonly IKeyStateTracker _keyTracker;
    private readonly IPlaybackMouseButtonMapper _buttonMapper;
    private readonly IPlaybackCoordinator _coordinator;
    private readonly MacroEventExecutor _executor;

    public MacroEventExecutorTests()
    {
        _simulator = Substitute.For<IInputSimulator>();
        _buttonTracker = Substitute.For<IButtonStateTracker>();
        _keyTracker = Substitute.For<IKeyStateTracker>();
        _buttonMapper = Substitute.For<IPlaybackMouseButtonMapper>();
        _coordinator = Substitute.For<IPlaybackCoordinator>();

        _executor = new MacroEventExecutor(
            _simulator,
            _buttonTracker,
            _keyTracker,
            _buttonMapper,
            _coordinator);

        _executor.Initialize(1920, 1080);
    }

    public void Dispose() => _executor.Dispose();

    [Fact]
    public void Execute_MouseMove_Relative_MovesSimulatorAndUpdatesCoordinator()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.MouseMove, X = 10, Y = 20 };

        // Act
        _executor.Execute(ev, MouseCoordinateMode.Relative);

        // Assert
        _simulator.Received(1).MoveRelative(10, 20);
        _coordinator.Received(1).AddDelta(10, 20);
    }

    [Fact]
    public void Execute_MouseMove_Absolute_NoButtonPressed_UsesAbsoluteAndUpdatesCoordinator()
    {
        // Arrange
        // No button pressed → IsAnyPressed returns false (default NSubstitute behaviour)
        var ev = new MacroEvent { Type = EventType.MouseMove, X = 100, Y = 80 };

        // Act
        _executor.Execute(ev, MouseCoordinateMode.Absolute);

        // Assert: absolute path – no button held, so MoveAbsolute is used for drift correction
        _simulator.Received(1).MoveAbsolute(100, 80);
        _simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _coordinator.Received(1).UpdatePosition(100, 80);
    }

    [Fact]
    public void Execute_MouseMove_Absolute_ButtonPressed_UsesRelativeDeltaForSmoothCurve()
    {
        // Arrange: simulate a button being held and the coordinator reporting previous position
        _ = _buttonTracker.IsAnyPressed.Returns(returnThis: true);
        _ = _coordinator.CurrentX.Returns(60);
        _ = _coordinator.CurrentY.Returns(40);

        var ev = new MacroEvent { Type = EventType.MouseMove, X = 100, Y = 80 };

        // Act
        _executor.Execute(ev, MouseCoordinateMode.Absolute);

        // Assert: drift-correction absolute first, then relative delta (100-60=40, 80-40=40)
        _simulator.Received(1).MoveAbsolute(60, 40);
        _simulator.Received(1).MoveRelative(40, 40);
        _coordinator.Received(1).UpdatePosition(100, 80);
    }

    [Fact]
    public void Execute_MouseMove_Absolute_ButtonPressed_WhenHybridDisabled_UsesAbsoluteOnly()
    {
        // Arrange
        _ = _buttonTracker.IsAnyPressed.Returns(returnThis: true);
        _ = _coordinator.CurrentX.Returns(60);
        _ = _coordinator.CurrentY.Returns(40);

        var executor = new MacroEventExecutor(
            _simulator,
            _buttonTracker,
            _keyTracker,
            _buttonMapper,
            _coordinator,
            useHybridAbsoluteDragMovement: false);
        executor.Initialize(1920, 1080);

        var ev = new MacroEvent { Type = EventType.MouseMove, X = 100, Y = 80 };

        // Act
        executor.Execute(ev, MouseCoordinateMode.Absolute);

        // Assert
        _simulator.Received(1).MoveAbsolute(100, 80);
        _simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _coordinator.Received(1).UpdatePosition(100, 80);
    }

    [Fact]
    public void Execute_ButtonPress_MapsButtonAndEmits()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.ButtonPress, Button = MacroMouseButton.Left };
        _ = _buttonMapper.Map(MacroMouseButton.Left).Returns((int)MacroMouseButton.Left);

        // Act
        _executor.Execute(ev, coordinateMode: null);

        // Assert
        _simulator.Received(1).MouseButton((ushort)MacroMouseButton.Left, pressed: true);
        _buttonTracker.Received(1).Press((ushort)MacroMouseButton.Left);
    }

    [Fact]
    public void Execute_ButtonRelease_MapsButtonAndEmits()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.ButtonRelease, Button = MacroMouseButton.Left };
        _ = _buttonMapper.Map(MacroMouseButton.Left).Returns((int)MacroMouseButton.Left);

        // Act
        _executor.Execute(ev, coordinateMode: null);

        // Assert
        _simulator.Received(1).MouseButton((ushort)MacroMouseButton.Left, pressed: false);
        _buttonTracker.Received(1).Release((ushort)MacroMouseButton.Left);
    }

    [Fact]
    public void Execute_KeyPress_EmitsKey()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 };

        // Act
        _executor.Execute(ev, coordinateMode: null);

        // Assert
        _simulator.Received(1).KeyPress(30, pressed: true);
        _keyTracker.Received(1).Press(30);
    }

    [Fact]
    public void Execute_Click_SimulatesPressAndRelease()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Right };
        _ = _buttonMapper.Map(MacroMouseButton.Right).Returns((int)MacroMouseButton.Right);

        // Act
        _executor.Execute(ev, coordinateMode: null);

        // Assert
        _simulator.Received(1).MouseButton((ushort)MacroMouseButton.Right, pressed: true);
        _simulator.Received(1).MouseButton((ushort)MacroMouseButton.Right, pressed: false);
    }

    [Fact]
    public void Execute_CurrentPositionClick_IgnoresStoredCoordinatesForImplicitMovement()
    {
        // Arrange
        var ev = new MacroEvent
        {
            Type = EventType.Click,
            Button = MacroMouseButton.Left,
            X = 500,
            Y = 300,
            UseCurrentPosition = true,
        };
        _ = _buttonMapper.Map(MacroMouseButton.Left).Returns((int)MacroMouseButton.Left);

        // Act
        _executor.Execute(ev, coordinateMode: null);

        // Assert
        _simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        _simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _simulator.Received(1).MouseButton((ushort)MacroMouseButton.Left, pressed: true);
        _simulator.Received(1).MouseButton((ushort)MacroMouseButton.Left, pressed: false);
    }

    [Fact]
    public void Execute_MouseMove_Absolute_WhenSimulatorCannotMoveAbsolute_Throws()
    {
        var simulator = new TrackingSimulator(supportsAbsoluteCoordinates: false);
        var coordinator = Substitute.For<IPlaybackCoordinator>();
        _ = coordinator.CurrentX.Returns(25);
        _ = coordinator.CurrentY.Returns(40);

        var executor = new MacroEventExecutor(
            simulator,
            _buttonTracker,
            _keyTracker,
            _buttonMapper,
            coordinator);
        executor.Initialize(0, 0);

        void act() => executor.Execute(new MacroEvent { Type = EventType.MouseMove, X = 100, Y = 90 }, MouseCoordinateMode.Absolute);

        _ = Assert.Throws<AbsolutePlaybackUnsupportedException>(act);
        Assert.Null(simulator.LastRelativeMove);
        Assert.Null(simulator.LastAbsoluteMove);
        coordinator.DidNotReceive().AddDelta(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public void Execute_Scroll_SimulatesScroll()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.ScrollUp };

        // Act
        _executor.Execute(ev, coordinateMode: null);

        // Assert
        _simulator.Received(1).Scroll(1);
    }

    [Fact]
    public void Execute_ButtonPress_Absolute_MovesToRecordedPositionBeforeButton()
    {
        var ev = new MacroEvent { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 100, Y = 200 };
        _ = _buttonMapper.Map(MacroMouseButton.Left).Returns((int)MacroMouseButton.Left);

        _executor.Execute(ev, MouseCoordinateMode.Absolute);

        Received.InOrder(() =>
        {
            _simulator.MoveAbsolute(100, 200);
            _simulator.MouseButton((ushort)MacroMouseButton.Left, pressed: true);
        });
        _coordinator.Received(1).UpdatePosition(100, 200);
    }

    [Fact]
    public void Execute_ButtonPress_Relative_MovesByDeltaBeforeButton()
    {
        var ev = new MacroEvent { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 10, Y = -5 };
        _ = _buttonMapper.Map(MacroMouseButton.Left).Returns((int)MacroMouseButton.Left);

        _executor.Execute(ev, MouseCoordinateMode.Relative);

        Received.InOrder(() =>
        {
            _simulator.MoveRelative(10, -5);
            _simulator.MouseButton((ushort)MacroMouseButton.Left, pressed: true);
        });
        _coordinator.Received(1).AddDelta(10, -5);
    }

    [Fact]
    public void Execute_ButtonPress_NullMode_EmitsButtonWithoutImplicitMovement()
    {
        var ev = new MacroEvent { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 10, Y = -5 };
        _ = _buttonMapper.Map(MacroMouseButton.Left).Returns((int)MacroMouseButton.Left);

        _executor.Execute(ev, coordinateMode: null);

        _simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        _simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _simulator.Received(1).MouseButton((ushort)MacroMouseButton.Left, pressed: true);
    }

    [Fact]
    public void Execute_ButtonPress_Absolute_WhenSimulatorCannotMoveAbsolute_ThrowsBeforeButton()
    {
        var simulator = new TrackingSimulator(supportsAbsoluteCoordinates: false);
        var coordinator = Substitute.For<IPlaybackCoordinator>();
        _ = coordinator.CurrentX.Returns(25);
        _ = coordinator.CurrentY.Returns(40);
        _ = _buttonMapper.Map(MacroMouseButton.Left).Returns((int)MacroMouseButton.Left);

        var executor = new MacroEventExecutor(
            simulator,
            _buttonTracker,
            _keyTracker,
            _buttonMapper,
            coordinator);
        executor.Initialize(0, 0);

        void act() => executor.Execute(new MacroEvent { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 100, Y = 90 }, MouseCoordinateMode.Absolute);

        _ = Assert.Throws<AbsolutePlaybackUnsupportedException>(act);
        Assert.Null(simulator.LastRelativeMove);
        Assert.Null(simulator.LastAbsoluteMove);
        coordinator.DidNotReceive().AddDelta(Arg.Any<int>(), Arg.Any<int>());
        _ = simulator.ButtonTransitions.Should().BeEmpty();
    }

    private sealed class TrackingSimulator(bool supportsAbsoluteCoordinates) : IInputSimulator, IInputSimulatorCapabilities
    {
        public string ProviderName => "Tracking";
        public bool IsSupported => true;
        public bool SupportsAbsoluteCoordinates { get; } = supportsAbsoluteCoordinates;
        public (int X, int Y)? LastAbsoluteMove { get; private set; }
        public (int X, int Y)? LastRelativeMove { get; private set; }
        public List<(int Button, bool Pressed)> ButtonTransitions { get; } = new();

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
        }

        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(screenWidth, screenHeight);
            return Task.CompletedTask;
        }

        public void MoveAbsolute(int x, int y)
        {
            LastAbsoluteMove = (x, y);
        }

        public void MoveRelative(int dx, int dy)
        {
            LastRelativeMove = (dx, dy);
        }

        public void MouseButton(int button, bool pressed)
        {
            ButtonTransitions.Add((button, pressed));
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
        }

        public void KeyPress(int keyCode, bool pressed)
        {
        }

        public void Sync()
        {
        }

        public void Dispose()
        {
        }
    }
}
