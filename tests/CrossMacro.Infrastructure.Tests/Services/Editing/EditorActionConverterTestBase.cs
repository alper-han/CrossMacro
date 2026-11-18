
namespace CrossMacro.Infrastructure.Tests.Services.Editing;

public abstract class EditorActionConverterTestBase
{
    internal readonly IKeyCodeMapper _keyCodeMapper;
    internal readonly EditorActionConverter _converter;

    protected EditorActionConverterTestBase()
    {
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _converter = new EditorActionConverter(_keyCodeMapper);
    }

    internal void ConfigureTextInputTyping()
    {
        _ = _keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(call => 1_000 + call.Arg<char>());
        _ = _keyCodeMapper.GetCharacterForKeyCode(Arg.Any<int>(), Arg.Any<bool>()).Returns(call => (char)(call.Arg<int>() - 1_000));
        _ = _keyCodeMapper.RequiresShift(Arg.Any<char>()).Returns(returnThis: false);
        _ = _keyCodeMapper.RequiresAltGr(Arg.Any<char>()).Returns(returnThis: false);
    }

    internal static void AssertScreenTargetColor(EditorAction action, EditorActionType expectedType, string expectedVariableName)
    {
        _ = action.Type.Should().Be(expectedType);
        _ = action.TryGetScreenReadingPayload(out var payload).Should().BeTrue();
        _ = payload.ScreenTargetColorSource.Should().Be(EditorActionScreenTargetColorSource.Variable);
        _ = payload.ScreenTargetColorVariableName.Should().Be(expectedVariableName);
    }
}
