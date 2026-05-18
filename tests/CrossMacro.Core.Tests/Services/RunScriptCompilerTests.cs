using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.Core.Tests.Services;

public class RunScriptCompilerTests
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly RunScriptCompiler _compiler;

    public RunScriptCompilerTests()
    {
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _keyCodeMapper.GetKeyCode("Shift").Returns(42);
        _keyCodeMapper.GetKeyCode("AltGr").Returns(100);
        _keyCodeMapper.GetKeyCode("Enter").Returns(28);
        _keyCodeMapper.GetKeyCode("Tab").Returns(15);
        _keyCodeMapper.GetKeyCodeForCharacter('A').Returns(30);
        _keyCodeMapper.RequiresShift('A').Returns(true);
        _keyCodeMapper.RequiresAltGr('A').Returns(false);
        _keyCodeMapper.GetKeyCodeForCharacter('@').Returns(16);
        _keyCodeMapper.RequiresShift('@').Returns(false);
        _keyCodeMapper.RequiresAltGr('@').Returns(true);
        _keyCodeMapper.IsModifierKeyCode(29).Returns(true);

        _compiler = new RunScriptCompiler(_keyCodeMapper);
    }

    [Fact]
    public void Compile_WhenTypeStepRequiresShift_EmitsModifierWrappedKeyEvents()
    {
        var result = _compiler.Compile([new RunScriptStep("type A")]);

        result.Success.Should().BeTrue();
        result.Sequence.Should().NotBeNull();
        result.Sequence!.Events.Should().HaveCount(4);
        result.Sequence.Events.Select(e => (e.Type, e.KeyCode)).Should().Equal(
            (EventType.KeyPress, 42),
            (EventType.KeyPress, 30),
            (EventType.KeyRelease, 30),
            (EventType.KeyRelease, 42));
    }

    [Fact]
    public void Compile_WhenAbsoluteAndRelativeMovesAreMixed_EmitsPerEventCoordinateModes()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move abs 100 200"),
            new RunScriptStep("move rel 10 -5")
        ]);

        result.Success.Should().BeTrue();
        result.Sequence.Should().NotBeNull();
        result.Sequence!.IsAbsoluteCoordinates.Should().BeFalse();
        result.Sequence.Events.Should().HaveCount(2);
        result.Sequence.Events.Select(e => (e.Type, e.X, e.Y, e.CoordinateMode)).Should().Equal(
            (EventType.MouseMove, 100, 200, MouseCoordinateMode.Absolute),
            (EventType.MouseMove, 10, -5, MouseCoordinateMode.Relative));
        MacroPositionSemantics.GetCoordinateModeSummary(result.Sequence).Should().Be(CoordinateModeSummary.Mixed);
    }

    [Fact]
    public void Compile_WhenRelativeMoveThenClick_EmitsRelativeButtonEventAtCurrentCoordinates()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move rel 10 -5"),
            new RunScriptStep("click left")
        ]);

        result.Success.Should().BeTrue();
        result.Sequence.Should().NotBeNull();
        result.Sequence!.Events.Should().HaveCount(2);
        var click = result.Sequence.Events[1];
        click.Type.Should().Be(EventType.Click);
        click.Button.Should().Be(MouseButton.Left);
        click.UseCurrentPosition.Should().BeFalse();
        click.X.Should().Be(0);
        click.Y.Should().Be(0);
        click.CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
    }

    [Fact]
    public void Compile_WhenMixedMovesAndClicks_EmitsButtonEventsWithCurrentMoveMode()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move abs 100 200"),
            new RunScriptStep("click left"),
            new RunScriptStep("move rel 10 -5"),
            new RunScriptStep("click right")
        ]);

        result.Success.Should().BeTrue();
        result.Sequence.Should().NotBeNull();
        result.Sequence!.IsAbsoluteCoordinates.Should().BeFalse();
        result.Sequence.Events.Should().HaveCount(4);
        result.Sequence.Events.Select(e => (e.Type, e.Button, e.X, e.Y, e.CoordinateMode)).Should().Equal(
            (EventType.MouseMove, MouseButton.None, 100, 200, MouseCoordinateMode.Absolute),
            (EventType.Click, MouseButton.Left, 100, 200, MouseCoordinateMode.Absolute),
            (EventType.MouseMove, MouseButton.None, 10, -5, MouseCoordinateMode.Relative),
            (EventType.Click, MouseButton.Right, 0, 0, MouseCoordinateMode.Relative));
        MacroPositionSemantics.GetCoordinateModeSummary(result.Sequence).Should().Be(CoordinateModeSummary.Mixed);
    }

    [Fact]
    public void Compile_WhenCurrentClickFollowsAbsoluteMove_DoesNotAssignCoordinateMode()
    {
        var result = _compiler.Compile(
        [
            new RunScriptStep("move abs 100 200"),
            new RunScriptStep("click current left")
        ]);

        result.Success.Should().BeTrue();
        result.Sequence.Should().NotBeNull();
        var click = result.Sequence!.Events[1];
        click.UseCurrentPosition.Should().BeTrue();
        click.X.Should().Be(0);
        click.Y.Should().Be(0);
        click.CoordinateMode.Should().BeNull();
    }

    [Fact]
    public void Compile_WhenMalformedAbsoluteMove_ReturnsFailure()
    {
        var result = _compiler.Compile([new RunScriptStep("move abs 100")]);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unsupported step syntax");
    }

    [Fact]
    public void Compile_WhenTypeStepRequiresAltGr_EmitsModifierWrappedKeyEvents()
    {
        var result = _compiler.Compile([new RunScriptStep("type @")]);

        result.Success.Should().BeTrue();
        result.Sequence.Should().NotBeNull();
        result.Sequence!.Events.Select(e => (e.Type, e.KeyCode)).Should().Equal(
            (EventType.KeyPress, 100),
            (EventType.KeyPress, 16),
            (EventType.KeyRelease, 16),
            (EventType.KeyRelease, 100));
    }

    [Fact]
    public void Compile_WhenTapContainsSingleModifier_EmitsPressAndRelease()
    {
        _keyCodeMapper.GetKeyCode("ctrl").Returns(29);

        var result = _compiler.Compile([new RunScriptStep("tap ctrl")]);

        result.Success.Should().BeTrue();
        result.Sequence.Should().NotBeNull();
        result.Sequence!.Events.Select(e => (e.Type, e.KeyCode)).Should().Equal(
            (EventType.KeyPress, 29),
            (EventType.KeyRelease, 29));
    }

    [Fact]
    public void Compile_WhenSetUsesEscapedDollarLiteral_PreservesLiteralTextInCondition()
    {
        var steps = new[]
        {
            new RunScriptStep("set name $$foo"),
            new RunScriptStep("if $name == $$foo {"),
            new RunScriptStep("click current left"),
            new RunScriptStep("}")
        };

        var result = _compiler.Compile(steps);

        result.Success.Should().BeTrue();
        result.Sequence.Should().NotBeNull();
        result.Sequence!.Events.Should().ContainSingle();
        result.Sequence.Events[0].Type.Should().Be(EventType.Click);
        result.Sequence.Events[0].Button.Should().Be(MouseButton.Left);
        result.Sequence.Events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void Compile_WhenTypeCharacterCannotBeMapped_ReturnsDetailedFailure()
    {
        _keyCodeMapper.GetKeyCodeForCharacter('?').Returns(-1);

        var result = _compiler.Compile([new RunScriptStep("type ?")]);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot map character '?' for type command");
    }
}
