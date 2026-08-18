namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class EditorActionConverterTests
{

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
}
