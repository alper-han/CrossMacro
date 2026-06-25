using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.Infrastructure.Tests.Services;

public class EditorActionConverterTests
{
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
            DelayMs = 55
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        events.Should().HaveCount(2);
        events[0].Type.Should().Be(EventType.KeyPress);
        events[0].KeyCode.Should().Be(30);
        events[0].DelayMs.Should().Be(55);
        events[1].Type.Should().Be(EventType.KeyRelease);
        events[1].KeyCode.Should().Be(30);
        events[1].DelayMs.Should().Be(10);
    }

    [Fact]
    public void ToMacroEvents_CurrentPositionClick_UsesZeroCoordinates()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MouseButton.Left,
            IsAbsolute = false,
            UseCurrentPosition = true,
            X = 120,
            Y = 240
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        events.Should().HaveCount(1);
        events[0].Type.Should().Be(EventType.Click);
        events[0].X.Should().Be(0);
        events[0].Y.Should().Be(0);
        events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void ToMacroEvents_CurrentPositionMouseDown_UsesZeroCoordinates()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseDown,
            Button = MouseButton.Left,
            IsAbsolute = false,
            UseCurrentPosition = true,
            X = 120,
            Y = 240
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        events.Should().HaveCount(1);
        events[0].Type.Should().Be(EventType.ButtonPress);
        events[0].X.Should().Be(0);
        events[0].Y.Should().Be(0);
        events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void ToMacroEvents_MouseClickWithCoordinates_EmitsSingleClickEventWithCoordinateMode()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MouseButton.Left,
            IsAbsolute = true,
            UseCurrentPosition = false,
            X = 120,
            Y = 240
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        events.Should().ContainSingle();
        events[0].Type.Should().Be(EventType.Click);
        events[0].X.Should().Be(120);
        events[0].Y.Should().Be(240);
        events[0].Button.Should().Be(MouseButton.Left);
        events[0].UseCurrentPosition.Should().BeFalse();
        events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
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
            RandomDelayMaxMs = 200
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        events.Should().HaveCount(1);
        events[0].Type.Should().Be(EventType.None);
        events[0].DelayMs.Should().Be(0);
        events[0].HasRandomDelay.Should().BeTrue();
        events[0].RandomDelayMinMs.Should().Be(100);
        events[0].RandomDelayMaxMs.Should().Be(200);
    }

    [Fact]
    public void ToMacroSequence_WhenDelayIsTrailing_SetsTrailingDelayMs()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20, DelayMs = 0 },
            new EditorAction { Type = EditorActionType.Delay, DelayMs = 250 }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Test", isAbsolute: true);

        // Assert
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        sequence.TrailingDelayMs.Should().Be(250);
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
                RandomDelayMaxMs = 120
            }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Test", isAbsolute: true);

        // Assert
        sequence.Events.Should().HaveCount(1);
        sequence.TrailingDelayMs.Should().Be(0);
        sequence.HasTrailingRandomDelay.Should().BeTrue();
        sequence.TrailingDelayMinMs.Should().Be(50);
        sequence.TrailingDelayMaxMs.Should().Be(120);
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
                RandomDelayMaxMs = 20
            },
            new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20, DelayMs = 0 }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Test", isAbsolute: true);

        // Assert
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].DelayMs.Should().Be(30);
        sequence.Events[0].HasRandomDelay.Should().BeTrue();
        sequence.Events[0].RandomDelayMinMs.Should().Be(10);
        sequence.Events[0].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void FromMacroEvent_WhenPressFollowedByRelease_MergesToKeyPressAction()
    {
        // Arrange
        _keyCodeMapper.GetKeyName(30).Returns("A");
        var keyPress = new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, DelayMs = 15 };
        var keyRelease = new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 };

        // Act
        var action = _converter.FromMacroEvent(keyPress, keyRelease);

        // Assert
        action.Type.Should().Be(EditorActionType.KeyPress);
        action.KeyCode.Should().Be(30);
        action.KeyName.Should().Be("A");
        action.DelayMs.Should().Be(15);
    }

    [Theory]
    [InlineData(EventType.KeyPress, EditorActionType.KeyDown)]
    [InlineData(EventType.KeyRelease, EditorActionType.KeyUp)]
    public void FromMacroEvent_WhenKeyboardEvent_RestoresKeyName(EventType eventType, EditorActionType expectedActionType)
    {
        // Arrange
        _keyCodeMapper.GetKeyName(18).Returns("E");
        var macroEvent = new MacroEvent { Type = eventType, KeyCode = 18 };

        // Act
        var action = _converter.FromMacroEvent(macroEvent);

        // Assert
        action.Type.Should().Be(expectedActionType);
        action.KeyCode.Should().Be(18);
        action.KeyName.Should().Be("E");
    }

    [Fact]
    public void ToMacroEvents_WhenMouseActionHasCoordinates_EmitsActionCoordinateMode()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 10, Y = 20 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, IsAbsolute = false, X = 3, Y = 4 },
            new EditorAction { Type = EditorActionType.MouseDown, Button = MouseButton.Right, IsAbsolute = true, X = 30, Y = 40 },
            new EditorAction { Type = EditorActionType.MouseUp, Button = MouseButton.Right, IsAbsolute = false, X = 5, Y = 6 }
        };

        // Act
        var events = actions.Select(action => _converter.ToMacroEvents(action).Single()).ToList();

        // Assert
        events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        events[1].Type.Should().Be(EventType.Click);
        events[2].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        events[3].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
    }

    [Fact]
    public void ToMacroEvents_WhenCurrentPositionOrScroll_DoesNotEmitCoordinateMode()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = true, X = 10, Y = 20 },
            new EditorAction { Type = EditorActionType.MouseDown, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = true, X = 30, Y = 40 },
            new EditorAction { Type = EditorActionType.MouseUp, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = true, X = 50, Y = 60 },
            new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = 1, IsAbsolute = true },
            new EditorAction { Type = EditorActionType.ScrollHorizontal, ScrollAmount = -1, IsAbsolute = true }
        };

        // Act
        var events = actions.SelectMany(action => _converter.ToMacroEvents(action)).ToList();

        // Assert
        events.Should().OnlyContain(ev => ev.CoordinateMode == null);
        events[0].UseCurrentPosition.Should().BeTrue();
        events[0].X.Should().Be(0);
        events[0].Y.Should().Be(0);
        events[1].UseCurrentPosition.Should().BeTrue();
        events[1].X.Should().Be(0);
        events[1].Y.Should().Be(0);
        events[2].UseCurrentPosition.Should().BeTrue();
        events[2].X.Should().Be(0);
        events[2].Y.Should().Be(0);
        events[3].Button.Should().Be(MouseButton.ScrollUp);
        events[4].Button.Should().Be(MouseButton.ScrollLeft);
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
            CoordinateMode = MouseCoordinateMode.Absolute
        };
        var clickEvent = new MacroEvent
        {
            Type = EventType.Click,
            Button = MouseButton.Left,
            X = 3,
            Y = 4,
            CoordinateMode = MouseCoordinateMode.Relative
        };

        // Act
        var moveAction = _converter.FromMacroEvent(moveEvent);
        var clickAction = _converter.FromMacroEvent(clickEvent);

        // Assert
        moveAction.IsAbsolute.Should().BeTrue();
        clickAction.IsAbsolute.Should().BeFalse();
        clickAction.UseCurrentPosition.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenRecordedPrintableKeysHaveNoBoundary_PreservesRawTimingActions()
    {
        // Arrange
        _keyCodeMapper.GetCharacterForKeyCode(30, false).Returns('a');
        _keyCodeMapper.GetCharacterForKeyCode(48, false).Returns('b');

        var sequence = new MacroSequence
        {
            Events =
            [
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, DelayMs = 12 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30, DelayMs = 0 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48, DelayMs = 10 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48, DelayMs = 0 }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(4);
        actions[0].Type.Should().Be(EditorActionType.Delay);
        actions[0].DelayMs.Should().Be(12);
        actions[1].Type.Should().Be(EditorActionType.KeyPress);
        actions[1].KeyCode.Should().Be(30);
        actions[1].DelayMs.Should().Be(0);
        actions[2].Type.Should().Be(EditorActionType.Delay);
        actions[2].DelayMs.Should().Be(10);
        actions[3].Type.Should().Be(EditorActionType.KeyPress);
        actions[3].KeyCode.Should().Be(48);
        actions[3].DelayMs.Should().Be(0);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenAdjacentTextInputs_PreservesSeparateTextInputBoundaries()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "hello" },
            new EditorAction { Type = EditorActionType.TextInput, Text = "world" }
        };

        var sequence = _converter.ToMacroSequence(actions, "Text boundary round trip", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        sequence.TextInputBoundaries.Should().HaveCount(2);
        restored.Should().HaveCount(2);
        restored.Select(action => action.Type).Should().Equal(EditorActionType.TextInput, EditorActionType.TextInput);
        restored.Select(action => action.Text).Should().Equal("hello", "world");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputHasSingleCharacter_PreservesTextInputAction()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "x" }
        };

        var sequence = _converter.ToMacroSequence(actions, "Single text boundary", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        sequence.TextInputBoundaries.Should().ContainSingle();
        restored.Should().ContainSingle();
        restored[0].Type.Should().Be(EditorActionType.TextInput);
        restored[0].Text.Should().Be("x");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputContainsControlCharacters_PreservesMultilineTextInputAction()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "a\r\nb\t\b" }
        };

        var sequence = _converter.ToMacroSequence(actions, "Multiline text boundary", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        sequence.Events.Should().HaveCount(10);
        sequence.Events.Select(ev => ev.KeyCode).Should().Equal(
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
        sequence.TextInputBoundaries.Should().ContainSingle()
            .Which.Should().Be(new TextInputBoundary(0, 10, "a\r\nb\t\b"));
        restored.Should().ContainSingle();
        restored[0].Type.Should().Be(EditorActionType.TextInput);
        restored[0].Text.Should().Be("a\r\nb\t\b");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedTextInputContainsControlCharacters_CompilesAndRestoresMultilineTextInputAction()
    {
        ConfigureTextInputTyping();
        _keyCodeMapper.GetKeyCode("Enter").Returns(InputEventCode.KEY_ENTER);
        _keyCodeMapper.GetKeyCode("Tab").Returns(InputEventCode.KEY_TAB);
        _keyCodeMapper.GetKeyCode("Backspace").Returns(InputEventCode.KEY_BACKSPACE);

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.TextInput, Text = "a\r\nb\t\b" },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        var sequence = _converter.ToMacroSequence(actions, "Script multiline text", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        sequence.ScriptSteps.Should().Equal(
            "repeat 1 {",
            "type a\r\nb\t\b",
            "}");
        sequence.Events.Should().HaveCount(10);
        sequence.Events.Select(ev => ev.KeyCode).Should().Equal(
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
        restored.Should().HaveCount(3);
        restored[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        restored[1].Type.Should().Be(EditorActionType.TextInput);
        restored[1].Text.Should().Be("a\r\nb\t\b");
        restored[2].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedTextInputContainsLiteralDollar_CompilesAndRestoresLiteralDollarText()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.TextInput, Text = "price $10 and $$HOME" },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        var sequence = _converter.ToMacroSequence(actions, "Script dollar text", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        sequence.ScriptSteps.Should().Equal(
            "repeat 1 {",
            "type price $$10 and $$$$HOME",
            "}");
        restored.Should().HaveCount(3);
        restored[1].Type.Should().Be(EditorActionType.TextInput);
        restored[1].Text.Should().Be("price $10 and $$HOME");
    }

    [Fact]
    public async Task SaveAndLoad_WhenScriptBackedTextInputContainsMultilineDollarText_PreservesRestoredTextInputAction()
    {
        ConfigureTextInputTyping();
        _keyCodeMapper.GetKeyCode("Enter").Returns(InputEventCode.KEY_ENTER);
        var fileManager = new MacroFileManager(() => _keyCodeMapper);
        var filePath = Path.Combine(Path.GetTempPath(), $"crossmacro_converter_{Guid.NewGuid():N}.macro");
        var text = "first line\nprice $10";

        try
        {
            var sequence = _converter.ToMacroSequence(
                [
                    new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
                    new EditorAction { Type = EditorActionType.TextInput, Text = text },
                    new EditorAction { Type = EditorActionType.BlockEnd }
                ],
                "Script multiline dollar text",
                isAbsolute: true);

            await fileManager.SaveAsync(sequence, filePath);
            var loaded = await fileManager.LoadAsync(filePath);
            var restored = _converter.FromMacroSequence(loaded!);

            loaded.Should().NotBeNull();
            loaded!.ScriptSteps.Should().Equal(
                "repeat 1 {",
                "type first line\nprice $$10",
                "}");
            restored.Should().HaveCount(3);
            restored[1].Type.Should().Be(EditorActionType.TextInput);
            restored[1].Text.Should().Be(text);
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
        _keyCodeMapper.GetKeyCodeForCharacter('@').Returns(2_000);
        _keyCodeMapper.RequiresAltGr('@').Returns(true);

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "@" }
        };

        var sequence = _converter.ToMacroSequence(actions, "AltGr text boundary", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        sequence.TextInputBoundaries.Should().ContainSingle();
        sequence.Events.Select(ev => ev.KeyCode).Should().Equal(
            InputEventCode.KEY_RIGHTALT,
            2_000,
            2_000,
            InputEventCode.KEY_RIGHTALT);
        restored.Should().ContainSingle();
        restored[0].Type.Should().Be(EditorActionType.TextInput);
        restored[0].Text.Should().Be("@");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputsSeparatedByDelay_PreservesDelayBoundary()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "one" },
            new EditorAction { Type = EditorActionType.Delay, DelayMs = 250 },
            new EditorAction { Type = EditorActionType.TextInput, Text = "two" }
        };

        var sequence = _converter.ToMacroSequence(actions, "Text delay boundary", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        restored.Select(action => action.Type).Should().Equal(
            EditorActionType.TextInput,
            EditorActionType.Delay,
            EditorActionType.TextInput);
        restored[0].Text.Should().Be("one");
        restored[1].DelayMs.Should().Be(250);
        restored[2].Text.Should().Be("two");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputsMixedWithMouseActions_PreservesActionShapeAndTextBoundaries()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "one" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, IsAbsolute = false, UseCurrentPosition = true },
            new EditorAction { Type = EditorActionType.TextInput, Text = "two" }
        };

        var sequence = _converter.ToMacroSequence(actions, "Text mouse boundary", isAbsolute: false, skipInitialZeroZero: true);
        var restored = _converter.FromMacroSequence(sequence);

        restored.Select(action => action.Type).Should().Equal(
            EditorActionType.TextInput,
            EditorActionType.MouseClick,
            EditorActionType.TextInput);
        restored[0].Text.Should().Be("one");
        restored[1].UseCurrentPosition.Should().BeTrue();
        restored[1].Button.Should().Be(MouseButton.Left);
        restored[2].Text.Should().Be("two");
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenTextInputContainsLiteralDollar_PreservesTextWithoutScriptExpansion()
    {
        ConfigureTextInputTyping();

        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.TextInput, Text = "cost $5" },
            new EditorAction { Type = EditorActionType.TextInput, Text = "$HOME" }
        };

        var sequence = _converter.ToMacroSequence(actions, "Text literal dollars", isAbsolute: true);
        var restored = _converter.FromMacroSequence(sequence);

        sequence.ScriptSteps.Should().BeEmpty();
        restored.Select(action => action.Text).Should().Equal("cost $5", "$HOME");
    }

    [Fact]
    public void FromMacroSequence_WhenTextInputBoundaryIsInvalid_FallsBackToRawKeyActions()
    {
        _keyCodeMapper.GetCharacterForKeyCode(30, false).Returns('a');
        _keyCodeMapper.GetCharacterForKeyCode(48, false).Returns('b');
        var sequence = new MacroSequence
        {
            Events =
            [
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48 }
            ],
            TextInputBoundaries = [new TextInputBoundary(0, 99, "invalid")]
        };

        var restored = _converter.FromMacroSequence(sequence);

        restored.Select(action => action.Type).Should().Equal(
            EditorActionType.KeyPress,
            EditorActionType.KeyPress);
        restored.Select(action => action.KeyCode).Should().Equal(30, 48);
    }

    [Fact]
    public void FromMacroSequence_WhenTextInputBoundaryTextDoesNotMatchEvents_FallsBackToRawKeyActions()
    {
        _keyCodeMapper.GetCharacterForKeyCode(30, false).Returns('a');
        _keyCodeMapper.GetCharacterForKeyCode(48, false).Returns('b');
        var sequence = new MacroSequence
        {
            Events =
            [
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48 }
            ],
            TextInputBoundaries = [new TextInputBoundary(0, 4, "stale")]
        };

        var restored = _converter.FromMacroSequence(sequence);

        restored.Select(action => action.Type).Should().Equal(
            EditorActionType.KeyPress,
            EditorActionType.KeyPress);
        restored.Select(action => action.KeyCode).Should().Equal(30, 48);
    }

    [Fact]
    public void ToMacroSequence_WhenBoundaryRestoredTextInputIsUnedited_PreservesOriginalKeyTiming()
    {
        ConfigureTextInputTyping();

        var sequence = new MacroSequence
        {
            Events =
            [
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 1_000 + 'a', DelayMs = 25 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 1_000 + 'a', DelayMs = 7 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 1_000 + 'b', DelayMs = 93 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 1_000 + 'b', DelayMs = 11 }
            ],
            TextInputBoundaries = [new TextInputBoundary(0, 4, "ab")]
        };

        var restored = _converter.FromMacroSequence(sequence);
        var roundTripped = _converter.ToMacroSequence(restored, "Preserve timed text", isAbsolute: true);

        restored.Select(action => action.Type).Should().Equal(EditorActionType.Delay, EditorActionType.TextInput);
        restored[0].DelayMs.Should().Be(25);
        restored[1].Text.Should().Be("ab");
        roundTripped.Events.Should().HaveCount(4);
        roundTripped.Events.Select(ev => ev.DelayMs).Should().Equal(25, 7, 93, 11);
        roundTripped.Events.Select(ev => ev.Type).Should().Equal(
            EventType.KeyPress,
            EventType.KeyRelease,
            EventType.KeyPress,
            EventType.KeyRelease);
        roundTripped.TextInputBoundaries.Should().ContainSingle()
            .Which.Should().Be(new TextInputBoundary(0, 4, "ab"));
    }

    [Fact]
    public void ToMacroSequence_WhenBoundaryRestoredTextInputIsEdited_RegeneratesSyntheticTypingEvents()
    {
        ConfigureTextInputTyping();

        var sequence = new MacroSequence
        {
            Events =
            [
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 1_000 + 'a', DelayMs = 25 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 1_000 + 'a', DelayMs = 7 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 1_000 + 'b', DelayMs = 93 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 1_000 + 'b', DelayMs = 11 }
            ],
            TextInputBoundaries = [new TextInputBoundary(0, 4, "ab")]
        };

        var restored = _converter.FromMacroSequence(sequence);
        restored.Select(action => action.Type).Should().Equal(EditorActionType.Delay, EditorActionType.TextInput);
        restored[1].Text = "ac";

        var roundTripped = _converter.ToMacroSequence(restored, "Regenerate edited text", isAbsolute: true);

        roundTripped.Events.Should().HaveCount(4);
        roundTripped.Events.Select(ev => ev.DelayMs).Should().Equal(25, 0, 10, 0);
        roundTripped.TextInputBoundaries.Should().ContainSingle()
            .Which.Should().Be(new TextInputBoundary(0, 4, "ac"));
    }

    [Fact]
    public void FromMacroSequence_WhenEventContainsRandomDelay_AddsRandomDelayActionBeforeEvent()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 20,
                    DelayMs = 0,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 70,
                    RandomDelayMaxMs = 130
                }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(2);
        actions[0].Type.Should().Be(EditorActionType.Delay);
        actions[0].UseRandomDelay.Should().BeTrue();
        actions[0].RandomDelayMinMs.Should().Be(70);
        actions[0].RandomDelayMaxMs.Should().Be(130);
        actions[1].Type.Should().Be(EditorActionType.MouseMove);
    }

    [Fact]
    public void FromMacroSequence_WhenAbsoluteModeAndMouseButtonEvents_SetsActionsAbsolute()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent { Type = EventType.Click, Button = MouseButton.Left, X = 120, Y = 220 },
                new MacroEvent { Type = EventType.ButtonPress, Button = MouseButton.Left, X = 130, Y = 230 },
                new MacroEvent { Type = EventType.ButtonRelease, Button = MouseButton.Left, X = 140, Y = 240 }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(3);
        actions.Should().OnlyContain(a => a.IsAbsolute);
    }

    [Fact]
    public void FromMacroSequence_WhenRelativeModeAndMouseButtonEvents_SetsActionsRelative()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            [
                new MacroEvent { Type = EventType.Click, Button = MouseButton.Left, X = 0, Y = 0 },
                new MacroEvent { Type = EventType.ButtonPress, Button = MouseButton.Left, X = 5, Y = -3 }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(2);
        actions.Should().OnlyContain(a => !a.IsAbsolute);
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
            [
                new MacroEvent { Type = EventType.Click, Button = MouseButton.Left, X = 0, Y = 0 }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(EditorActionType.MouseClick);
        actions[0].UseCurrentPosition.Should().BeTrue();
        actions[0].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenExplicitCurrentPositionClick_MarksUseCurrentPosition()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 0,
                    Y = 0,
                    UseCurrentPosition = true
                }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(1);
        actions[0].UseCurrentPosition.Should().BeTrue();
        actions[0].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenAbsoluteMacroContainsCurrentPositionClick_KeepsClickRelative()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 640,
                    Y = 480,
                    UseCurrentPosition = true
                },
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 800,
                    Y = 600
                }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(2);
        actions[0].Type.Should().Be(EditorActionType.MouseClick);
        actions[0].UseCurrentPosition.Should().BeTrue();
        actions[0].IsAbsolute.Should().BeFalse();
        actions[1].Type.Should().Be(EditorActionType.MouseMove);
        actions[1].IsAbsolute.Should().BeTrue();
    }

    [Fact]
    public void FromMacroSequence_WhenEventCoordinateModesAreMixed_RestoresPerActionMode()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 100,
                    Y = 200,
                    CoordinateMode = MouseCoordinateMode.Absolute
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 5,
                    Y = -2,
                    CoordinateMode = MouseCoordinateMode.Relative
                },
                new MacroEvent
                {
                    Type = EventType.ButtonPress,
                    Button = MouseButton.Right,
                    X = 300,
                    Y = 400,
                    CoordinateMode = MouseCoordinateMode.Absolute
                },
                new MacroEvent
                {
                    Type = EventType.ButtonRelease,
                    Button = MouseButton.Right,
                    X = -1,
                    Y = -1,
                    CoordinateMode = MouseCoordinateMode.Relative
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Middle,
                    UseCurrentPosition = true,
                    CoordinateMode = MouseCoordinateMode.Absolute
                }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(5);
        actions[0].Type.Should().Be(EditorActionType.MouseMove);
        actions[0].IsAbsolute.Should().BeTrue();
        actions[1].Type.Should().Be(EditorActionType.MouseClick);
        actions[1].IsAbsolute.Should().BeFalse();
        actions[1].UseCurrentPosition.Should().BeFalse();
        actions[2].Type.Should().Be(EditorActionType.MouseDown);
        actions[2].IsAbsolute.Should().BeTrue();
        actions[3].Type.Should().Be(EditorActionType.MouseUp);
        actions[3].IsAbsolute.Should().BeFalse();
        actions[4].Type.Should().Be(EditorActionType.MouseClick);
        actions[4].UseCurrentPosition.Should().BeTrue();
        actions[4].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenCoordinateModeMissing_UsesLegacySequenceFallback()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent { Type = EventType.MouseMove, X = 10, Y = 20 },
                new MacroEvent { Type = EventType.Click, Button = MouseButton.Left, X = 30, Y = 40 }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(2);
        actions.Should().OnlyContain(action => action.IsAbsolute);
    }

    [Fact]
    public void ToAndFromMacroSequence_WhenActionsUseMixedCoordinateModes_PreservesModesOnEventsAndActions()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 200 },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 5, Y = -3 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, IsAbsolute = true, X = 300, Y = 400 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = -1, IsAbsolute = true }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Mixed Modes", isAbsolute: false);
        var restored = _converter.FromMacroSequence(sequence);

        // Assert
        sequence.Events.Should().HaveCount(5);
        sequence.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        sequence.Events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        sequence.Events[2].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        sequence.Events[2].Type.Should().Be(EventType.Click);
        sequence.Events[3].UseCurrentPosition.Should().BeTrue();
        sequence.Events[3].CoordinateMode.Should().BeNull();
        sequence.Events[4].Button.Should().Be(MouseButton.ScrollDown);
        sequence.Events[4].CoordinateMode.Should().BeNull();

        restored.Should().HaveCount(5);
        restored[0].IsAbsolute.Should().BeTrue();
        restored[1].IsAbsolute.Should().BeFalse();
        restored[2].IsAbsolute.Should().BeTrue();
        restored[3].UseCurrentPosition.Should().BeTrue();
        restored[3].IsAbsolute.Should().BeFalse();
        restored[4].Type.Should().Be(EditorActionType.ScrollVertical);
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
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false }
        };

        try
        {
            // Act
            var sequence = _converter.ToMacroSequence(actions, "Mixed Editor Round Trip", isAbsolute: true);
            await fileManager.SaveAsync(sequence, filePath);
            var saved = await File.ReadAllTextAsync(filePath);
            var loaded = await fileManager.LoadAsync(filePath);
            var restored = _converter.FromMacroSequence(loaded!);

            // Assert
            sequence.Events.Select(ev => ev.CoordinateMode).Should().Equal(
                MouseCoordinateMode.Absolute,
                MouseCoordinateMode.Relative,
                null);
            saved.Should().Contain("M,abs,100,200");
            saved.Should().Contain("M,rel,5,-3");
            saved.Should().Contain("C,0,0,Left,CurrentPosition");
            saved.Should().NotContain("C,abs,0,0,Left");
            saved.Should().NotContain("C,rel,0,0,Left");

            loaded.Should().NotBeNull();
            loaded!.Events.Select(ev => ev.CoordinateMode).Should().Equal(
                MouseCoordinateMode.Absolute,
                MouseCoordinateMode.Relative,
                null);
            restored.Should().HaveCount(3);
            restored[0].IsAbsolute.Should().BeTrue();
            restored[1].IsAbsolute.Should().BeFalse();
            restored[2].UseCurrentPosition.Should().BeTrue();
            restored[2].IsAbsolute.Should().BeFalse();
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
    public void ToMacroSequence_WhenScriptStepIfElse_CompilesBranch()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.SetVariable, Text = "mode=fast" },
            new EditorAction { Type = EditorActionType.IfBlockStart, Text = "$mode == fast" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
            new EditorAction { Type = EditorActionType.ElseBlockStart },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Macro", isAbsolute: false);

        // Assert
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.Click);
        sequence.Events[0].Button.Should().Be(MouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenOnlyStateScriptActions_ThrowsInvalidOperationException()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1"
            }
        };

        // Act
        Action act = () => _converter.ToMacroSequence(actions, "State Only Script", isAbsolute: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*did not produce any executable events*");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptAndRegularActionsMixed_UsesUnifiedCompiler()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 120, Y = 220 },
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "2" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Mixed Macro", isAbsolute: true);

        // Assert
        sequence.IsAbsoluteCoordinates.Should().BeTrue();
        sequence.Events.Should().HaveCount(3);
        sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        sequence.Events[1].Type.Should().Be(EventType.Click);
        sequence.Events[2].Type.Should().Be(EventType.Click);
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
                ScriptValue = "fast"
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                X = 320,
                Y = 240
            }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "State Script Mixed Coordinates", isAbsolute: true);

        // Assert
        sequence.Events.Should().HaveCount(2);
        sequence.Events[0].Type.Should().Be(EventType.Click);
        sequence.Events[0].UseCurrentPosition.Should().BeTrue();
        sequence.Events[0].CoordinateMode.Should().BeNull();
        sequence.Events[1].Type.Should().Be(EventType.MouseMove);
        sequence.Events[1].X.Should().Be(320);
        sequence.Events[1].Y.Should().Be(240);
        sequence.Events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        sequence.ScriptSteps.Should().Equal(
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
                Y = 300
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Absolute Then Current Click", isAbsolute: true);

        // Assert
        sequence.Events.Should().HaveCount(2);
        sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        sequence.Events[0].X.Should().Be(500);
        sequence.Events[0].Y.Should().Be(300);
        sequence.Events[1].Type.Should().Be(EventType.Click);
        sequence.Events[1].UseCurrentPosition.Should().BeTrue();
        sequence.ScriptSteps.Should().Equal(
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
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        Action act = () => _converter.ToMacroSequence(actions, "Broken Script", isAbsolute: false);

        // Assert
        act.Should().Throw<InvalidOperationException>();
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
                ScriptValue = "fast"
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "mode",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Text,
                ScriptRightOperand = "fast"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured Script Macro", isAbsolute: false);

        // Assert
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.Click);
        sequence.Events[0].Button.Should().Be(MouseButton.Left);
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
                ScriptValue = "a=b"
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Set Equals Text", isAbsolute: false);

        // Assert
        sequence.ScriptSteps.Should().Equal(
            "set x=a=b",
            "click current left");
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.Click);
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
                ScriptValue = "$foo"
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Set Dollar Text", isAbsolute: false);

        // Assert
        sequence.ScriptSteps.Should().Equal(
            "set name $$foo",
            "click current left");
        sequence.Events.Should().HaveCount(1);
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
                ScriptRightOperand = "$foo"
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Condition Dollar Text", isAbsolute: false);

        // Assert
        sequence.ScriptSteps.Should().Equal(
            "if $$foo == $$foo {",
            "click current left",
            "}");
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.Click);
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
                ScriptValue = "$foo"
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "$name",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Text,
                ScriptRightOperand = "$foo"
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        var sequence = _converter.ToMacroSequence(actions, "Condition Variable Prefix", isAbsolute: false);

        sequence.ScriptSteps.Should().Equal(
            "set name $$foo",
            "if $name == $$foo {",
            "click current left",
            "}");
        sequence.Events.Should().ContainSingle();
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
                ScreenColorVariableName = "color"
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "color",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Color,
                ScriptRightOperand = "1c1c1c"
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false
            },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        var sequence = _converter.ToMacroSequence(actions, "Condition Color", isAbsolute: false);

        sequence.ScriptSteps.Should().Equal(
            "pixelcolor 1 2 color",
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
                ScriptValue = "0"
            },
            new EditorAction
            {
                Type = EditorActionType.IncrementVariable,
                Text = "broken_inc_payload",
                ScriptVariableName = "counter",
                ScriptNumericSourceType = ScriptNumericSourceType.Number,
                ScriptNumericValue = "2"
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                Text = "broken_condition_payload",
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "counter",
                ScriptConditionOperator = ScriptConditionOperator.GreaterThanOrEqual,
                ScriptRightOperandType = ScriptOperandType.Number,
                ScriptRightOperand = "2"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
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
                ForStepValue = "1"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured Overrides Legacy Text", isAbsolute: false);

        // Assert
        sequence.ScriptSteps.Should().Equal(
            "set counter 0",
            "inc counter 2",
            "if $counter >= 2 {",
            "click current left",
            "}",
            "for j from 1 to 2 step 1 {",
            "click current left",
            "}");
        sequence.Events.Should().HaveCount(3);
    }

    [Fact]
    public void ToMacroSequence_WhenLegacySetActionEditedThenResetToDefaults_UsesStructuredSerialization()
    {
        // Arrange
        var loadedActions = _converter.FromMacroSequence(new MacroSequence
        {
            ScriptSteps =
            [
                "set 1bad=0",
                "click current left"
            ]
        });

        loadedActions.Should().HaveCount(2);
        loadedActions[0].Type.Should().Be(EditorActionType.SetVariable);
        loadedActions[0].Text.Should().Be("1bad=0");
        loadedActions[0].PreferLegacyScriptText.Should().BeTrue();

        // Simulate editing via structured controls and then returning to defaults.
        loadedActions[0].ScriptVariableName = "counter";
        loadedActions[0].ScriptVariableName = "i";
        loadedActions[0].ScriptValue = "5";
        loadedActions[0].ScriptValue = "0";

        loadedActions[0].PreferLegacyScriptText.Should().BeFalse();

        // Act
        var sequence = _converter.ToMacroSequence(loadedActions, "Legacy Reset Defaults", isAbsolute: false);

        // Assert
        sequence.ScriptSteps.Should().Equal(
            "set i 0",
            "click current left");
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.Click);
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
                ForEndValue = "3"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured For Macro", isAbsolute: false);

        // Assert
        sequence.Events.Should().HaveCount(3);
        sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click);
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
                ScriptValue = "3"
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
                ForStepValue = "$limit"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Shared For Variable", isAbsolute: false);

        // Assert
        sequence.ScriptSteps.Should().Contain("for i from 0 to $limit step $limit {");
        sequence.Events.Should().HaveCount(2); // i = 0, 3
        sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click);
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
                ForEndValue = "3"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Break },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Break Loop Macro", isAbsolute: false);

        // Assert
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.Click);
        sequence.Events[0].Button.Should().Be(MouseButton.Left);
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
                ForEndValue = "3"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Continue },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Continue Loop Macro", isAbsolute: false);

        // Assert
        sequence.Events.Should().HaveCount(3);
        sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click && ev.Button == MouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenBreakUsedOutsideLoop_ThrowsInvalidOperationException()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.Break },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false }
        };

        // Act
        Action act = () => _converter.ToMacroSequence(actions, "Invalid Break Macro", isAbsolute: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
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
                ScriptValue = "1"
            },
            new EditorAction
            {
                Type = EditorActionType.KeyPress,
                KeyCode = 30
            }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Skip Initial Propagation", isAbsolute: false, skipInitialZeroZero: false);

        // Assert
        sequence.SkipInitialZeroZero.Should().BeTrue();
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
                ScriptValue = "0"
            },
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Step Preserve", isAbsolute: false);

        // Assert
        sequence.ScriptSteps.Should().Equal(
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
                ScriptValue = "1"
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 10, RandomDelayMaxMs = 20 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Random Delay", isAbsolute: false);

        // Assert
        sequence.Events.Should().HaveCount(2);
        sequence.Events[1].HasRandomDelay.Should().BeTrue();
        sequence.Events[1].RandomDelayMinMs.Should().Be(10);
        sequence.Events[1].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedHasInitialRandomDelay_PreservesFirstEventRandomDelay()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 10, RandomDelayMaxMs = 20 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Initial Random Delay", isAbsolute: false);

        // Assert
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.Click);
        sequence.Events[0].HasRandomDelay.Should().BeTrue();
        sequence.Events[0].RandomDelayMinMs.Should().Be(10);
        sequence.Events[0].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedModifierOnlyKeyPress_DoesNotFail()
    {
        // Arrange
        _keyCodeMapper.IsModifierKeyCode(29).Returns(true);
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1"
            },
            new EditorAction
            {
                Type = EditorActionType.KeyPress,
                KeyCode = 29
            }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Modifier KeyPress", isAbsolute: false);

        // Assert
        sequence.Events.Should().HaveCount(2);
        sequence.Events[0].Type.Should().Be(EventType.KeyPress);
        sequence.Events[0].KeyCode.Should().Be(29);
        sequence.Events[1].Type.Should().Be(EventType.KeyRelease);
        sequence.Events[1].KeyCode.Should().Be(29);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsPresent_RestoresStructuredScriptActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "set i 0",
                "for i from 1 to 10 {",
                "click left",
                "}",
                "repeat $n {",
                "tap 30",
                "}"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(7);

        actions[0].Type.Should().Be(EditorActionType.SetVariable);
        actions[0].ScriptVariableName.Should().Be("i");
        actions[0].ScriptValueType.Should().Be(ScriptValueType.Number);
        actions[0].ScriptValue.Should().Be("0");

        actions[1].Type.Should().Be(EditorActionType.ForBlockStart);
        actions[1].ForVariableName.Should().Be("i");
        actions[1].ForStartType.Should().Be(ScriptNumericSourceType.Number);
        actions[1].ForStartValue.Should().Be("1");
        actions[1].ForEndType.Should().Be(ScriptNumericSourceType.Number);
        actions[1].ForEndValue.Should().Be("10");

        actions[2].Type.Should().Be(EditorActionType.MouseClick);
        actions[2].UseCurrentPosition.Should().BeTrue();

        actions[3].Type.Should().Be(EditorActionType.BlockEnd);

        actions[4].Type.Should().Be(EditorActionType.RepeatBlockStart);
        actions[4].ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        actions[4].ScriptNumericValue.Should().Be("n");

        actions[5].Type.Should().Be(EditorActionType.KeyPress);
        actions[5].KeyCode.Should().Be(30);

        actions[6].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainNamedKeyDownUp_RestoresKeyActions()
    {
        // Arrange
        _keyCodeMapper.GetKeyCode("ctrl").Returns(29);
        _keyCodeMapper.GetKeyName(29).Returns("Ctrl");
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "key down ctrl",
                "key up ctrl"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(2);
        actions[0].Type.Should().Be(EditorActionType.KeyDown);
        actions[0].KeyCode.Should().Be(29);
        actions[0].KeyName.Should().Be("Ctrl");
        actions[1].Type.Should().Be(EditorActionType.KeyUp);
        actions[1].KeyCode.Should().Be(29);
        actions[1].KeyName.Should().Be("Ctrl");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepContainsNamedSingleTap_RestoresKeyPress()
    {
        // Arrange
        _keyCodeMapper.GetKeyCode("enter").Returns(28);
        _keyCodeMapper.GetKeyName(28).Returns("Enter");
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "tap enter"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(EditorActionType.KeyPress);
        actions[0].KeyCode.Should().Be(28);
        actions[0].KeyName.Should().Be("Enter");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepContainsScrollWithoutCount_RestoresSingleScrollAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "scroll up"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(EditorActionType.ScrollVertical);
        actions[0].ScrollAmount.Should().Be(1);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainRandomDelayRange_RestoresDelayAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "repeat 2 {",
                "delay random 10..20",
                "click left",
                "}"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(4);
        actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        actions[1].Type.Should().Be(EditorActionType.Delay);
        actions[1].UseRandomDelay.Should().BeTrue();
        actions[1].RandomDelayMinMs.Should().Be(10);
        actions[1].RandomDelayMaxMs.Should().Be(20);
        actions[2].Type.Should().Be(EditorActionType.MouseClick);
        actions[3].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void FromMacroSequence_WhenSetStepUsesEscapedDollar_RestoresTextLiteral()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "set name $$foo",
                "click current left"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(2);
        actions[0].Type.Should().Be(EditorActionType.SetVariable);
        actions[0].ScriptVariableName.Should().Be("name");
        actions[0].ScriptValueType.Should().Be(ScriptValueType.Text);
        actions[0].ScriptValue.Should().Be("$foo");
    }

    [Fact]
    public void FromMacroSequence_WhenConditionStepUsesEscapedDollar_RestoresTextLiterals()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "if $$foo == $$bar {",
                "click left",
                "}"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(3);
        actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.Text);
        actions[0].ScriptLeftOperand.Should().Be("$foo");
        actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Text);
        actions[0].ScriptRightOperand.Should().Be("$bar");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsUseMoveAliasAbsolute_RestoresStructuredMoveAndClick()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "move absolute 200 300",
                "click l"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(2);
        actions[0].Type.Should().Be(EditorActionType.MouseMove);
        actions[0].IsAbsolute.Should().BeTrue();
        actions[0].X.Should().Be(200);
        actions[0].Y.Should().Be(300);
        actions[1].Type.Should().Be(EditorActionType.MouseClick);
        actions[1].IsAbsolute.Should().BeTrue();
        actions[1].X.Should().Be(200);
        actions[1].Y.Should().Be(300);
        actions[1].Button.Should().Be(MouseButton.Left);
        actions[1].UseCurrentPosition.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenScriptContainsExplicitMoveClickPairs_RoundTripsMoveEvents()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "move abs 10 10",
                "click left",
                "move abs 20 20",
                "click left"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "RoundTrip Move Click Pairs", isAbsolute: true);

        // Assert
        actions.Should().HaveCount(4);
        actions[0].Type.Should().Be(EditorActionType.MouseMove);
        actions[1].Type.Should().Be(EditorActionType.MouseClick);
        actions[2].Type.Should().Be(EditorActionType.MouseMove);
        actions[3].Type.Should().Be(EditorActionType.MouseClick);

        saved.Events.Should().HaveCount(4);
        saved.Events[0].Type.Should().Be(EventType.MouseMove);
        saved.Events[0].X.Should().Be(10);
        saved.Events[0].Y.Should().Be(10);
        saved.Events[1].Type.Should().Be(EventType.Click);
        saved.Events[1].X.Should().Be(10);
        saved.Events[1].Y.Should().Be(10);
        saved.Events[2].Type.Should().Be(EventType.MouseMove);
        saved.Events[2].X.Should().Be(20);
        saved.Events[2].Y.Should().Be(20);
        saved.Events[3].Type.Should().Be(EventType.Click);
        saved.Events[3].X.Should().Be(20);
        saved.Events[3].Y.Should().Be(20);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsUseMixedMoveModes_RoundTripsEventCoordinateModes()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "move abs 200 300",
                "click left",
                "move rel 5 -4",
                "click right"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "Mixed Script Modes", isAbsolute: false);

        // Assert
        actions.Should().HaveCount(4);
        actions[0].IsAbsolute.Should().BeTrue();
        actions[1].IsAbsolute.Should().BeTrue();
        actions[1].UseCurrentPosition.Should().BeFalse();
        actions[2].IsAbsolute.Should().BeFalse();
        actions[3].IsAbsolute.Should().BeFalse();
        actions[3].UseCurrentPosition.Should().BeFalse();

        saved.Events.Should().HaveCount(4);
        saved.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        saved.Events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        saved.Events[2].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        saved.Events[3].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
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
                Button = MouseButton.Left,
                IsAbsolute = true,
                X = 200,
                Y = 300,
                UseCurrentPosition = false
            },
            new EditorAction { Type = EditorActionType.BlockEnd }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Backed No Duplicate Move", isAbsolute: true);

        // Assert
        sequence.ScriptSteps.Should().Equal(
            "repeat 1 {",
            "move abs 200 300",
            "click left",
            "}");
        sequence.Events.Should().HaveCount(2);
        sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        sequence.Events[1].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainCurrentPositionDownUp_PreservesUseCurrentPosition()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "down left",
                "up left"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "DownUpCurrentPosition", isAbsolute: false);

        // Assert
        actions.Should().HaveCount(2);
        actions[0].Type.Should().Be(EditorActionType.MouseDown);
        actions[0].UseCurrentPosition.Should().BeTrue();
        actions[0].IsAbsolute.Should().BeFalse();
        actions[1].Type.Should().Be(EditorActionType.MouseUp);
        actions[1].UseCurrentPosition.Should().BeTrue();
        actions[1].IsAbsolute.Should().BeFalse();

        saved.Events.Should().HaveCount(2);
        saved.Events[0].Type.Should().Be(EventType.ButtonPress);
        saved.Events[0].UseCurrentPosition.Should().BeTrue();
        saved.Events[1].Type.Should().Be(EventType.ButtonRelease);
        saved.Events[1].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainAbsoluteMoveThenCurrentPositionClick_PreservesSeparateCurrentPositionAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "move abs 120 240",
                "click current left"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(2);
        actions[0].Type.Should().Be(EditorActionType.MouseMove);
        actions[0].IsAbsolute.Should().BeTrue();
        actions[0].X.Should().Be(120);
        actions[0].Y.Should().Be(240);
        actions[1].Type.Should().Be(EditorActionType.MouseClick);
        actions[1].UseCurrentPosition.Should().BeTrue();
        actions[1].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenAbsoluteMoveAndDelayedImplicitClick_PreservesPositionedButtonSemantics()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "move abs 500 300",
                "delay 50",
                "click left"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(3);
        actions[0].Type.Should().Be(EditorActionType.MouseMove);
        actions[0].IsAbsolute.Should().BeTrue();
        actions[0].X.Should().Be(500);
        actions[0].Y.Should().Be(300);
        actions[1].Type.Should().Be(EditorActionType.Delay);
        actions[1].DelayMs.Should().Be(50);
        actions[2].Type.Should().Be(EditorActionType.MouseClick);
        actions[2].UseCurrentPosition.Should().BeFalse();
        actions[2].IsAbsolute.Should().BeTrue();
        actions[2].X.Should().Be(500);
        actions[2].Y.Should().Be(300);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainBreakAndContinue_RestoresLoopControlActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "repeat 1 {",
                "break",
                "}",
                "repeat 1 {",
                "continue",
                "}"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(6);
        actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        actions[1].Type.Should().Be(EditorActionType.Break);
        actions[2].Type.Should().Be(EditorActionType.BlockEnd);
        actions[3].Type.Should().Be(EditorActionType.RepeatBlockStart);
        actions[4].Type.Should().Be(EditorActionType.Continue);
        actions[5].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenScriptStepIsUnsupported_RestoresRawActionAndWarning()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "set i 0",
                "tap ctrl+c",
                "click left"
            ]
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        result.RestoredFromScriptSteps.Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].StepIndex.Should().Be(2);
        result.Warnings[0].Step.Should().Be("tap ctrl+c");
        result.Actions.Should().HaveCount(3);
        result.Actions[1].Type.Should().Be(EditorActionType.RawScriptStep);
        result.Actions[1].Text.Should().Be("tap ctrl+c");
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenScreenReadingStepsPresent_RestoresStructuredActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "pixelcolor 10 20 color",
                "pixelcolor rel -1 2 relativeColor",
                "waitcolor 11 22 00FFAA 2500 wait_ok",
                "pixelsearch 0 0 3 3 123456 found x y tolerance 5"
            ]
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        result.RestoredFromScriptSteps.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
        result.Actions.Should().HaveCount(4);
        result.Actions[0].Type.Should().Be(EditorActionType.PixelColor);
        result.Actions[0].ScreenX.Should().Be(10);
        result.Actions[0].ScreenY.Should().Be(20);
        result.Actions[0].ScreenColorVariableName.Should().Be("color");
        result.Actions[1].Type.Should().Be(EditorActionType.PixelColor);
        result.Actions[1].IsAbsolute.Should().BeFalse();
        result.Actions[1].ScreenX.Should().Be(-1);
        result.Actions[1].ScreenY.Should().Be(2);
        result.Actions[1].ScreenColorVariableName.Should().Be("relativeColor");
        result.Actions[2].Type.Should().Be(EditorActionType.WaitColor);
        result.Actions[2].ScreenX.Should().Be(11);
        result.Actions[2].ScreenY.Should().Be(22);
        result.Actions[2].ScreenColorHex.Should().Be("00FFAA");
        result.Actions[2].ScreenTimeoutMs.Should().Be(2500);
        result.Actions[2].ScreenColorVariableName.Should().Be("wait_ok");
        result.Actions[3].Type.Should().Be(EditorActionType.PixelSearch);
        result.Actions[3].ScreenLeft.Should().Be(0);
        result.Actions[3].ScreenTop.Should().Be(0);
        result.Actions[3].ScreenWidth.Should().Be(3);
        result.Actions[3].ScreenHeight.Should().Be(3);
        result.Actions[3].ScreenColorHex.Should().Be("123456");
        result.Actions[3].ScreenFoundVariableName.Should().Be("found");
        result.Actions[3].ScreenFoundXVariableName.Should().Be("x");
        result.Actions[3].ScreenFoundYVariableName.Should().Be("y");
        result.Actions[3].ScreenTolerance.Should().Be(5);
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
                ScreenColorVariableName = "color"
            },
            new EditorAction
            {
                Type = EditorActionType.PixelColor,
                IsAbsolute = false,
                ScreenX = -1,
                ScreenY = 2,
                ScreenColorVariableName = "relativeColor"
            },
            new EditorAction
            {
                Type = EditorActionType.WaitColor,
                ScreenX = 11,
                ScreenY = 22,
                ScreenColorHex = "00ffaa",
                ScreenTimeoutMs = 2500,
                ScreenColorVariableName = "wait_ok"
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
                ScreenTolerance = 5
            }
        };

        var sequence = _converter.ToMacroSequence(actions, "Screen Reading", isAbsolute: true);

        sequence.ScriptSteps.Should().Equal(
            "pixelcolor 10 20 color",
            "pixelcolor rel -1 2 relativeColor",
            "waitcolor 11 22 00FFAA 2500 wait_ok",
            "pixelsearch 0 0 3 3 123456 found x y tolerance 5");
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
                ScreenColorVariableName = "wait_ok"
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
                ScreenTolerance = 5
            }
        };

        var sequence = _converter.ToMacroSequence(actions, "Screen Reading Variables", isAbsolute: true);

        sequence.ScriptSteps.Should().Equal(
            "waitcolor 11 22 $sampled 2500 wait_ok",
            "pixelsearch 0 0 3 3 $sampled found x y tolerance 5");

        var restored = _converter.FromMacroSequenceWithDiagnostics(sequence);

        restored.RestoredFromScriptSteps.Should().BeTrue();
        restored.Warnings.Should().BeEmpty();
        restored.Actions.Should().HaveCount(2);

        AssertScreenTargetColor(restored.Actions[0], EditorActionType.WaitColor, "sampled");
        AssertScreenTargetColor(restored.Actions[1], EditorActionType.PixelSearch, "sampled");
    }

    [Theory]
    [InlineData("pixelcolor 10 20")]
    [InlineData("pixelcolor rel 1 2")]
    [InlineData("waitcolor 11 22 00FFAA")]
    [InlineData("pixelsearch 0 0 3 3 123456")]
    [InlineData("pixelsearch 0 0 3 3 123456 tolerance 10")]
    public void FromMacroSequenceWithDiagnostics_WhenScreenReadingCompilerOnlyShapePresent_RestoresRawActionAndWarning(string step)
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps = [step]
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        result.RestoredFromScriptSteps.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].Step.Should().Be(step);
        result.Actions.Should().ContainSingle();
        result.Actions[0].Type.Should().Be(EditorActionType.RawScriptStep);
        result.Actions[0].Text.Should().Be(step);
    }

    [Fact]
    public void ToMacroSequence_WhenRawScriptStepPresent_PreservesRawStepAndCompiles()
    {
        // Arrange
        _keyCodeMapper.GetKeyCode("ctrl").Returns(29);
        _keyCodeMapper.GetKeyCode("c").Returns(46);
        _keyCodeMapper.IsModifierKeyCode(29).Returns(true);
        _keyCodeMapper.IsModifierKeyCode(46).Returns(false);

        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.RawScriptStep,
                Text = "tap ctrl+c"
            }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Raw Step", isAbsolute: false);

        // Assert
        sequence.ScriptSteps.Should().Equal("tap ctrl+c");
        sequence.Events.Should().HaveCount(4);
        sequence.Events[0].Type.Should().Be(EventType.KeyPress);
        sequence.Events[3].Type.Should().Be(EventType.KeyRelease);
    }

    [Fact]
    public void FromMacroSequence_WhenConditionContainsComparatorText_ParsesUsingEqualityOperator()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "if $mode == a>=b {",
                "click left",
                "}"
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(3);
        actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        actions[0].ScriptConditionOperator.Should().Be(ScriptConditionOperator.Equals);
        actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        actions[0].ScriptLeftOperand.Should().Be("mode");
        actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Text);
        actions[0].ScriptRightOperand.Should().Be("a>=b");
    }

    [Fact]
    public void FromMacroSequence_WhenConditionUsesBareHexColor_LoadsColorOperand()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "if $color == 1c1c1c {",
                "click left",
                "}"
            ]
        };

        var actions = _converter.FromMacroSequence(sequence);

        actions.Should().HaveCount(3);
        actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        actions[0].ScriptLeftOperand.Should().Be("color");
        actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Color);
        actions[0].ScriptRightOperand.Should().Be("1C1C1C");
    }

    [Fact]
    public void FromMacroSequence_WhenConditionUsesNumericOnlyBareHexColor_LoadsColorOperand()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "if $color == 000000 {",
                "click left",
                "}"
            ]
        };

        var actions = _converter.FromMacroSequence(sequence);

        actions.Should().HaveCount(3);
        actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Color);
        actions[0].ScriptRightOperand.Should().Be("000000");
    }

    [Fact]
    public void FromMacroSequence_WhenNumericComparisonUsesSixDigitNumber_KeepsNumberOperand()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            [
                "if $count > 100000 {",
                "click left",
                "}"
            ]
        };

        var actions = _converter.FromMacroSequence(sequence);

        actions.Should().HaveCount(3);
        actions[0].ScriptConditionOperator.Should().Be(ScriptConditionOperator.GreaterThan);
        actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Number);
        actions[0].ScriptRightOperand.Should().Be("100000");
    }

    private void ConfigureTextInputTyping()
    {
        _keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(call => 1_000 + call.Arg<char>());
        _keyCodeMapper.GetCharacterForKeyCode(Arg.Any<int>(), Arg.Any<bool>()).Returns(call => (char)(call.Arg<int>() - 1_000));
        _keyCodeMapper.RequiresShift(Arg.Any<char>()).Returns(false);
        _keyCodeMapper.RequiresAltGr(Arg.Any<char>()).Returns(false);
    }

    private static void AssertScreenTargetColor(EditorAction action, EditorActionType expectedType, string expectedVariableName)
    {
        action.Type.Should().Be(expectedType);
        action.TryGetScreenReadingPayload(out var payload).Should().BeTrue();
        payload.ScreenTargetColorSource.Should().Be(EditorActionScreenTargetColorSource.Variable);
        payload.ScreenTargetColorVariableName.Should().Be(expectedVariableName);
    }
}
