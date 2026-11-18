namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorViewModelIntegrationTests
{
    [Fact]
    public void ActionListItems_WhenUsingRuntimeConverterAndValidator_RendersContextualEndsAndIndentation()
    {
        var localization = TestLocalization.Echo(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Editor_BlockName_If"] = "IfToken",
                ["Editor_BlockName_Repeat"] = "RepeatToken",
                ["Editor_Action_EndBlock"] = "End {0}",
            },
            TestLocalization.EditorActionTypeName);
        using var viewModel = CreateViewModel(BuildKeyCodeMapper(), localization, out _);

        viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        viewModel.AddAction();
        viewModel.SelectedAction = viewModel.Actions[0];
        viewModel.Picker.ActionType = EditorActionType.IfBlockStart;
        viewModel.AddAction();

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
        var keyCodeMapper = BuildKeyCodeMapper();
        _ = keyCodeMapper.GetKeyName(18).Returns("E");
        var localization = TestLocalization.Echo(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Editor_Action_KeyDown"] = "Hold '{0}'" },
            TestLocalization.EditorActionTypeName);
        using var viewModel = CreateViewModel(keyCodeMapper, localization, out _);
        var sequence = new MacroSequence
        {
            Events = { new MacroEvent { Type = EventType.KeyPress, KeyCode = 18 } },
        };

        viewModel.LoadMacroSequence(sequence);

        _ = viewModel.ActionListItems.Should().ContainSingle();
        _ = viewModel.ActionListItems[0].DisplayName.Should().Be("Hold 'E'");
        _ = viewModel.Actions[0].KeyName.Should().Be("E");
    }

    [Fact]
    public void AddAction_WhenMulDivAdded_RuntimeConverterEmitsMulDivScriptLines()
    {
        using var viewModel = CreateViewModel(BuildKeyCodeMapper(), TestLocalization.Echo(), out var converter);
        viewModel.Picker.ActionType = EditorActionType.SetVariable;
        viewModel.AddAction();
        viewModel.SelectedAction!.ScriptVariableName = "x";
        viewModel.SelectedAction.ScriptValueType = ScriptValueType.Number;
        viewModel.SelectedAction.ScriptValue = "1";

        viewModel.Picker.ActionType = EditorActionType.MultiplyVariable;
        viewModel.AddAction();
        viewModel.SelectedAction.ScriptVariableName = "x";
        viewModel.SelectedAction.ScriptNumericValue = "2";

        viewModel.Picker.ActionType = EditorActionType.DivideVariable;
        viewModel.AddAction();
        viewModel.SelectedAction.ScriptVariableName = "x";
        viewModel.SelectedAction.ScriptNumericValue = "2";

        var sequence = converter.ToMacroSequence(viewModel.Actions, "Mul Div Presentation", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal("set x 1", "mul x 2", "div x 2");
    }

    private static EditorViewModel CreateViewModel(
        IKeyCodeMapper keyCodeMapper,
        ILocalizationService localization,
        out EditorActionConverter converter)
    {
        converter = new EditorActionConverter(keyCodeMapper);
        return new EditorViewModel(
            converter,
            new EditorActionValidator(converter),
            Substitute.For<ICoordinateCaptureService>(),
            Substitute.For<IMacroFileManager>(),
            Substitute.For<IDialogService>(),
            keyCodeMapper,
            Substitute.For<IMacroPlayer>(),
            localization,
            new EditorActionDisplayFormatter(localization),
            uiDispatcher: ImmediateUiDispatcher.Instance);
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
