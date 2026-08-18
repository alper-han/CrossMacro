
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class EditorActionConverterTests
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly EditorActionConverter _converter;

    public EditorActionConverterTests()
    {
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _converter = new EditorActionConverter(_keyCodeMapper);
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
