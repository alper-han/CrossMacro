using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
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
        var keyPress = new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, DelayMs = 15 };
        var keyRelease = new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 };

        // Act
        var action = _converter.FromMacroEvent(keyPress, keyRelease);

        // Assert
        action.Type.Should().Be(EditorActionType.KeyPress);
        action.KeyCode.Should().Be(30);
        action.DelayMs.Should().Be(15);
    }

    [Fact]
    public void FromMacroSequence_WhenConsecutivePrintableKeys_MergesToTextInput()
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
        actions.Should().HaveCount(2);
        actions[0].Type.Should().Be(EditorActionType.Delay);
        actions[0].DelayMs.Should().Be(12);
        actions[1].Type.Should().Be(EditorActionType.TextInput);
        actions[1].Text.Should().Be("ab");
        actions[1].DelayMs.Should().Be(0);
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
        sequence.Events[1].Type.Should().Be(EventType.MouseMove);
        sequence.Events[1].X.Should().Be(320);
        sequence.Events[1].Y.Should().Be(240);
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
        actions[1].Type.Should().Be(EditorActionType.KeyUp);
        actions[1].KeyCode.Should().Be(29);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepContainsNamedSingleTap_RestoresKeyPress()
    {
        // Arrange
        _keyCodeMapper.GetKeyCode("enter").Returns(28);
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
}
