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
