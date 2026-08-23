namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class EditorActionConverterTests
{

    [Fact]
    public void ToMacroEvents_KeyPress_ExpandsToPressAndRelease()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.KeyPress,
            KeyCode = 30,
            DelayMs = 55,
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        _ = events.Should().HaveCount(2);
        _ = events[0].Type.Should().Be(EventType.KeyPress);
        _ = events[0].KeyCode.Should().Be(30);
        _ = events[0].DelayMs.Should().Be(55);
        _ = events[1].Type.Should().Be(EventType.KeyRelease);
        _ = events[1].KeyCode.Should().Be(30);
        _ = events[1].DelayMs.Should().Be(10);
    }

    [Fact]
    public void EditorProjectionBoundary_PreservesSequenceConversionArguments()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, X = 12, Y = 34, IsAbsolute = false },
        };
        var projection = new EditorMacroProjection(actions, "Projection", isAbsoluteCoordinates: false, skipInitialZeroZero: true);

        var sequence = _converter.ToMacroSequence(projection);
        var restored = _converter.FromMacroSequenceProjection(sequence);

        _ = sequence.Name.Should().Be("Projection");
        _ = sequence.IsAbsoluteCoordinates.Should().BeFalse();
        _ = sequence.SkipInitialZeroZero.Should().BeTrue();
        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = restored.Actions[0].X.Should().Be(12);
        _ = restored.Actions[0].Y.Should().Be(34);
        _ = restored.Name.Should().Be("Projection");
        _ = restored.IsAbsoluteCoordinates.Should().BeFalse();
        _ = restored.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void ToMacroEvents_CurrentPositionClick_UsesZeroCoordinates()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.Left,
            IsAbsolute = false,
            UseCurrentPosition = true,
            X = 120,
            Y = 240,
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        _ = events.Should().HaveCount(1);
        _ = events[0].Type.Should().Be(EventType.Click);
        _ = events[0].X.Should().Be(0);
        _ = events[0].Y.Should().Be(0);
        _ = events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void ToMacroEvents_CurrentPositionMouseDown_UsesZeroCoordinates()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseDown,
            Button = MacroMouseButton.Left,
            IsAbsolute = false,
            UseCurrentPosition = true,
            X = 120,
            Y = 240,
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        _ = events.Should().HaveCount(1);
        _ = events[0].Type.Should().Be(EventType.ButtonPress);
        _ = events[0].X.Should().Be(0);
        _ = events[0].Y.Should().Be(0);
        _ = events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void ToMacroEvents_MouseClickWithCoordinates_EmitsSingleClickEventWithCoordinateMode()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.Left,
            IsAbsolute = true,
            UseCurrentPosition = false,
            X = 120,
            Y = 240,
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        _ = events.Should().ContainSingle();
        _ = events[0].Type.Should().Be(EventType.Click);
        _ = events[0].X.Should().Be(120);
        _ = events[0].Y.Should().Be(240);
        _ = events[0].Button.Should().Be(MacroMouseButton.Left);
        _ = events[0].UseCurrentPosition.Should().BeFalse();
        _ = events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
    }

    [Theory]
    [InlineData(EditorActionType.MouseClick, EventType.Click)]
    [InlineData(EditorActionType.MouseDown, EventType.ButtonPress)]
    [InlineData(EditorActionType.MouseUp, EventType.ButtonRelease)]
    public void ToMacroEvents_WhenMouseButtonActionIsRawRelative_PreservesRelativeCoordinateSemantics(
        EditorActionType actionType,
        EventType eventType)
    {
        var action = new EditorAction
        {
            Type = actionType,
            Button = MacroMouseButton.Left,
            IsAbsolute = false,
            X = 3,
            Y = -5,
        };

        var events = _converter.ToMacroEvents(action);

        _ = events.Should().ContainSingle();
        _ = events[0].Type.Should().Be(eventType);
        _ = events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = events[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = events[0].X.Should().Be(3);
        _ = events[0].Y.Should().Be(-5);
    }

    [Fact]
    public void ToMacroEvents_DelayWithRandom_ProducesPlaceholderWithRandomMetadata()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.Delay,
            UseRandomDelay = true,
            RandomDelayMinMs = 100,
            RandomDelayMaxMs = 200,
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        _ = events.Should().HaveCount(1);
        _ = events[0].Type.Should().Be(EventType.None);
        _ = events[0].DelayMs.Should().Be(0);
        _ = events[0].HasRandomDelay.Should().BeTrue();
        _ = events[0].RandomDelayMinMs.Should().Be(100);
        _ = events[0].RandomDelayMaxMs.Should().Be(200);
    }

    [Fact]
    public void ToMacroSequence_WhenDelayIsTrailing_SetsTrailingDelayMs()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20, DelayMs = 0 },
            new EditorAction { Type = EditorActionType.Delay, DelayMs = 250 },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Test", isAbsolute: true);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = sequence.TrailingDelayMs.Should().Be(250);
    }

    [Fact]
    public void ToMacroSequence_WhenTrailingDelayUsesMicroseconds_PreservesItInBothEditorDirections()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20 },
            new EditorAction { Type = EditorActionType.Delay, DelayMicroseconds = 2_375 },
        };

        var sequence = _converter.ToMacroSequence(actions, "Test", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.TrailingDelayMicroseconds.Should().Be(2_375);
        _ = restored.Where(action => action.Type is EditorActionType.Delay && action.DelayMicroseconds == 2_375)
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void ToMacroSequence_WhenRandomDelayIsTrailing_SetsTrailingRandomDelay()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20, DelayMs = 0 },
            new EditorAction
            {
                Type = EditorActionType.Delay,
                UseRandomDelay = true,
                RandomDelayMinMs = 50,
                RandomDelayMaxMs = 120,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Test", isAbsolute: true);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.TrailingDelayMs.Should().Be(0);
        _ = sequence.HasTrailingRandomDelay.Should().BeTrue();
        _ = sequence.TrailingDelayMinMs.Should().Be(50);
        _ = sequence.TrailingDelayMaxMs.Should().Be(120);
    }

    [Fact]
    public void ToMacroSequence_WhenFixedAndRandomDelayBeforeEvent_PreservesBoth()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.Delay, DelayMs = 30 },
            new EditorAction
            {
                Type = EditorActionType.Delay,
                UseRandomDelay = true,
                RandomDelayMinMs = 10,
                RandomDelayMaxMs = 20,
            },
            new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20, DelayMs = 0 },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Test", isAbsolute: true);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].DelayMs.Should().Be(30);
        _ = sequence.Events[0].HasRandomDelay.Should().BeTrue();
        _ = sequence.Events[0].RandomDelayMinMs.Should().Be(10);
        _ = sequence.Events[0].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void FromMacroEvent_WhenPressFollowedByRelease_MergesToKeyPressAction()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyName(30).Returns("A");
        var keyPress = new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, DelayMs = 15 };
        var keyRelease = new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 };

        // Act
        var action = _converter.FromMacroEvent(keyPress, keyRelease);

        // Assert
        _ = action.Type.Should().Be(EditorActionType.KeyPress);
        _ = action.KeyCode.Should().Be(30);
        _ = action.KeyName.Should().Be("A");
        _ = action.DelayMs.Should().Be(15);
    }

    [Theory]
    [InlineData(EventType.KeyPress, EditorActionType.KeyDown)]
    [InlineData(EventType.KeyRelease, EditorActionType.KeyUp)]
    public void FromMacroEvent_WhenKeyboardEvent_RestoresKeyName(EventType eventType, EditorActionType expectedActionType)
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyName(18).Returns("E");
        var macroEvent = new MacroEvent { Type = eventType, KeyCode = 18 };

        // Act
        var action = _converter.FromMacroEvent(macroEvent);

        // Assert
        _ = action.Type.Should().Be(expectedActionType);
        _ = action.KeyCode.Should().Be(18);
        _ = action.KeyName.Should().Be("E");
    }

    [Fact]
    public void ToMacroEvents_WhenMouseActionHasCoordinates_EmitsActionCoordinateMode()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 10, Y = 20 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, IsAbsolute = false, X = 3, Y = 4 },
            new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Right, IsAbsolute = true, X = 30, Y = 40 },
            new EditorAction
            {
                Type = EditorActionType.MouseUp,
                Button = MacroMouseButton.Right,
                IsAbsolute = false,
                CoordinateSpace = MouseCoordinateSpace.RawDevice,
                X = 5,
                Y = 6,
            },
        };

        // Act
        var events = actions.Select(action => _converter.ToMacroEvents(action).Single()).ToList();

        // Assert
        _ = events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = events[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = events[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = events[1].Type.Should().Be(EventType.Click);
        _ = events[2].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = events[2].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = events[3].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = events[3].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
    }

    [Fact]
    public void ToMacroEvents_WhenCurrentPositionOrScroll_DoesNotEmitCoordinateMode()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = true, X = 10, Y = 20 },
            new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = true, X = 30, Y = 40 },
            new EditorAction { Type = EditorActionType.MouseUp, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = true, X = 50, Y = 60 },
            new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = 1, IsAbsolute = true },
            new EditorAction { Type = EditorActionType.ScrollHorizontal, ScrollAmount = -1, IsAbsolute = true },
        };

        // Act
        var events = actions.SelectMany(action => _converter.ToMacroEvents(action)).ToList();

        // Assert
        _ = events.Should().OnlyContain(ev => ev.CoordinateMode == null);
        _ = events[0].UseCurrentPosition.Should().BeTrue();
        _ = events[0].X.Should().Be(0);
        _ = events[0].Y.Should().Be(0);
        _ = events[1].UseCurrentPosition.Should().BeTrue();
        _ = events[1].X.Should().Be(0);
        _ = events[1].Y.Should().Be(0);
        _ = events[2].UseCurrentPosition.Should().BeTrue();
        _ = events[2].X.Should().Be(0);
        _ = events[2].Y.Should().Be(0);
        _ = events[3].Button.Should().Be(MacroMouseButton.ScrollUp);
        _ = events[4].Button.Should().Be(MacroMouseButton.ScrollLeft);
    }

    [Fact]
    public void FromMacroEvent_WhenCoordinateModePresent_SetsActionMode()
    {
        // Arrange
        var moveEvent = new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 10,
            Y = 20,
            CoordinateMode = MouseCoordinateMode.Absolute,
        };
        var clickEvent = new MacroEvent
        {
            Type = EventType.Click,
            Button = MacroMouseButton.Left,
            X = 3,
            Y = 4,
            CoordinateMode = MouseCoordinateMode.Relative,
        };

        // Act
        var moveAction = _converter.FromMacroEvent(moveEvent);
        var clickAction = _converter.FromMacroEvent(clickEvent);

        // Assert
        _ = moveAction.IsAbsolute.Should().BeTrue();
        _ = moveAction.CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = clickAction.IsAbsolute.Should().BeFalse();
        _ = clickAction.CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = clickAction.UseCurrentPosition.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenRecordedPrintableKeysHaveNoBoundary_PreservesRawTimingActions()
    {
        // Arrange
        _ = _keyCodeMapper.GetCharacterForKeyCode(30, withShift: false).Returns('a');
        _ = _keyCodeMapper.GetCharacterForKeyCode(48, withShift: false).Returns('b');

        var sequence = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, DelayMs = 12 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30, DelayMs = 0 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48, DelayMs = 10 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48, DelayMs = 0 },
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(4);
        _ = actions[0].Type.Should().Be(EditorActionType.Delay);
        _ = actions[0].DelayMs.Should().Be(12);
        _ = actions[1].Type.Should().Be(EditorActionType.KeyPress);
        _ = actions[1].KeyCode.Should().Be(30);
        _ = actions[1].DelayMs.Should().Be(0);
        _ = actions[2].Type.Should().Be(EditorActionType.Delay);
        _ = actions[2].DelayMs.Should().Be(10);
        _ = actions[3].Type.Should().Be(EditorActionType.KeyPress);
        _ = actions[3].KeyCode.Should().Be(48);
        _ = actions[3].DelayMs.Should().Be(0);
    }

    [Fact]
    public void FromMacroSequence_WhenEventContainsRandomDelay_AddsRandomDelayActionBeforeEvent()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 20,
                    DelayMs = 0,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 70,
                    RandomDelayMaxMs = 130,
                },
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.Delay);
        _ = actions[0].UseRandomDelay.Should().BeTrue();
        _ = actions[0].RandomDelayMinMs.Should().Be(70);
        _ = actions[0].RandomDelayMaxMs.Should().Be(130);
        _ = actions[1].Type.Should().Be(EditorActionType.MouseMove);
    }

    [Fact]
    public void FromMacroSequence_WhenAbsoluteModeAndMouseButtonEvents_SetsActionsAbsolute()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 120, Y = 220 },
                new MacroEvent { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 130, Y = 230 },
                new MacroEvent { Type = EventType.ButtonRelease, Button = MacroMouseButton.Left, X = 140, Y = 240 },
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(3);
        _ = actions.Should().OnlyContain(a => a.IsAbsolute);
    }

    [Fact]
    public void FromMacroSequence_WhenRelativeModeAndMouseButtonEvents_SetsActionsRelative()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 0, Y = 0 },
                new MacroEvent { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 5, Y = -3 },
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions.Should().OnlyContain(a => !a.IsAbsolute);
    }

    [Fact]
    public void FromMacroSequence_WhenRelativeZeroCoordinateClick_MarksUseCurrentPosition()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 0, Y = 0 },
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(1);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[0].UseCurrentPosition.Should().BeTrue();
        _ = actions[0].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenExplicitCurrentPositionClick_MarksUseCurrentPosition()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
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
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(1);
        _ = actions[0].UseCurrentPosition.Should().BeTrue();
        _ = actions[0].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenAbsoluteMacroContainsCurrentPositionClick_KeepsClickRelative()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 640,
                    Y = 480,
                    UseCurrentPosition = true,
                },
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 800,
                    Y = 600,
                },
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[0].UseCurrentPosition.Should().BeTrue();
        _ = actions[0].IsAbsolute.Should().BeFalse();
        _ = actions[1].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[1].IsAbsolute.Should().BeTrue();
    }

    [Fact]
    public void FromMacroSequence_WhenEventCoordinateModesAreMixed_RestoresPerActionMode()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 5,
                    Y = -2,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
                new MacroEvent
                {
                    Type = EventType.ButtonPress,
                    Button = MacroMouseButton.Right,
                    X = 300,
                    Y = 400,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new MacroEvent
                {
                    Type = EventType.ButtonRelease,
                    Button = MacroMouseButton.Right,
                    X = -1,
                    Y = -1,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.RawDevice,
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Middle,
                    UseCurrentPosition = true,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(5);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[0].IsAbsolute.Should().BeTrue();
        _ = actions[1].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[1].IsAbsolute.Should().BeFalse();
        _ = actions[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = actions[1].UseCurrentPosition.Should().BeFalse();
        _ = actions[2].Type.Should().Be(EditorActionType.MouseDown);
        _ = actions[2].IsAbsolute.Should().BeTrue();
        _ = actions[3].Type.Should().Be(EditorActionType.MouseUp);
        _ = actions[3].IsAbsolute.Should().BeFalse();
        _ = actions[3].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = actions[4].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[4].UseCurrentPosition.Should().BeTrue();
        _ = actions[4].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenCoordinateModeMissing_UsesLegacySequenceFallback()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove, X = 10, Y = 20 },
                new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 30, Y = 40 },
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions.Should().OnlyContain(action => action.IsAbsolute);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenActionsUseMixedCoordinateModes_PreservesModesOnEventsAndActions()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 200 },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 5, Y = -3 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, IsAbsolute = true, X = 300, Y = 400 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = -1, IsAbsolute = true },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Mixed Modes", isAbsolute: false);
        var restored = _converter.FromMacroSequence(sequence);

        // Assert
        _ = sequence.Events.Should().HaveCount(5);
        _ = sequence.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = sequence.Events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = sequence.Events[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = sequence.Events[2].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = sequence.Events[2].Type.Should().Be(EventType.Click);
        _ = sequence.Events[3].UseCurrentPosition.Should().BeTrue();
        _ = sequence.Events[3].CoordinateMode.Should().BeNull();
        _ = sequence.Events[4].Button.Should().Be(MacroMouseButton.ScrollDown);
        _ = sequence.Events[4].CoordinateMode.Should().BeNull();

        _ = restored.Should().HaveCount(5);
        _ = restored[0].IsAbsolute.Should().BeTrue();
        _ = restored[1].IsAbsolute.Should().BeFalse();
        _ = restored[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = restored[2].IsAbsolute.Should().BeTrue();
        _ = restored[3].UseCurrentPosition.Should().BeTrue();
        _ = restored[3].IsAbsolute.Should().BeFalse();
        _ = restored[4].Type.Should().Be(EditorActionType.ScrollVertical);
    }

    [Fact]
    public async Task ToMacroSequence_SaveLoadAndRestore_WhenActionsUseMixedModes_PreservesEventModesAndCurrentPosition()
    {
        // Arrange
        var fileManager = new MacroFileManager(() => _keyCodeMapper);
        var filePath = Path.Combine(Path.GetTempPath(), $"mixed_editor_roundtrip_{Guid.NewGuid()}.macro");
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 200 },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 5, Y = -3 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
        };

        try
        {
            // Act
            var sequence = _converter.ToMacroSequence(actions, "Mixed Editor Round Trip", isAbsolute: true);
            await fileManager.SaveAsync(sequence, filePath);
            var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
            var loaded = await fileManager.LoadAsync(filePath);
            var restored = _converter.FromMacroSequence(loaded!);

            // Assert
            _ = sequence.Events.Select(ev => ev.CoordinateMode).Should().Equal(
                MouseCoordinateMode.Absolute,
                MouseCoordinateMode.Relative,
                null);
            _ = saved.Should().Contain("M,abs,100,200");
            _ = sequence.Events.Select(ev => ev.CoordinateSpace).Should().Equal(
                MouseCoordinateSpace.LogicalDesktop,
                MouseCoordinateSpace.RawDevice,
                null);
            _ = saved.Should().Contain("M,rel-raw,5,-3");
            _ = saved.Should().Contain("C,0,0,Left,CurrentPosition");
            _ = saved.Should().NotContain("C,abs,0,0,Left");
            _ = saved.Should().NotContain("C,rel,0,0,Left");

            _ = loaded.Should().NotBeNull();
            _ = loaded!.Events.Select(ev => ev.CoordinateMode).Should().Equal(
                MouseCoordinateMode.Absolute,
                MouseCoordinateMode.Relative,
                null);
            _ = loaded.Events.Select(ev => ev.CoordinateSpace).Should().Equal(
                MouseCoordinateSpace.LogicalDesktop,
                MouseCoordinateSpace.RawDevice,
                null);
            _ = restored.Should().HaveCount(3);
            _ = restored[0].IsAbsolute.Should().BeTrue();
            _ = restored[1].IsAbsolute.Should().BeFalse();
            _ = restored[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
            _ = restored[2].UseCurrentPosition.Should().BeTrue();
            _ = restored[2].IsAbsolute.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenRelativeActionUsesRawDeviceSpace_PreservesIt()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = false,
                CoordinateSpace = MouseCoordinateSpace.RawDevice,
                X = 7,
                Y = -4,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Raw relative", isAbsolute: false);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = sequence.Events[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = restored.Should().ContainSingle();
        _ = restored[0].IsAbsolute.Should().BeFalse();
        _ = restored[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenRelativeActionUsesLogicalDesktopSpace_PreservesIt()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = false,
                CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                X = 7,
                Y = -4,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Logical relative", isAbsolute: false);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = restored.Should().ContainSingle();
        _ = restored[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredSetTextContainsEquals_UsesUnambiguousSetSyntax()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "x",
                ScriptValueType = ScriptValueType.Text,
                ScriptValue = "a=b",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Set Equals Text", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "set x=a=b",
            "click current left");
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredSetTextStartsWithDollar_EscapesLiteralDollar()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "name",
                ScriptValueType = ScriptValueType.Text,
                ScriptValue = "$foo",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Set Dollar Text", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "set name $$foo",
            "click current left");
        _ = sequence.Events.Should().HaveCount(1);
    }

    [Fact]
    public void ToMacroSequence_WhenLegacySetActionEditedThenResetToDefaults_UsesStructuredSerialization()
    {
        // Arrange
        var loadedActions = _converter.FromMacroSequence(new MacroSequence
        {
            ScriptSteps =
            {
                "set 1bad=0",
                "click current left",
            },
        });

        _ = loadedActions.Should().HaveCount(2);
        _ = loadedActions[0].Type.Should().Be(EditorActionType.SetVariable);
        _ = loadedActions[0].Text.Should().Be("1bad=0");
        _ = loadedActions[0].PreferLegacyScriptText.Should().BeTrue();

        // Simulate editing via structured controls and then returning to defaults.
        loadedActions[0].ScriptVariableName = "counter";
        loadedActions[0].ScriptVariableName = "i";
        loadedActions[0].ScriptValue = "5";
        loadedActions[0].ScriptValue = "0";

        _ = loadedActions[0].PreferLegacyScriptText.Should().BeFalse();

        // Act
        var sequence = _converter.ToMacroSequence(loadedActions, "Legacy Reset Defaults", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "set i 0",
            "click current left");
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void FromMacroSequence_WhenSetStepUsesEscapedDollar_RestoresTextLiteral()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set name $$foo",
                "click current left",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.SetVariable);
        _ = actions[0].ScriptVariableName.Should().Be("name");
        _ = actions[0].ScriptValueType.Should().Be(ScriptValueType.Text);
        _ = actions[0].ScriptValue.Should().Be("$foo");
    }

    [Fact]
    public void FromMacroSequence_WhenAbsoluteMoveAndDelayedImplicitClick_PreservesPositionedButtonSemantics()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move abs 500 300",
                "delay 50",
                "click left",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(3);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[0].IsAbsolute.Should().BeTrue();
        _ = actions[0].X.Should().Be(500);
        _ = actions[0].Y.Should().Be(300);
        _ = actions[1].Type.Should().Be(EditorActionType.Delay);
        _ = actions[1].DelayMs.Should().Be(50);
        _ = actions[2].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[2].UseCurrentPosition.Should().BeFalse();
        _ = actions[2].IsAbsolute.Should().BeTrue();
        _ = actions[2].X.Should().Be(500);
        _ = actions[2].Y.Should().Be(300);
    }

    [Fact]
    public void FromMacroSequence_WhenNumericComparisonUsesSixDigitNumber_KeepsNumberOperand()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $count > 100000 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(3);
        _ = actions[0].ScriptConditionOperator.Should().Be(ScriptConditionOperator.GreaterThan);
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Number);
        _ = actions[0].ScriptRightOperand.Should().Be("100000");
    }
}
