
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class EditorActionConverterTests
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly EditorActionConverter _converter;

    public EditorActionConverterTests()
    {
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _converter = new EditorActionConverter(_keyCodeMapper);
    }

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
        _ = events[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
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
    public void ToAndFromMacroSequence_WhenAdjacentTextInputs_PreservesSeparateTextInputBoundaries()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "hello" },
            new EditorAction { Type = EditorActionType.TextInput, Text = "world" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Text boundary round trip", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.TextInputBoundaries.Should().HaveCount(2);
        _ = restored.Should().HaveCount(2);
        _ = restored.Select(action => action.Type).Should().Equal(EditorActionType.TextInput, EditorActionType.TextInput);
        _ = restored.Select(action => action.Text).Should().Equal("hello", "world");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputHasSingleCharacter_PreservesTextInputAction()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "x" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Single text boundary", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.TextInputBoundaries.Should().ContainSingle();
        _ = restored.Should().ContainSingle();
        _ = restored[0].Type.Should().Be(EditorActionType.TextInput);
        _ = restored[0].Text.Should().Be("x");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputContainsControlCharacters_PreservesMultilineTextInputAction()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "a\r\nb\t\b" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Multiline text boundary", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.Events.Should().HaveCount(10);
        _ = sequence.Events.Select(ev => ev.KeyCode).Should().Equal(
            1_000 + 'a',
            1_000 + 'a',
            InputEventCode.KEY_ENTER,
            InputEventCode.KEY_ENTER,
            1_000 + 'b',
            1_000 + 'b',
            InputEventCode.KEY_TAB,
            InputEventCode.KEY_TAB,
            InputEventCode.KEY_BACKSPACE,
            InputEventCode.KEY_BACKSPACE);
        _ = sequence.TextInputBoundaries.Should().ContainSingle()
            .Which.Should().Be(new TextInputBoundary(0, 10, "a\r\nb\t\b"));
        _ = restored.Should().ContainSingle();
        _ = restored[0].Type.Should().Be(EditorActionType.TextInput);
        _ = restored[0].Text.Should().Be("a\r\nb\t\b");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedTextInputContainsControlCharacters_CompilesAndRestoresMultilineTextInputAction()
    {
        ConfigureTextInputTyping();
        _ = _keyCodeMapper.GetKeyCode("Enter").Returns(InputEventCode.KEY_ENTER);
        _ = _keyCodeMapper.GetKeyCode("Tab").Returns(InputEventCode.KEY_TAB);
        _ = _keyCodeMapper.GetKeyCode("Backspace").Returns(InputEventCode.KEY_BACKSPACE);

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.TextInput, Text = "a\r\nb\t\b" },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var sequence = _converter.ToMacroSequence(actions, "Script multiline text", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.ScriptSteps.Should().Equal(
            "repeat 1 {",
            "type a\r\nb\t\b",
            "}");
        _ = sequence.Events.Should().HaveCount(10);
        _ = sequence.Events.Select(ev => ev.KeyCode).Should().Equal(
            1_000 + 'a',
            1_000 + 'a',
            InputEventCode.KEY_ENTER,
            InputEventCode.KEY_ENTER,
            1_000 + 'b',
            1_000 + 'b',
            InputEventCode.KEY_TAB,
            InputEventCode.KEY_TAB,
            InputEventCode.KEY_BACKSPACE,
            InputEventCode.KEY_BACKSPACE);
        _ = restored.Should().HaveCount(3);
        _ = restored[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = restored[1].Type.Should().Be(EditorActionType.TextInput);
        _ = restored[1].Text.Should().Be("a\r\nb\t\b");
        _ = restored[2].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedTextInputContainsLiteralDollar_CompilesAndRestoresLiteralDollarText()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.SetVariable, Text = "myVar=1" },
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.TextInput, Text = "price $$10 and $myVar" },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var sequence = _converter.ToMacroSequence(actions, "Script dollar text", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.ScriptSteps.Should().Equal(
            "set myVar=1",
            "repeat 1 {",
            "type price $$10 and $myVar",
            "}");
        _ = restored.Should().HaveCount(4);
        _ = restored[2].Type.Should().Be(EditorActionType.TextInput);
        _ = restored[2].Text.Should().Be("price $$10 and $myVar");
    }

    [Fact]
    public async Task SaveAndLoad_WhenScriptBackedTextInputContainsMultilineDollarText_PreservesRestoredTextInputAction()
    {
        ConfigureTextInputTyping();
        _ = _keyCodeMapper.GetKeyCode("Enter").Returns(InputEventCode.KEY_ENTER);
        var fileManager = new MacroFileManager(() => _keyCodeMapper);
        var filePath = Path.Combine(Path.GetTempPath(), $"crossmacro_converter_{Guid.NewGuid():N}.macro");
        const string text = "first line\nprice $$10";

        try
        {
            var sequence = _converter.ToMacroSequence(
                [
                    new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
                    new EditorAction { Type = EditorActionType.TextInput, Text = text },
                    new EditorAction { Type = EditorActionType.BlockEnd },
                ],
                "Script multiline dollar text",
                isAbsolute: true);

            await fileManager.SaveAsync(sequence, filePath);
            var loaded = await fileManager.LoadAsync(filePath);
            var restored = _converter.FromMacroSequence(loaded!);

            _ = loaded.Should().NotBeNull();
            _ = loaded!.ScriptSteps.Should().Equal(
                "repeat 1 {",
                "type first line\nprice $$10",
                "}");
            _ = restored.Should().HaveCount(3);
            _ = restored[1].Type.Should().Be(EditorActionType.TextInput);
            _ = restored[1].Text.Should().Be(text);
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
    public void ToAndFromMacroSequence_WhenTextInputRequiresAltGr_PreservesTextInputAction()
    {
        ConfigureTextInputTyping();
        _ = _keyCodeMapper.GetKeyCodeForCharacter('@').Returns(2_000);
        _ = _keyCodeMapper.RequiresAltGr('@').Returns(returnThis: true);

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "@" },
        };

        var sequence = _converter.ToMacroSequence(actions, "AltGr text boundary", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.TextInputBoundaries.Should().ContainSingle();
        _ = sequence.Events.Select(ev => ev.KeyCode).Should().Equal(
            InputEventCode.KEY_RIGHTALT,
            2_000,
            2_000,
            InputEventCode.KEY_RIGHTALT);
        _ = restored.Should().ContainSingle();
        _ = restored[0].Type.Should().Be(EditorActionType.TextInput);
        _ = restored[0].Text.Should().Be("@");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputsSeparatedByDelay_PreservesDelayBoundary()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "one" },
            new EditorAction { Type = EditorActionType.Delay, DelayMs = 250 },
            new EditorAction { Type = EditorActionType.TextInput, Text = "two" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Text delay boundary", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = restored.Select(action => action.Type).Should().Equal(
            EditorActionType.TextInput,
            EditorActionType.Delay,
            EditorActionType.TextInput);
        _ = restored[0].Text.Should().Be("one");
        _ = restored[1].DelayMs.Should().Be(250);
        _ = restored[2].Text.Should().Be("two");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputsMixedWithMouseActions_PreservesActionShapeAndTextBoundaries()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "one" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, IsAbsolute = false, UseCurrentPosition = true },
            new EditorAction { Type = EditorActionType.TextInput, Text = "two" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Text mouse boundary", isAbsolute: false, skipInitialZeroZero: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = restored.Select(action => action.Type).Should().Equal(
            EditorActionType.TextInput,
            EditorActionType.MouseClick,
            EditorActionType.TextInput);
        _ = restored[0].Text.Should().Be("one");
        _ = restored[1].UseCurrentPosition.Should().BeTrue();
        _ = restored[1].Button.Should().Be(MacroMouseButton.Left);
        _ = restored[2].Text.Should().Be("two");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputContainsLiteralDollar_PreservesTextWithoutScriptExpansion()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "cost $5" },
            new EditorAction { Type = EditorActionType.TextInput, Text = "$HOME" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Text literal dollars", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.ScriptSteps.Should().BeEmpty();
        _ = restored.Select(action => action.Text).Should().Equal("cost $5", "$HOME");
    }

    [Fact]
    public void FromMacroSequence_WhenTextInputBoundaryIsInvalid_FallsBackToRawKeyActions()
    {
        _ = _keyCodeMapper.GetCharacterForKeyCode(30, withShift: false).Returns('a');
        _ = _keyCodeMapper.GetCharacterForKeyCode(48, withShift: false).Returns('b');
        var sequence = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48 },
            },
            TextInputBoundaries = { new TextInputBoundary(0, 99, "invalid") },
        };

        var restored = _converter.FromMacroSequence(sequence);

        _ = restored.Select(action => action.Type).Should().Equal(
            EditorActionType.KeyPress,
            EditorActionType.KeyPress);
        _ = restored.Select(action => action.KeyCode).Should().Equal(30, 48);
    }

    [Fact]
    public void FromMacroSequence_WhenTextInputBoundaryTextDoesNotMatchEvents_FallsBackToRawKeyActions()
    {
        _ = _keyCodeMapper.GetCharacterForKeyCode(30, withShift: false).Returns('a');
        _ = _keyCodeMapper.GetCharacterForKeyCode(48, withShift: false).Returns('b');
        var sequence = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48 },
            },
            TextInputBoundaries = { new TextInputBoundary(0, 4, "stale") },
        };

        var restored = _converter.FromMacroSequence(sequence);

        _ = restored.Select(action => action.Type).Should().Equal(
            EditorActionType.KeyPress,
            EditorActionType.KeyPress);
        _ = restored.Select(action => action.KeyCode).Should().Equal(30, 48);
    }

    [Fact]
    public void ToMacroSequence_WhenBoundaryRestoredTextInputIsUnedited_PreservesOriginalKeyTiming()
    {
        ConfigureTextInputTyping();

        var sequence = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 1_000 + 'a', DelayMs = 25 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 1_000 + 'a', DelayMs = 7 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 1_000 + 'b', DelayMs = 93 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 1_000 + 'b', DelayMs = 11 },
            },
            TextInputBoundaries = { new TextInputBoundary(0, 4, "ab") },
        };

        var restored = _converter.FromMacroSequence(sequence);
        var roundTripped = _converter.ToMacroSequence(restored, "Preserve timed text", isAbsolute: true);

        _ = restored.Select(action => action.Type).Should().Equal(EditorActionType.Delay, EditorActionType.TextInput);
        _ = restored[0].DelayMs.Should().Be(25);
        _ = restored[1].Text.Should().Be("ab");
        _ = roundTripped.Events.Should().HaveCount(4);
        _ = roundTripped.Events.Select(ev => ev.DelayMs).Should().Equal(25, 7, 93, 11);
        _ = roundTripped.Events.Select(ev => ev.Type).Should().Equal(
            EventType.KeyPress,
            EventType.KeyRelease,
            EventType.KeyPress,
            EventType.KeyRelease);
        _ = roundTripped.TextInputBoundaries.Should().ContainSingle()
            .Which.Should().Be(new TextInputBoundary(0, 4, "ab"));
    }

    [Fact]
    public void ToMacroSequence_WhenBoundaryRestoredTextInputIsEdited_RegeneratesSyntheticTypingEvents()
    {
        ConfigureTextInputTyping();

        var sequence = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 1_000 + 'a', DelayMs = 25 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 1_000 + 'a', DelayMs = 7 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 1_000 + 'b', DelayMs = 93 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 1_000 + 'b', DelayMs = 11 },
            },
            TextInputBoundaries = { new TextInputBoundary(0, 4, "ab") },
        };

        var restored = _converter.FromMacroSequence(sequence);
        _ = restored.Select(action => action.Type).Should().Equal(EditorActionType.Delay, EditorActionType.TextInput);
        restored[1].Text = "ac";

        var roundTripped = _converter.ToMacroSequence(restored, "Regenerate edited text", isAbsolute: true);

        _ = roundTripped.Events.Should().HaveCount(4);
        _ = roundTripped.Events.Select(ev => ev.DelayMs).Should().Equal(25, 0, 10, 0);
        _ = roundTripped.TextInputBoundaries.Should().ContainSingle()
            .Which.Should().Be(new TextInputBoundary(0, 4, "ac"));
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
        _ = sequence.Events[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = sequence.Events[2].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = sequence.Events[2].Type.Should().Be(EventType.Click);
        _ = sequence.Events[3].UseCurrentPosition.Should().BeTrue();
        _ = sequence.Events[3].CoordinateMode.Should().BeNull();
        _ = sequence.Events[4].Button.Should().Be(MacroMouseButton.ScrollDown);
        _ = sequence.Events[4].CoordinateMode.Should().BeNull();

        _ = restored.Should().HaveCount(5);
        _ = restored[0].IsAbsolute.Should().BeTrue();
        _ = restored[1].IsAbsolute.Should().BeFalse();
        _ = restored[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
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
                MouseCoordinateSpace.LogicalDesktop,
                null);
            _ = saved.Should().Contain("M,rel-logical,5,-3");
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
                MouseCoordinateSpace.LogicalDesktop,
                null);
            _ = restored.Should().HaveCount(3);
            _ = restored[0].IsAbsolute.Should().BeTrue();
            _ = restored[1].IsAbsolute.Should().BeFalse();
            _ = restored[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
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
    public void ToAndFromMacroSequence_WhenScriptBackedActionUsesRawDeviceSpace_PreservesIt()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = false,
                CoordinateSpace = MouseCoordinateSpace.RawDevice,
                X = 7,
                Y = -4,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var sequence = _converter.ToMacroSequence(actions, "Raw relative script", isAbsolute: false);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.ScriptSteps.Should().Equal("repeat 1 {", "move rel-raw 7 -4", "}");
        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = sequence.Events[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = restored.Should().HaveCount(3);
        _ = restored[1].IsAbsolute.Should().BeFalse();
        _ = restored[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptStepIfElse_CompilesBranch()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.SetVariable, Text = "mode=fast" },
            new EditorAction { Type = EditorActionType.IfBlockStart, Text = "$mode == fast" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
            new EditorAction { Type = EditorActionType.ElseBlockStart },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].Button.Should().Be(MacroMouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenOnlyStateScriptActions_ProducesRuntimeOnlyScriptMacro()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "State Only Script", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().BeEmpty();
        _ = sequence.ScriptSteps.Should().ContainSingle().Which.Should().StartWith("set i");
    }

    [Fact]
    public void ToMacroSequence_WhenClipboardActionsUsed_PreservesClipboardScriptSteps()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.ClipboardGet, ScriptVariableName = "clipText" },
            new EditorAction { Type = EditorActionType.ClipboardSet, Text = "hello $clipText" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Clipboard Macro", isAbsolute: true);

        _ = sequence.Events.Should().BeEmpty();
        _ = sequence.ScriptSteps.Should().Equal(
            "clipboard get clipText",
            "clipboard set hello $clipText");
    }

    [Fact]
    public void ToMacroSequence_WhenClipboardSetUsesEscapedDollar_PreservesLiteralDollarEscape()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.ClipboardSet, Text = "literal $$clipText" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Clipboard Macro", isAbsolute: true);

        _ = sequence.Events.Should().BeEmpty();
        _ = sequence.ScriptSteps.Should().Equal("clipboard set literal $$clipText");
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenClipboardStepsPresent_RestoresStructuredActions()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "clipboard get clipText",
                "clipboard set hello $clipText",
            },
        };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().BeEmpty();
        _ = result.Actions.Should().HaveCount(2);
        _ = result.Actions[0].Type.Should().Be(EditorActionType.ClipboardGet);
        _ = result.Actions[0].ScriptVariableName.Should().Be("clipText");
        _ = result.Actions[1].Type.Should().Be(EditorActionType.ClipboardSet);
        _ = result.Actions[1].Text.Should().Be("hello $clipText");
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenClipboardSetUsesEscapedDollar_PreservesLiteralDollarEscape()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps = { "clipboard set literal $$clipText" },
        };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().BeEmpty();
        _ = result.Actions.Should().ContainSingle();
        _ = result.Actions[0].Type.Should().Be(EditorActionType.ClipboardSet);
        _ = result.Actions[0].Text.Should().Be("literal $$clipText");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptAndRegularActionsMixed_UsesUnifiedCompiler()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 120, Y = 220 },
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "2" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Mixed Macro", isAbsolute: true);

        // Assert
        _ = sequence.IsAbsoluteCoordinates.Should().BeTrue();
        _ = sequence.Events.Should().HaveCount(3);
        _ = sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[1].Type.Should().Be(EventType.Click);
        _ = sequence.Events[2].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenStateScriptAndMixedCoordinates_UsesStandardConversionAndPreservesScriptSteps()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "mode",
                ScriptValueType = ScriptValueType.Text,
                ScriptValue = "fast",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                X = 320,
                Y = 240,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "State Script Mixed Coordinates", isAbsolute: true);

        // Assert
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].UseCurrentPosition.Should().BeTrue();
        _ = sequence.Events[0].CoordinateMode.Should().BeNull();
        _ = sequence.Events[1].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[1].X.Should().Be(320);
        _ = sequence.Events[1].Y.Should().Be(240);
        _ = sequence.Events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = sequence.ScriptSteps.Should().Equal(
            "set mode fast",
            "click current left",
            "move abs 320 240");
    }

    [Fact]
    public void ToMacroSequence_WhenAbsoluteMovePrecedesCurrentPositionClickInScriptBlock_PreservesCurrentPositionSemantics()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                X = 500,
                Y = 300,
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Absolute Then Current Click", isAbsolute: true);

        // Assert
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[0].X.Should().Be(500);
        _ = sequence.Events[0].Y.Should().Be(300);
        _ = sequence.Events[1].Type.Should().Be(EventType.Click);
        _ = sequence.Events[1].UseCurrentPosition.Should().BeTrue();
        _ = sequence.ScriptSteps.Should().Equal(
            "repeat 1 {",
            "move abs 500 300",
            "click current left",
            "}");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptCompilationFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.WhileBlockStart, Text = "$i < 2" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        Action act = () => _converter.ToMacroSequence(actions, "Broken Script", isAbsolute: false);

        // Assert
        _ = act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredScriptActionsUsed_CompilesSuccessfully()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "mode",
                ScriptValueType = ScriptValueType.Text,
                ScriptValue = "fast",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "mode",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Text,
                ScriptRightOperand = "fast",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured Script Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].Button.Should().Be(MacroMouseButton.Left);
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
    public void ToMacroSequence_WhenConditionTextOperandsStartWithDollar_EscapesLiteralDollar()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.Text,
                ScriptLeftOperand = "$foo",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Text,
                ScriptRightOperand = "$foo",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Condition Dollar Text", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "if $$foo == $$foo {",
            "click current left",
            "}");
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredConditionUsesVariableReferenceWithDollarPrefix_NormalizesOnlyVariableSide()
    {
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
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "$name",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Text,
                ScriptRightOperand = "$foo",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var sequence = _converter.ToMacroSequence(actions, "Condition Variable Prefix", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal(
            "set name $$foo",
            "if $name == $$foo {",
            "click current left",
            "}");
        _ = sequence.Events.Should().ContainSingle();
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredConditionUsesColorOperand_EmitsUppercaseBareHex()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.PixelColor,
                IsAbsolute = true,
                ScreenX = 1,
                ScreenY = 2,
                ScreenColorVariableName = "color",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "color",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Color,
                ScriptRightOperand = "1c1c1c",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var sequence = _converter.ToMacroSequence(actions, "Condition Color", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal(
            "pixelcolor 1 2 color timeout 5000",
            "if $color == 1C1C1C {",
            "click current left",
            "}");
    }

    [Fact]
    public void ToMacroSequence_WhenLegacyScriptTextExistsAndStructuredFieldsAreEdited_PrefersStructuredSerialization()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                Text = "broken_set_payload",
                ScriptVariableName = "counter",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "0",
            },
            new EditorAction
            {
                Type = EditorActionType.IncrementVariable,
                Text = "broken_inc_payload",
                ScriptVariableName = "counter",
                ScriptNumericSourceType = ScriptNumericSourceType.Number,
                ScriptNumericValue = "2",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                Text = "broken_condition_payload",
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "counter",
                ScriptConditionOperator = ScriptConditionOperator.GreaterThanOrEqual,
                ScriptRightOperandType = ScriptOperandType.Number,
                ScriptRightOperand = "2",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                Text = "broken_for_payload",
                ForVariableName = "j",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "2",
                ForHasStep = true,
                ForStepType = ScriptNumericSourceType.Number,
                ForStepValue = "1",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured Overrides Legacy Text", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "set counter 0",
            "inc counter 2",
            "if $counter >= 2 {",
            "click current left",
            "}",
            "for j from 1 to 2 step 1 {",
            "click current left",
            "}");
        _ = sequence.Events.Should().HaveCount(3);
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
    public void ToMacroSequence_WhenStructuredForBlockUsed_RepeatsExpectedCount()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured For Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(3);
        _ = sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenForEndAndStepShareVariable_CompilesAndNormalizesVariableToken()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "limit",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "3",
            },
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "0",
                ForEndType = ScriptNumericSourceType.VariableReference,
                ForEndValue = "$limit",
                ForHasStep = true,
                ForStepType = ScriptNumericSourceType.VariableReference,
                ForStepValue = "$limit",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Shared For Variable", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Contain("for i from 0 to $limit step $limit {");
        _ = sequence.Events.Should().HaveCount(2); // i = 0, 3
        _ = sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenBreakUsedInsideLoop_StopsLoopExecution()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Break },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Break Loop Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].Button.Should().Be(MacroMouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenContinueUsedInsideLoop_SkipsRemainingBodySteps()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Continue },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Continue Loop Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(3);
        _ = sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click && ev.Button == MacroMouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenBreakUsedOutsideLoop_ThrowsInvalidOperationException()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.Break },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
        };

        // Act
        Action act = () => _converter.ToMacroSequence(actions, "Invalid Break Macro", isAbsolute: false);

        // Assert
        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*can only be used inside repeat/while/for blocks*");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedConversionUsed_UsesSkipInitialZeroZeroDefault()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
            new EditorAction
            {
                Type = EditorActionType.KeyPress,
                KeyCode = 30,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Skip Initial Propagation", isAbsolute: false, skipInitialZeroZero: false);

        // Assert
        _ = sequence.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void ToMacroSequence_WhenScriptActionsUsed_PreservesSourceScriptSteps()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "0",
            },
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Step Preserve", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "set i 0",
            "for i from 1 to 3 {",
            "click current left",
            "}");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedContainsRandomDelay_PreservesRandomDelayMetadata()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 10, RandomDelayMaxMs = 20 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Random Delay", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[1].HasRandomDelay.Should().BeTrue();
        _ = sequence.Events[1].RandomDelayMinMs.Should().Be(10);
        _ = sequence.Events[1].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedHasInitialRandomDelay_PreservesFirstEventRandomDelay()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 10, RandomDelayMaxMs = 20 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Initial Random Delay", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].HasRandomDelay.Should().BeTrue();
        _ = sequence.Events[0].RandomDelayMinMs.Should().Be(10);
        _ = sequence.Events[0].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedModifierOnlyKeyPress_DoesNotFail()
    {
        // Arrange
        _ = _keyCodeMapper.IsModifierKeyCode(29).Returns(returnThis: true);
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
            new EditorAction
            {
                Type = EditorActionType.KeyPress,
                KeyCode = 29,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Modifier KeyPress", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.KeyPress);
        _ = sequence.Events[0].KeyCode.Should().Be(29);
        _ = sequence.Events[1].Type.Should().Be(EventType.KeyRelease);
        _ = sequence.Events[1].KeyCode.Should().Be(29);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsPresent_RestoresStructuredScriptActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set i 0",
                "for i from 1 to 10 {",
                "click left",
                "}",
                "repeat $n {",
                "tap 30",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(7);

        _ = actions[0].Type.Should().Be(EditorActionType.SetVariable);
        _ = actions[0].ScriptVariableName.Should().Be("i");
        _ = actions[0].ScriptValueType.Should().Be(ScriptValueType.Number);
        _ = actions[0].ScriptValue.Should().Be("0");

        _ = actions[1].Type.Should().Be(EditorActionType.ForBlockStart);
        _ = actions[1].ForVariableName.Should().Be("i");
        _ = actions[1].ForStartType.Should().Be(ScriptNumericSourceType.Number);
        _ = actions[1].ForStartValue.Should().Be("1");
        _ = actions[1].ForEndType.Should().Be(ScriptNumericSourceType.Number);
        _ = actions[1].ForEndValue.Should().Be("10");

        _ = actions[2].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[2].UseCurrentPosition.Should().BeTrue();

        _ = actions[3].Type.Should().Be(EditorActionType.BlockEnd);

        _ = actions[4].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[4].ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = actions[4].ScriptNumericValue.Should().Be("n");

        _ = actions[5].Type.Should().Be(EditorActionType.KeyPress);
        _ = actions[5].KeyCode.Should().Be(30);

        _ = actions[6].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainNamedKeyDownUp_RestoresKeyActions()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyCode("ctrl").Returns(29);
        _ = _keyCodeMapper.GetKeyName(29).Returns("Ctrl");
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "key down ctrl",
                "key up ctrl",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.KeyDown);
        _ = actions[0].KeyCode.Should().Be(29);
        _ = actions[0].KeyName.Should().Be("Ctrl");
        _ = actions[1].Type.Should().Be(EditorActionType.KeyUp);
        _ = actions[1].KeyCode.Should().Be(29);
        _ = actions[1].KeyName.Should().Be("Ctrl");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepContainsNamedSingleTap_RestoresKeyPress()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyCode("enter").Returns(28);
        _ = _keyCodeMapper.GetKeyName(28).Returns("Enter");
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "tap enter",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(1);
        _ = actions[0].Type.Should().Be(EditorActionType.KeyPress);
        _ = actions[0].KeyCode.Should().Be(28);
        _ = actions[0].KeyName.Should().Be("Enter");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepContainsScrollWithoutCount_RestoresSingleScrollAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "scroll up",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(1);
        _ = actions[0].Type.Should().Be(EditorActionType.ScrollVertical);
        _ = actions[0].ScrollAmount.Should().Be(1);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainRandomDelayRange_RestoresDelayAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "repeat 2 {",
                "delay random 10..20",
                "click left",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(4);
        _ = actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[1].Type.Should().Be(EditorActionType.Delay);
        _ = actions[1].UseRandomDelay.Should().BeTrue();
        _ = actions[1].RandomDelayMinMs.Should().Be(10);
        _ = actions[1].RandomDelayMaxMs.Should().Be(20);
        _ = actions[2].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[3].Type.Should().Be(EditorActionType.BlockEnd);
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
    public void FromMacroSequence_WhenConditionStepUsesEscapedDollar_RestoresTextLiterals()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $$foo == $$bar {",
                "click left",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(3);
        _ = actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.Text);
        _ = actions[0].ScriptLeftOperand.Should().Be("$foo");
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Text);
        _ = actions[0].ScriptRightOperand.Should().Be("$bar");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsUseMoveAliasAbsolute_RestoresStructuredMoveAndClick()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move absolute 200 300",
                "click l",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[0].IsAbsolute.Should().BeTrue();
        _ = actions[0].X.Should().Be(200);
        _ = actions[0].Y.Should().Be(300);
        _ = actions[1].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[1].IsAbsolute.Should().BeTrue();
        _ = actions[1].X.Should().Be(200);
        _ = actions[1].Y.Should().Be(300);
        _ = actions[1].Button.Should().Be(MacroMouseButton.Left);
        _ = actions[1].UseCurrentPosition.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenScriptContainsExplicitMoveClickPairs_RoundTripsMoveEvents()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move abs 10 10",
                "click left",
                "move abs 20 20",
                "click left",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "RoundTrip Move Click Pairs", isAbsolute: true);

        // Assert
        _ = actions.Should().HaveCount(4);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[1].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[2].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[3].Type.Should().Be(EditorActionType.MouseClick);

        _ = saved.Events.Should().HaveCount(4);
        _ = saved.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = saved.Events[0].X.Should().Be(10);
        _ = saved.Events[0].Y.Should().Be(10);
        _ = saved.Events[1].Type.Should().Be(EventType.Click);
        _ = saved.Events[1].X.Should().Be(10);
        _ = saved.Events[1].Y.Should().Be(10);
        _ = saved.Events[2].Type.Should().Be(EventType.MouseMove);
        _ = saved.Events[2].X.Should().Be(20);
        _ = saved.Events[2].Y.Should().Be(20);
        _ = saved.Events[3].Type.Should().Be(EventType.Click);
        _ = saved.Events[3].X.Should().Be(20);
        _ = saved.Events[3].Y.Should().Be(20);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsUseMixedMoveModes_RoundTripsEventCoordinateModes()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move abs 200 300",
                "click left",
                "move rel-logical 5 -4",
                "click right",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "Mixed Script Modes", isAbsolute: false);

        // Assert
        _ = actions.Should().HaveCount(4);
        _ = actions[0].IsAbsolute.Should().BeTrue();
        _ = actions[1].IsAbsolute.Should().BeTrue();
        _ = actions[1].UseCurrentPosition.Should().BeFalse();
        _ = actions[2].IsAbsolute.Should().BeFalse();
        _ = actions[2].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = actions[3].IsAbsolute.Should().BeFalse();
        _ = actions[3].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = actions[3].UseCurrentPosition.Should().BeFalse();

        _ = saved.Events.Should().HaveCount(4);
        _ = saved.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = saved.Events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = saved.Events[2].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = saved.Events[3].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedAndMoveImmediatelyPrecedesAbsoluteClick_DoesNotDuplicateMoveStep()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 200, Y = 300 },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                IsAbsolute = true,
                X = 200,
                Y = 300,
                UseCurrentPosition = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Backed No Duplicate Move", isAbsolute: true);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "repeat 1 {",
            "move abs 200 300",
            "click left",
            "}");
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[1].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainCurrentPositionDownUp_PreservesUseCurrentPosition()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "down left",
                "up left",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "DownUpCurrentPosition", isAbsolute: false);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseDown);
        _ = actions[0].UseCurrentPosition.Should().BeTrue();
        _ = actions[0].IsAbsolute.Should().BeFalse();
        _ = actions[1].Type.Should().Be(EditorActionType.MouseUp);
        _ = actions[1].UseCurrentPosition.Should().BeTrue();
        _ = actions[1].IsAbsolute.Should().BeFalse();

        _ = saved.Events.Should().HaveCount(2);
        _ = saved.Events[0].Type.Should().Be(EventType.ButtonPress);
        _ = saved.Events[0].UseCurrentPosition.Should().BeTrue();
        _ = saved.Events[1].Type.Should().Be(EventType.ButtonRelease);
        _ = saved.Events[1].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainAbsoluteMoveThenCurrentPositionClick_PreservesSeparateCurrentPositionAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move abs 120 240",
                "click current left",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[0].IsAbsolute.Should().BeTrue();
        _ = actions[0].X.Should().Be(120);
        _ = actions[0].Y.Should().Be(240);
        _ = actions[1].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[1].UseCurrentPosition.Should().BeTrue();
        _ = actions[1].IsAbsolute.Should().BeFalse();
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
    public void FromMacroSequence_WhenScriptStepsContainBreakAndContinue_RestoresLoopControlActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "repeat 1 {",
                "break",
                "}",
                "repeat 1 {",
                "continue",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(6);
        _ = actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[1].Type.Should().Be(EditorActionType.Break);
        _ = actions[2].Type.Should().Be(EditorActionType.BlockEnd);
        _ = actions[3].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[4].Type.Should().Be(EditorActionType.Continue);
        _ = actions[5].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenScriptStepIsUnsupported_RestoresRawActionAndWarning()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set i 0",
                "tap ctrl+c",
                "click left",
            },
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().HaveCount(1);
        _ = result.Warnings[0].StepIndex.Should().Be(2);
        _ = result.Warnings[0].Step.Should().Be("tap ctrl+c");
        _ = result.Actions.Should().HaveCount(3);
        _ = result.Actions[1].Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Actions[1].Text.Should().Be("tap ctrl+c");
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenScreenReadingStepsPresent_RestoresStructuredActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelcolor 10 20 color",
                "pixelcolor rel -1 2 relativeColor",
                "waitcolor 11 22 00FFAA 2500 wait_ok",
                "pixelsearch 0 0 3 3 123456 found x y tolerance 5",
            },
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().BeEmpty();
        _ = result.Actions.Should().HaveCount(4);
        _ = result.Actions[0].Type.Should().Be(EditorActionType.PixelColor);
        _ = result.Actions[0].ScreenX.Should().Be(10);
        _ = result.Actions[0].ScreenY.Should().Be(20);
        _ = result.Actions[0].ScreenColorVariableName.Should().Be("color");
        _ = result.Actions[1].Type.Should().Be(EditorActionType.PixelColor);
        _ = result.Actions[1].IsAbsolute.Should().BeFalse();
        _ = result.Actions[1].ScreenX.Should().Be(-1);
        _ = result.Actions[1].ScreenY.Should().Be(2);
        _ = result.Actions[1].ScreenColorVariableName.Should().Be("relativeColor");
        _ = result.Actions[2].Type.Should().Be(EditorActionType.WaitColor);
        _ = result.Actions[2].ScreenX.Should().Be(11);
        _ = result.Actions[2].ScreenY.Should().Be(22);
        _ = result.Actions[2].ScreenColorHex.Should().Be("00FFAA");
        _ = result.Actions[2].ScreenTimeoutMs.Should().Be(2500);
        _ = result.Actions[2].ScreenColorVariableName.Should().Be("wait_ok");
        _ = result.Actions[3].Type.Should().Be(EditorActionType.PixelSearch);
        _ = result.Actions[3].ScreenLeft.Should().Be(0);
        _ = result.Actions[3].ScreenTop.Should().Be(0);
        _ = result.Actions[3].ScreenWidth.Should().Be(3);
        _ = result.Actions[3].ScreenHeight.Should().Be(3);
        _ = result.Actions[3].ScreenColorHex.Should().Be("123456");
        _ = result.Actions[3].ScreenFoundVariableName.Should().Be("found");
        _ = result.Actions[3].ScreenFoundXVariableName.Should().Be("x");
        _ = result.Actions[3].ScreenFoundYVariableName.Should().Be("y");
        _ = result.Actions[3].ScreenTolerance.Should().Be(5);
    }

    [Fact]
    public void ToMacroSequence_WhenScreenReadingActionsPresent_SerializesStructuredPayloads()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.PixelColor,
                IsAbsolute = true,
                ScreenX = 10,
                ScreenY = 20,
                ScreenTimeoutMs = 1200,
                ScreenColorVariableName = "color",
            },
            new EditorAction
            {
                Type = EditorActionType.PixelColor,
                IsAbsolute = false,
                ScreenX = -1,
                ScreenY = 2,
                ScreenTimeoutMs = 1300,
                ScreenColorVariableName = "relativeColor",
            },
            new EditorAction
            {
                Type = EditorActionType.WaitColor,
                ScreenX = 11,
                ScreenY = 22,
                ScreenColorHex = "00ffaa",
                ScreenTimeoutMs = 2500,
                ScreenColorVariableName = "wait_ok",
            },
            new EditorAction
            {
                Type = EditorActionType.PixelSearch,
                ScreenLeft = 0,
                ScreenTop = 0,
                ScreenWidth = 3,
                ScreenHeight = 3,
                ScreenColorHex = "123456",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "x",
                ScreenFoundYVariableName = "y",
                ScreenTimeoutMs = 1400,
                ScreenTolerance = 5,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Screen Reading", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "pixelcolor 10 20 color timeout 1200",
            "pixelcolor rel -1 2 relativeColor timeout 1300",
            "waitcolor 11 22 00FFAA 2500 wait_ok",
            "pixelsearch 0 0 3 3 123456 found x y timeout 1400 tolerance 5");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenImageSearchActionPresent_PreservesStructuredPayload()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ImageSearch,
                ScreenLeft = 10,
                ScreenTop = 20,
                ScreenWidth = 30,
                ScreenHeight = 40,
                ImageAssetName = "Target_1",
                ScreenFoundVariableName = "foundTarget",
                ScreenFoundXVariableName = "targetX",
                ScreenFoundYVariableName = "targetY",
                ScreenTimeoutMs = 1500,
                ImageSearchSimilarity = 0.875,
                ImageSearchDownsample = 2,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Image Search", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "imagesearch 10 20 40 60 Target_1 foundTarget targetX targetY timeout 1500 similarity 0.875 downsample 2");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.RestoredFromScriptSteps.Should().BeTrue();
        _ = restored.Warnings.Should().BeEmpty();
        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].Type.Should().Be(EditorActionType.ImageSearch);
        _ = restored.Actions[0].ScreenLeft.Should().Be(10);
        _ = restored.Actions[0].ScreenTop.Should().Be(20);
        _ = restored.Actions[0].ScreenWidth.Should().Be(30);
        _ = restored.Actions[0].ScreenHeight.Should().Be(40);
        _ = restored.Actions[0].ImageAssetName.Should().Be("Target_1");
        _ = restored.Actions[0].ScreenFoundVariableName.Should().Be("foundTarget");
        _ = restored.Actions[0].ScreenFoundXVariableName.Should().Be("targetX");
        _ = restored.Actions[0].ScreenFoundYVariableName.Should().Be("targetY");
        _ = restored.Actions[0].ScreenTimeoutMs.Should().Be(1500);
        _ = restored.Actions[0].ImageSearchSimilarity.Should().Be(0.875);
        _ = restored.Actions[0].ImageSearchDownsample.Should().Be(2);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenScaleAwareIsExplicit_PreservesScaleAwareOption()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ImageSearch,
                ImageAssetName = "Target_1",
                ImageSearchScaleAware = true,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Image Search", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().ContainSingle()
            .Which.Should().EndWith("scaleaware");
        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].ImageSearchScaleAware.Should().BeTrue();
    }

    [Theory]
    [InlineData(MacroMouseButton.Left, "left")]
    [InlineData(MacroMouseButton.Right, "right")]
    [InlineData(MacroMouseButton.Middle, "middle")]
    public void ToAndFromMacroSequence_WhenImageClickActionPresent_PreservesStructuredPayload(MacroMouseButton button, string buttonToken)
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ImageClick,
                Button = button,
                ScreenLeft = 10,
                ScreenTop = 20,
                ScreenWidth = 30,
                ScreenHeight = 40,
                ImageAssetName = "ButtonAsset",
                ScreenFoundVariableName = "clicked",
                ScreenFoundXVariableName = "clickX",
                ScreenFoundYVariableName = "clickY",
                ScreenTimeoutMs = 1600,
                ImageSearchSimilarity = 0.75,
                ImageSearchDownsample = 3,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Image Click", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            $"imageclick 10 20 40 60 ButtonAsset clicked clickX clickY button {buttonToken} timeout 1600 similarity 0.75 downsample 3");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.RestoredFromScriptSteps.Should().BeTrue();
        _ = restored.Warnings.Should().BeEmpty();
        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].Type.Should().Be(EditorActionType.ImageClick);
        _ = restored.Actions[0].ScreenLeft.Should().Be(10);
        _ = restored.Actions[0].ScreenTop.Should().Be(20);
        _ = restored.Actions[0].ScreenWidth.Should().Be(30);
        _ = restored.Actions[0].ScreenHeight.Should().Be(40);
        _ = restored.Actions[0].ImageAssetName.Should().Be("ButtonAsset");
        _ = restored.Actions[0].ScreenFoundVariableName.Should().Be("clicked");
        _ = restored.Actions[0].ScreenFoundXVariableName.Should().Be("clickX");
        _ = restored.Actions[0].ScreenFoundYVariableName.Should().Be("clickY");
        _ = restored.Actions[0].Button.Should().Be(button);
        _ = restored.Actions[0].ScreenTimeoutMs.Should().Be(1600);
        _ = restored.Actions[0].ImageSearchSimilarity.Should().Be(0.75);
        _ = restored.Actions[0].ImageSearchDownsample.Should().Be(3);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenWaitImageActionPresent_PreservesStructuredPayload()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.WaitImage,
                ScreenLeft = 1,
                ScreenTop = 2,
                ScreenWidth = 3,
                ScreenHeight = 4,
                ImageAssetName = "DialogAsset",
                ScreenFoundVariableName = "dialogFound",
                ScreenFoundXVariableName = "dialogX",
                ScreenFoundYVariableName = "dialogY",
                ScreenTimeoutMs = 2500,
                ImageSearchSimilarity = 0.625,
                ImageSearchDownsample = 2,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Wait Image", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "waitimage 1 2 4 6 DialogAsset dialogFound dialogX dialogY timeout 2500 similarity 0.625 downsample 2");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.RestoredFromScriptSteps.Should().BeTrue();
        _ = restored.Warnings.Should().BeEmpty();
        _ = restored.Actions.Should().ContainSingle();
        _ = restored.Actions[0].Type.Should().Be(EditorActionType.WaitImage);
        _ = restored.Actions[0].ScreenLeft.Should().Be(1);
        _ = restored.Actions[0].ScreenTop.Should().Be(2);
        _ = restored.Actions[0].ScreenWidth.Should().Be(3);
        _ = restored.Actions[0].ScreenHeight.Should().Be(4);
        _ = restored.Actions[0].ImageAssetName.Should().Be("DialogAsset");
        _ = restored.Actions[0].ScreenFoundVariableName.Should().Be("dialogFound");
        _ = restored.Actions[0].ScreenFoundXVariableName.Should().Be("dialogX");
        _ = restored.Actions[0].ScreenFoundYVariableName.Should().Be("dialogY");
        _ = restored.Actions[0].ScreenTimeoutMs.Should().Be(2500);
        _ = restored.Actions[0].ImageSearchSimilarity.Should().Be(0.625);
        _ = restored.Actions[0].ImageSearchDownsample.Should().Be(2);
    }

    [Theory]
    [InlineData("imagesearch TargetImage similarity NaN")]
    [InlineData("imagesearch TargetImage similarity Infinity")]
    [InlineData("imagesearch TargetImage similarity -Infinity")]
    public void FromMacroSequence_WhenImageSearchSimilarityIsNotFinite_RestoresRawScriptStep(string step)
    {
        var sequence = new MacroSequence { ScriptSteps = { step } };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().ContainSingle();
        _ = result.Actions.Should().ContainSingle().Which.Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Actions[0].Text.Should().Be(step);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenScreenReadingActionsUseVariableTargetColors_PreservesVariableTargetColorMetadata()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.WaitColor,
                ScreenX = 11,
                ScreenY = 22,
                ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable,
                ScreenTargetColorVariableName = "sampled",
                ScreenTimeoutMs = 2500,
                ScreenColorVariableName = "wait_ok",
            },
            new EditorAction
            {
                Type = EditorActionType.PixelSearch,
                ScreenLeft = 0,
                ScreenTop = 0,
                ScreenWidth = 3,
                ScreenHeight = 3,
                ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable,
                ScreenTargetColorVariableName = "sampled",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "x",
                ScreenFoundYVariableName = "y",
                ScreenTolerance = 5,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Screen Reading Variables", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "waitcolor 11 22 $sampled 2500 wait_ok",
            "pixelsearch 0 0 3 3 $sampled found x y timeout 5000 tolerance 5");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restored.RestoredFromScriptSteps.Should().BeTrue();
        _ = restored.Warnings.Should().BeEmpty();
        _ = restored.Actions.Should().HaveCount(2);

        AssertScreenTargetColor(restored.Actions[0], EditorActionType.WaitColor, "sampled");
        AssertScreenTargetColor(restored.Actions[1], EditorActionType.PixelSearch, "sampled");
    }

    [Theory]
    [InlineData("pixelcolor 10 20 timeout")]
    [InlineData("pixelcolor rel 1 2 timeout")]
    [InlineData("waitcolor 11 22 00FFAA")]
    [InlineData("pixelsearch 0 0 3 3 123456 timeout")]
    public void FromMacroSequenceWithDiagnostics_WhenScreenReadingCompilerOnlyShapePresent_RestoresRawActionAndWarning(string step)
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps = { step },
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().ContainSingle();
        _ = result.Warnings[0].Step.Should().Be(step);
        _ = result.Actions.Should().ContainSingle();
        _ = result.Actions[0].Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Actions[0].Text.Should().Be(step);
    }

    [Fact]
    public void ToMacroSequence_WhenRawScriptStepPresent_PreservesRawStepAndCompiles()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyCode("ctrl").Returns(29);
        _ = _keyCodeMapper.GetKeyCode("c").Returns(46);
        _ = _keyCodeMapper.IsModifierKeyCode(29).Returns(returnThis: true);
        _ = _keyCodeMapper.IsModifierKeyCode(46).Returns(returnThis: false);

        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.RawScriptStep,
                Text = "tap ctrl+c",
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Raw Step", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal("tap ctrl+c");
        _ = sequence.Events.Should().HaveCount(4);
        _ = sequence.Events[0].Type.Should().Be(EventType.KeyPress);
        _ = sequence.Events[3].Type.Should().Be(EventType.KeyRelease);
    }

    [Fact]
    public void FromMacroSequence_WhenConditionContainsComparatorText_ParsesUsingEqualityOperator()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $mode == a>=b {",
                "click left",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(3);
        _ = actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[0].ScriptConditionOperator.Should().Be(ScriptConditionOperator.Equals);
        _ = actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[0].ScriptLeftOperand.Should().Be("mode");
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Text);
        _ = actions[0].ScriptRightOperand.Should().Be("a>=b");
    }

    [Fact]
    public void FromMacroSequence_WhenConditionUsesBareHexColor_LoadsColorOperand()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $color == 1c1c1c {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(3);
        _ = actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[0].ScriptLeftOperand.Should().Be("color");
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Color);
        _ = actions[0].ScriptRightOperand.Should().Be("1C1C1C");
    }

    [Fact]
    public void FromMacroSequence_WhenConditionUsesNumericOnlyBareHexColor_LoadsColorOperand()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $color == 000000 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(3);
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Color);
        _ = actions[0].ScriptRightOperand.Should().Be("000000");
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

    [Theory]
    [InlineData(ShellCommandMode.Shell, "shell \"echo ok\" 1 20 300")]
    [InlineData(ShellCommandMode.ShellCapture, "shell capture \"echo ok\" exitCode stdout _ 1 20 300")]
    [InlineData(ShellCommandMode.ShellInput, "shell input \"hello\" \"echo ok\" 1 20 300")]
    [InlineData(ShellCommandMode.ShellCaptureInput, "shell capture-input \"hello\" \"echo ok\" exitCode stdout _ 1 20 300")]
    public void ToMacroSequence_ForShellCommandModes_SerializesExistingShellSyntax(ShellCommandMode mode, string expectedStep)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = mode,
            ShellCommand = "echo ok",
            ShellStandardInput = "hello",
            ShellExitCodeVariableName = "exitCode",
            ShellStandardOutputVariableName = "stdout",
            ShellStandardErrorVariableName = "_",
            ShellRetries = 1,
            ShellBackoffMs = 20,
            ShellTimeoutMs = 300,
        };

        var sequence = _converter.ToMacroSequence([action], "Shell", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal(expectedStep);
    }

    [Theory]
    [InlineData("shell \"echo ok\"", ShellCommandMode.Shell, "echo ok", "", "exit_code", "stdout", "stderr", 0, 0, 0)]
    [InlineData("shell capture \"echo ok\" exitCode stdout _ 2 50 1000", ShellCommandMode.ShellCapture, "echo ok", "", "exitCode", "stdout", "_", 2, 50, 1000)]
    [InlineData("shell input \"stdin text\" \"cat\" 1", ShellCommandMode.ShellInput, "cat", "stdin text", "exit_code", "stdout", "stderr", 1, 0, 0)]
    [InlineData("shell capture-input \"stdin text\" \"cat\" exitCode stdout stderr 0 0 500", ShellCommandMode.ShellCaptureInput, "cat", "stdin text", "exitCode", "stdout", "stderr", 0, 0, 500)]
    public void FromMacroSequence_ForShellForms_RestoresStructuredShellCommand(
        string step,
        ShellCommandMode expectedMode,
        string expectedCommand,
        string expectedInput,
        string expectedExit,
        string expectedStdout,
        string expectedStderr,
        int expectedRetries,
        int expectedBackoff,
        int expectedTimeout)
    {
        var sequence = new MacroSequence { ScriptSteps = { step } };

        var actions = _converter.FromMacroSequence(sequence);

        var action = actions.Should().ContainSingle().Subject;
        _ = action.Type.Should().Be(EditorActionType.ShellCommand);
        _ = action.ShellCommandMode.Should().Be(expectedMode);
        _ = action.ShellCommand.Should().Be(expectedCommand);
        _ = action.ShellStandardInput.Should().Be(expectedInput);
        _ = action.ShellExitCodeVariableName.Should().Be(expectedExit);
        _ = action.ShellStandardOutputVariableName.Should().Be(expectedStdout);
        _ = action.ShellStandardErrorVariableName.Should().Be(expectedStderr);
        _ = action.ShellRetries.Should().Be(expectedRetries);
        _ = action.ShellBackoffMs.Should().Be(expectedBackoff);
        _ = action.ShellTimeoutMs.Should().Be(expectedTimeout);
    }

    [Fact]
    public void FromMacroSequence_WhenShellLineIsInvalid_RestoresRawScriptStep()
    {
        var sequence = new MacroSequence { ScriptSteps = { "shell capture \"echo ok\" onlyTwo targets" } };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.Actions.Should().ContainSingle().Which.Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Warnings.Should().ContainSingle();
    }

    [Theory]
    [InlineData(WindowCommandMode.Active, "window active title activeTitle")]
    [InlineData(WindowCommandMode.Search, "window search title \"Firefox\" windowAddress")]
    [InlineData(WindowCommandMode.Wait, "window wait title \"Firefox\" 2500 windowAddress")]
    [InlineData(WindowCommandMode.Focus, "window focus title \"Firefox\"")]
    [InlineData(WindowCommandMode.Close, "window close title \"Firefox\"")]
    [InlineData(WindowCommandMode.Move, "window move 100 200")]
    [InlineData(WindowCommandMode.Resize, "window resize 800 600")]
    [InlineData(WindowCommandMode.Center, "window center active")]
    [InlineData(WindowCommandMode.Maximize, "window maximize active")]
    [InlineData(WindowCommandMode.Fullscreen, "window fullscreen active")]
    [InlineData(WindowCommandMode.Floating, "window float active")]
    [InlineData(WindowCommandMode.WorkspaceGet, "window getdesktop workspaceName")]
    [InlineData(WindowCommandMode.WorkspaceSwitch, "window setdesktop \"2\"")]
    [InlineData(WindowCommandMode.WorkspaceMoveActive, "window setdesktopforwindow active \"2\"")]
    [InlineData(WindowCommandMode.WorkspaceMoveWindow, "window setdesktopforwindow address 0x123 \"2\"")]
    public void ToMacroSequence_ForWindowCommandModes_SerializesRunScriptWindowSyntax(WindowCommandMode mode, string expectedStep)
    {
        var sequence = _converter.ToMacroSequence([CreateWindowAction(mode)], "Window", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal(expectedStep);
    }

    [Fact]
    public void FromMacroSequence_ForWindowSearchWithEscapedQuote_RestoresStructuredWindowCommand()
    {
        var sequence = new MacroSequence { ScriptSteps = { "window search title \"Fire\\\"fox\" $addr" } };

        var action = _converter.FromMacroSequence(sequence).Should().ContainSingle().Subject;

        _ = action.Type.Should().Be(EditorActionType.WindowCommand);
        _ = action.WindowCommandMode.Should().Be(WindowCommandMode.Search);
        _ = action.WindowSelectorKind.Should().Be("title");
        _ = action.WindowSelectorValue.Should().Be("Fire\"fox");
        _ = action.WindowOutputVariable.Should().Be("addr");
    }

    [Fact]
    public void FromMacroSequence_WhenWindowLineIsInvalid_RestoresRawScriptStep()
    {
        var sequence = new MacroSequence { ScriptSteps = { "window search title $missingTerm" } };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.Actions.Should().ContainSingle().Which.Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Actions[0].Text.Should().Be("window search title $missingTerm");
        _ = result.Warnings.Should().ContainSingle();
    }

    private static EditorAction CreateWindowAction(WindowCommandMode mode)
    {
        return new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = mode,
            WindowSelectorKind = "title",
            WindowSelectorValue = mode is WindowCommandMode.WorkspaceMoveWindow ? "0x123" : "Firefox",
            WindowActiveField = "title",
            WindowOutputVariable = mode switch
            {
                WindowCommandMode.WorkspaceGet => "workspaceName",
                WindowCommandMode.Active => "activeTitle",
                _ => "windowAddress",
            },
            WindowTimeoutMs = 2500,
            WindowX = 100,
            WindowY = 200,
            WindowWidth = 800,
            WindowHeight = 600,
            WindowWorkspace = "2",
        };
    }

    private void ConfigureTextInputTyping()
    {
        _ = _keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(call => 1_000 + call.Arg<char>());
        _ = _keyCodeMapper.GetCharacterForKeyCode(Arg.Any<int>(), Arg.Any<bool>()).Returns(call => (char)(call.Arg<int>() - 1_000));
        _ = _keyCodeMapper.RequiresShift(Arg.Any<char>()).Returns(returnThis: false);
        _ = _keyCodeMapper.RequiresAltGr(Arg.Any<char>()).Returns(returnThis: false);
    }

    private static void AssertScreenTargetColor(EditorAction action, EditorActionType expectedType, string expectedVariableName)
    {
        _ = action.Type.Should().Be(expectedType);
        _ = action.TryGetScreenReadingPayload(out var payload).Should().BeTrue();
        _ = payload.ScreenTargetColorSource.Should().Be(EditorActionScreenTargetColorSource.Variable);
        _ = payload.ScreenTargetColorVariableName.Should().Be(expectedVariableName);
    }
}
