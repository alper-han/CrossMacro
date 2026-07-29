
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorViewModelIntegrationTests
{
    [Fact]
    public void ActionListItems_WhenUsingRuntimeConverterAndValidator_RendersContextualEndsAndIndentation()
    {
        // Arrange
        var keyCodeMapper = BuildKeyCodeMapper();
        var converter = new EditorActionConverter(keyCodeMapper);
        var validator = new EditorActionValidator(converter);
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Editor_BlockName_If" => "IfToken",
            "Editor_BlockName_Repeat" => "RepeatToken",
            _ when call.Arg<string>().StartsWith("Editor_ActionType_", StringComparison.Ordinal) => call.Arg<string>()["Editor_ActionType_".Length..],
            _ => call.Arg<string>(),
        });
        var viewModel = new EditorViewModel(
            converter,
            validator,
            Substitute.For<ICoordinateCaptureService>(),
            Substitute.For<IMacroFileManager>(),
            Substitute.For<IDialogService>(),
            keyCodeMapper,
            Substitute.For<CrossMacro.Core.Services.IMacroPlayer>(),
            localizationService,
            new EditorActionDisplayFormatter(localizationService))
        {
            // Act
            NewActionType = EditorActionType.RepeatBlockStart,
        };
        viewModel.AddAction();

        viewModel.SelectedAction = viewModel.Actions[0];
        viewModel.NewActionType = EditorActionType.IfBlockStart;
        viewModel.AddAction();

        // Assert
        _ = viewModel.ActionListItems.Should().HaveCount(4);
        _ = viewModel.ActionListItems[0].IndentLevel.Should().Be(0);
        _ = viewModel.ActionListItems[1].IndentLevel.Should().Be(1);
        _ = viewModel.ActionListItems[2].DisplayName.Should().Be("End IfToken");
        _ = viewModel.ActionListItems[2].IndentLevel.Should().Be(1);
        _ = viewModel.ActionListItems[3].DisplayName.Should().Be("End RepeatToken");
        _ = viewModel.ActionListItems[3].IndentLevel.Should().Be(0);
    }

    [Fact]
    public void LoadMacroSequence_WhenUsingRuntimeConverter_RendersRecordedKeyNames()
    {
        // Arrange
        var keyCodeMapper = BuildKeyCodeMapper();
        _ = keyCodeMapper.GetKeyName(18).Returns("E");
        var converter = new EditorActionConverter(keyCodeMapper);
        var validator = new EditorActionValidator(converter);
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.InvariantCulture);
        _ = localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Editor_Action_KeyDown" => "Hold '{0}'",
            _ when call.Arg<string>().StartsWith("Editor_ActionType_", StringComparison.Ordinal) => call.Arg<string>()["Editor_ActionType_".Length..],
            _ => call.Arg<string>(),
        });
        var viewModel = new EditorViewModel(
            converter,
            validator,
            Substitute.For<ICoordinateCaptureService>(),
            Substitute.For<IMacroFileManager>(),
            Substitute.For<IDialogService>(),
            keyCodeMapper,
            Substitute.For<CrossMacro.Core.Services.IMacroPlayer>(),
            localizationService,
            new EditorActionDisplayFormatter(localizationService));
        var sequence = new MacroSequence
        {
            Events = { new MacroEvent { Type = EventType.KeyPress, KeyCode = 18 } },
        };

        // Act
        viewModel.LoadMacroSequence(sequence);

        // Assert
        _ = viewModel.ActionListItems.Should().ContainSingle();
        _ = viewModel.ActionListItems[0].DisplayName.Should().Be("Hold 'E'");
        _ = viewModel.Actions[0].KeyName.Should().Be("E");
    }

    private static IKeyCodeMapper BuildKeyCodeMapper()
    {
        var mapper = Substitute.For<IKeyCodeMapper>();
        _ = mapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = mapper.GetKeyCode("Shift").Returns(42);
        _ = mapper.GetKeyCode("AltGr").Returns(100);
        _ = mapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(call => call.Arg<char>());
        _ = mapper.RequiresShift(Arg.Any<char>()).Returns(call => char.IsUpper(call.Arg<char>()));
        _ = mapper.RequiresAltGr(Arg.Any<char>()).Returns(returnThis: false);
        _ = mapper.GetKeyName(Arg.Any<int>()).Returns("A");
        return mapper;
    }
}
