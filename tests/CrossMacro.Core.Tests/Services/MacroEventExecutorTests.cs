using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using NSubstitute;
using Xunit;

namespace CrossMacro.Core.Tests.Services;

public class MacroEventExecutorTests
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

    [Fact]
    public void Execute_MouseMove_Relative_MovesSimulatorAndUpdatesCoordinator()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.MouseMove, X = 10, Y = 20 };
        
        // Act
        _executor.Execute(ev, isRecordedAbsolute: false);

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
        _executor.Execute(ev, isRecordedAbsolute: true);

        // Assert: absolute path – no button held, so MoveAbsolute is used for drift correction
        _simulator.Received(1).MoveAbsolute(100, 80);
        _simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
        _coordinator.Received(1).UpdatePosition(100, 80);
    }

    [Fact]
    public void Execute_MouseMove_Absolute_ButtonPressed_UsesRelativeDeltaForSmoothCurve()
    {
        // Arrange: simulate a button being held and the coordinator reporting previous position
        _buttonTracker.IsAnyPressed.Returns(true);
        _coordinator.CurrentX.Returns(60);
        _coordinator.CurrentY.Returns(40);

        var ev = new MacroEvent { Type = EventType.MouseMove, X = 100, Y = 80 };

        // Act
        _executor.Execute(ev, isRecordedAbsolute: true);

        // Assert: drift-correction absolute first, then relative delta (100-60=40, 80-40=40)
        _simulator.Received(1).MoveAbsolute(60, 40);
        _simulator.Received(1).MoveRelative(40, 40);
        _coordinator.Received(1).UpdatePosition(100, 80);
    }

    [Fact]
    public void Execute_ButtonPress_MapsButtonAndEmits()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.ButtonPress, Button = MouseButton.Left };
        _buttonMapper.Map(MouseButton.Left).Returns((int)MouseButton.Left);

        // Act
        _executor.Execute(ev, isRecordedAbsolute: false);

        // Assert
        _simulator.Received(1).MouseButton((ushort)MouseButton.Left, true);
        _buttonTracker.Received(1).Press((ushort)MouseButton.Left);
    }

    [Fact]
    public void Execute_ButtonRelease_MapsButtonAndEmits()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.ButtonRelease, Button = MouseButton.Left };
        _buttonMapper.Map(MouseButton.Left).Returns((int)MouseButton.Left);

        // Act
        _executor.Execute(ev, isRecordedAbsolute: false);

        // Assert
        _simulator.Received(1).MouseButton((ushort)MouseButton.Left, false);
        _buttonTracker.Received(1).Release((ushort)MouseButton.Left);
    }

    [Fact]
    public void Execute_KeyPress_EmitsKey()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 };

        // Act
        _executor.Execute(ev, isRecordedAbsolute: false);

        // Assert
        _simulator.Received(1).KeyPress(30, true);
        _keyTracker.Received(1).Press(30);
    }

    [Fact]
    public void Execute_Click_SimulatesPressAndRelease()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.Click, Button = MouseButton.Right };
        _buttonMapper.Map(MouseButton.Right).Returns((int)MouseButton.Right);

        // Act
        _executor.Execute(ev, isRecordedAbsolute: false);

        // Assert
        _simulator.Received(1).MouseButton((ushort)MouseButton.Right, true);
        _simulator.Received(1).MouseButton((ushort)MouseButton.Right, false);
    }

    [Fact]
    public void Execute_Scroll_SimulatesScroll()
    {
        // Arrange
        var ev = new MacroEvent { Type = EventType.Click, Button = MouseButton.ScrollUp };

        // Act
        _executor.Execute(ev, isRecordedAbsolute: false);

        // Assert
        _simulator.Received(1).Scroll(1);
    }
}
