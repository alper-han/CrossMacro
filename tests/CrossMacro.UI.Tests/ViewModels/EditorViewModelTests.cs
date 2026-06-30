using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Views.Tabs;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class EditorViewModelTests
{
    public static TheoryData<string> Task7BindingMembers => new()
    {
        nameof(EditorViewModel.ShowPixelColorFields),
        nameof(EditorViewModel.ShowWaitColorFields),
        nameof(EditorViewModel.ShowPixelSearchFields),
        nameof(EditorViewModel.ShowScreenReadingFields),
        nameof(EditorViewModel.ShowScreenReadingColorFields),
        nameof(EditorViewModel.ShowScreenReadingPointFields),
        nameof(EditorViewModel.ShowScreenReadingRawAssistance),
        nameof(EditorViewModel.ScreenReadingRawHint),
        nameof(EditorViewModel.ShowScreenReadingColorPreview),
        nameof(EditorViewModel.ScreenReadingColorPreviewHex),
        nameof(EditorViewModel.ScreenTargetColorSources),
        nameof(EditorViewModel.AvailableColorVariableNames),
        nameof(EditorViewModel.HasAvailableColorVariableNames),
        nameof(EditorViewModel.SelectedScreenTargetColorVariableSuggestion),
        nameof(EditorViewModel.ShowScreenTargetColorHexInput),
        nameof(EditorViewModel.ShowScreenTargetColorVariableInput),
        nameof(EditorViewModel.ShowScreenTargetColorVariablePicker),
        nameof(EditorViewModel.AvailableVariableNames),
        nameof(EditorViewModel.HasAvailableVariableNames),
        nameof(EditorViewModel.SelectedSetVariableSuggestion),
        nameof(EditorViewModel.SelectedIncDecVariableSuggestion),
        nameof(EditorViewModel.SelectedConditionLeftVariableSuggestion),
        nameof(EditorViewModel.SelectedConditionRightVariableSuggestion),
        nameof(EditorViewModel.SelectedForVariableSuggestion),
        nameof(EditorViewModel.ShowSetVariablePicker),
        nameof(EditorViewModel.ShowIncDecVariablePicker),
        nameof(EditorViewModel.ShowConditionLeftVariablePicker),
        nameof(EditorViewModel.ShowConditionLeftOperandTextBox),
        nameof(EditorViewModel.ShowConditionLeftColorPicker),
        nameof(EditorViewModel.ShowConditionRightVariablePicker),
        nameof(EditorViewModel.ShowConditionRightOperandTextBox),
        nameof(EditorViewModel.ShowConditionRightColorPicker),
        nameof(EditorViewModel.ShowForVariablePicker),
        nameof(EditorViewModel.ScriptConditionOperators),
        nameof(EditorViewModel.ConditionRightOperandHint),
        nameof(EditorViewModel.CanUndo),
        nameof(EditorViewModel.CanRedo),
        nameof(EditorViewModel.CaptureMouseAsync),
        nameof(EditorViewModel.CaptureTargetColorAsync),
        nameof(EditorViewModel.CaptureConditionLeftColorAsync),
        nameof(EditorViewModel.CaptureConditionRightColorAsync),
        nameof(EditorViewModel.CapturePixelSearchTopLeftAsync),
        nameof(EditorViewModel.CapturePixelSearchBottomRightAsync),
        nameof(EditorViewModel.CancelCapture)
    };

    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly ILocalizationService _localizationService;
    private readonly IScreenPixelReader _screenPixelReader;
    private readonly IMacroPlayer _macroPlayer;
    private readonly EditorViewModel _viewModel;

    public EditorViewModelTests()
    {
        _converter = Substitute.For<IEditorActionConverter>();
        _validator = Substitute.For<IEditorActionValidator>();
        _captureService = Substitute.For<ICoordinateCaptureService>();
        _fileManager = Substitute.For<IMacroFileManager>();
        _dialogService = Substitute.For<IDialogService>();
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _localizationService = Substitute.For<ILocalizationService>();
        _screenPixelReader = Substitute.For<IScreenPixelReader>();
        _macroPlayer = Substitute.For<IMacroPlayer>();
        _keyCodeMapper.GetKeyName(Arg.Any<int>()).Returns("A");
        _screenPixelReader.IsSupported.Returns(true);
        _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.InvariantCulture);
        _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Editor_DefaultMacroName" => "[Editor_DefaultMacroName]",
            "Editor_StatusReady" => "[Editor_StatusReady]",
            "Editor_StatusAddedAction" => "[Editor_StatusAddedAction] {0}",
            "Editor_StatusRemovedAction" => "[Editor_StatusRemovedAction]",
            "Editor_StatusRemovedSelectedActions" => "[Editor_StatusRemovedSelectedActions]",
            "Editor_StatusDuplicatedSelectedActions" => "[Editor_StatusDuplicatedSelectedActions]",
            "Editor_StatusMovedSelectedActionsUp" => "[Editor_StatusMovedSelectedActionsUp]",
            "Editor_StatusMovedSelectedActionsDown" => "[Editor_StatusMovedSelectedActionsDown]",
            "Editor_StatusDeletedHiddenEvents" => "[Editor_StatusDeletedHiddenEvents]",
            "Editor_StatusNoHiddenEventsToDelete" => "[Editor_StatusNoHiddenEventsToDelete]",
            "Editor_SimplifiedMovementHint" => "[Editor_SimplifiedMovementHint] {0}",
            "Editor_StatusUndone" => "[Editor_StatusUndone]",
            "Editor_StatusRedone" => "[Editor_StatusRedone]",
            "Editor_StatusCaptureSelectionChanged" => "[Editor_StatusCaptureSelectionChanged]",
            "Editor_StatusInsertedElseBlock" => "[Editor_StatusInsertedElseBlock]",
            "Editor_StatusOperationBlocked" => "[Editor_StatusOperationBlocked]",
            "Editor_StatusPixelReaderUnavailable" => "[Editor_StatusPixelReaderUnavailable]",
            "Editor_StatusCaptureColorPrompt" => "[Editor_StatusCaptureColorPrompt]",
            "Editor_StatusCaptureColorFailed" => "[Editor_StatusCaptureColorFailed] {0}",
            "Editor_StatusCapturedColor" => "[Editor_StatusCapturedColor] {0} {1} {2}",
            "Editor_StatusCapturedRegionTopLeft" => "[Editor_StatusCapturedRegionTopLeft] {0} {1}",
            "Editor_StatusCapturedRegionBottomRight" => "[Editor_StatusCapturedRegionBottomRight] {0} {1}",
            "Editor_StatusCaptureRegionInvalidBottomRight" => "[Editor_StatusCaptureRegionInvalidBottomRight]",
            "Editor_StatusRemovedBlock" => "[Editor_StatusRemovedBlock]",
            "Editor_StatusValidationFailed" => "[Editor_StatusValidationFailed]",
            "Editor_DialogTitleNoActions" => "[Editor_DialogTitleNoActions]",
            "Editor_DialogMessageNoActions" => "[Editor_DialogMessageNoActions]",
            "Editor_DialogTitleValidationErrors" => "[Editor_DialogTitleValidationErrors]",
            "Editor_ValidationErrorHeader" => "[Editor_ValidationErrorHeader]",
            "Editor_DialogButtonOk" => "[Editor_DialogButtonOk]",
            "Editor_CurrentPositionClick" => "[Editor_CurrentPositionClick]",
            "Editor_CurrentPositionHold" => "[Editor_CurrentPositionHold]",
            "Editor_CurrentPositionRelease" => "[Editor_CurrentPositionRelease]",
            "Editor_CurrentPositionUse" => "[Editor_CurrentPositionUse]",
            "Editor_TextInputEscapedControlHint" => "[Editor_TextInputEscapedControlHint]",
            "Editor_Action_TextInput" => "Type \"{0}\"",
            "Editor_BlockName_If" => "IfToken",
            "Editor_BlockName_Repeat" => "RepeatToken",
            "Editor_BlockName_Else" => "ElseToken",
            "Editor_BlockName_While" => "WhileToken",
            "Editor_BlockName_For" => "ForToken",
            "Editor_BlockName_Block" => "BlockToken",
            _ when call.Arg<string>().StartsWith("Editor_ActionType_") => call.Arg<string>()["Editor_ActionType_".Length..],
            _ => call.Arg<string>()
        });

        _validator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>()).Returns((true, new List<string>()));

        _viewModel = new EditorViewModel(
            _converter,
            _validator,
            _captureService,
            _fileManager,
            _dialogService,
            _keyCodeMapper,
            _macroPlayer,
            _localizationService,
            new EditorActionDisplayFormatter(_localizationService),
            _screenPixelReader);
    }

    [Theory]
    [MemberData(nameof(Task7BindingMembers))]
    public void Task7BindingMembers_RemainPublic(string memberName)
    {
        typeof(EditorViewModel)
            .GetMember(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Should()
            .NotBeEmpty();
    }

    [Fact]
    public void SelectedActionDisplayText_ForTextInput_PreservesMultilineText()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "\basd\r\nasd\t\\"
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Act / Assert
        _viewModel.SelectedActionDisplayText.Should().Be("\basd\r\nasd\t\\");
    }

    [Fact]
    public void SelectedActionDisplayText_ForTextInput_SetsRawMultilineText()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.TextInput };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Act
        _viewModel.SelectedActionDisplayText = "first line\nsecond line\t\\";

        // Assert
        action.Text.Should().Be("first line\nsecond line\t\\");
    }

    [Fact]
    public void TextInputAcceptsReturn_WhenSelectedActionIsTextInput_ReturnsTrue()
    {
        var action = new EditorAction { Type = EditorActionType.TextInput };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.TextInputAcceptsReturn.Should().BeTrue();
    }

    [Fact]
    public void TextInputAcceptsReturn_WhenSelectedActionIsNonTextPayload_ReturnsFalse()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.TextInputAcceptsReturn.Should().BeFalse();
    }

    [Fact]
    public void SelectedActionDisplayText_WhenSelectedActionChanges_RaisesPropertyChanged()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "\b"
        };
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // Act
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Assert
        changed.Should().Contain(nameof(EditorViewModel.SelectedActionDisplayText));
        _viewModel.SelectedActionDisplayText.Should().Be("\b");
    }

    [Fact]
    public void AddAction_AddsActionAndSelectsIt()
    {
        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.HasActions.Should().BeTrue();
        _viewModel.Status.Should().Contain("[Editor_StatusAddedAction]");
    }

    [Theory]
    [InlineData(EditorActionType.PixelColor)]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void AddAction_ForScreenReadingActions_InitializesStructuredFields(EditorActionType actionType)
    {
        _viewModel.NewActionType = actionType;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        action.Type.Should().Be(actionType);
        action.ScreenColorHex.Should().Be("FFFFFF");
        action.ScreenColorVariableName.Should().Be(actionType == EditorActionType.WaitColor ? "wait_ok" : "color");
        action.ScreenFoundXVariableName.Should().Be("found_x");
        action.ScreenFoundYVariableName.Should().Be("found_y");
        action.ScreenTimeoutMs.Should().Be(5000);
        action.ScreenTolerance.Should().Be(0);
        action.ScreenWidth.Should().Be(actionType == EditorActionType.PixelSearch ? 1920 : 1);
        action.ScreenHeight.Should().Be(actionType == EditorActionType.PixelSearch ? 1080 : 1);
        _viewModel.SelectedAction.Should().BeSameAs(action);
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void AddAction_ForTargetColorActions_DefaultsTargetColorSourceToManualAndKeepsDefaultHex(EditorActionType actionType)
    {
        _viewModel.NewActionType = actionType;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        action.ScreenColorHex.Should().Be("FFFFFF");
        action.ScreenTargetColorSource.Should().Be(EditorActionScreenTargetColorSource.ManualHex);
    }

    [Theory]
    [InlineData(EditorActionType.PixelColor, true, false, false, true, false, true)]
    [InlineData(EditorActionType.WaitColor, false, true, false, true, true, true)]
    [InlineData(EditorActionType.PixelSearch, false, false, true, true, true, false)]
    public void SelectedAction_ForScreenReadingActions_ExposesMatchingEditorPanel(
        EditorActionType actionType,
        bool showPixelColor,
        bool showWaitColor,
        bool showPixelSearch,
        bool showScreenReadingFields,
        bool showScreenReadingColorFields,
        bool showScreenReadingPointFields)
    {
        var action = new EditorAction { Type = actionType, ScreenColorHex = "A1B2C3" };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _viewModel.ShowPixelColorFields.Should().Be(showPixelColor);
        _viewModel.ShowWaitColorFields.Should().Be(showWaitColor);
        _viewModel.ShowPixelSearchFields.Should().Be(showPixelSearch);
        _viewModel.ShowScreenReadingFields.Should().Be(showScreenReadingFields);
        _viewModel.ShowScreenReadingColorFields.Should().Be(showScreenReadingColorFields);
        _viewModel.ShowScreenReadingPointFields.Should().Be(showScreenReadingPointFields);
        _viewModel.ShowScreenReadingColorPreview.Should().Be(actionType is EditorActionType.WaitColor or EditorActionType.PixelSearch);
        _viewModel.ScreenReadingColorPreviewHex.Should().Be(actionType == EditorActionType.PixelColor ? string.Empty : "A1B2C3");
        _viewModel.ShowScreenReadingRawAssistance.Should().BeFalse();
        _viewModel.ScreenReadingRawHint.Should().BeEmpty();
    }

    [Fact]
    public void ScreenReadingFields_WhenSelectedActionIsNotScreenReading_AreHidden()
    {
        var action = new EditorAction { Type = EditorActionType.TextInput };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _viewModel.ShowScreenReadingFields.Should().BeFalse();
    }

    [Fact]
    public void ScreenReadingColorPreview_WhenSelectedColorChanges_NormalizesAndNotifies()
    {
        var action = new EditorAction { Type = EditorActionType.WaitColor, ScreenColorHex = "ffffff" };
        var changed = new List<string?>();
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        action.ScreenColorHex = "12abef";

        _viewModel.ScreenReadingColorPreviewHex.Should().Be("12ABEF");
        _viewModel.ShowScreenReadingColorPreview.Should().BeTrue();
        changed.Should().Contain(nameof(EditorViewModel.ScreenReadingColorPreviewHex));
        changed.Should().Contain(nameof(EditorViewModel.ShowScreenReadingColorPreview));
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void ScreenTargetColorSource_WhenVariableOrManualChanges_TogglesHexAndVariableVisibility(EditorActionType actionType)
    {
        var action = new EditorAction
        {
            Type = actionType,
            ScreenColorHex = "FFFFFF"
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        action.ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable;

        _viewModel.ShowScreenTargetColorHexInput.Should().BeFalse();
        _viewModel.ShowScreenTargetColorVariableInput.Should().BeTrue();
        _viewModel.ShowScreenReadingColorPreview.Should().BeFalse();

        action.ScreenTargetColorSource = EditorActionScreenTargetColorSource.ManualHex;

        _viewModel.ShowScreenTargetColorHexInput.Should().BeTrue();
        _viewModel.ShowScreenTargetColorVariableInput.Should().BeFalse();
        _viewModel.ShowScreenReadingColorPreview.Should().BeTrue();
    }

    [Theory]
    [InlineData("pixelcolor rel 1 2 sampled", "Editor_RawScreenReadingHint_PixelColor", "localized pixelcolor hint")]
    [InlineData("waitcolor 10 20 00FF00 1000 wait_ok", "Editor_RawScreenReadingHint_WaitColor", "localized waitcolor hint")]
    [InlineData("pixelsearch 0 0 100 100 00FF00 found", "Editor_RawScreenReadingHint_PixelSearch", "localized pixelsearch hint")]
    public void ScreenReadingRawHint_ForScreenReadingRawScript_UsesLocalizedHint(
        string rawStep,
        string resourceKey,
        string expectedHint)
    {
        _localizationService[resourceKey].Returns(expectedHint);
        var action = new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = rawStep
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.ShowScreenReadingRawAssistance.Should().BeTrue();
        _viewModel.ScreenReadingRawHint.Should().Be(expectedHint);
    }

    [Fact]
    public void ScriptConditionOperators_WhenOperandIsColor_FiltersToEqualityOperatorsAndNormalizesSelection()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "color",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "10"
        };
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.PixelColor, ScreenColorVariableName = "color" });
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _localizationService["Editor_ConditionColorHint"].Returns("Use Color operand.");

        action.ScriptRightOperandType = ScriptOperandType.Color;

        action.ScriptConditionOperator.Should().Be(ScriptConditionOperator.Equals);
        _viewModel.ScriptConditionOperators.Should().Equal(
            ScriptConditionOperator.Equals,
            ScriptConditionOperator.NotEquals);
        _viewModel.ConditionRightOperandHint.Should().Contain("Color");
    }

    [Fact]
    public void ScriptConditionOperators_WhenVariableCanBeNumeric_AllowsNumericOperators()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "found_x",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "10"
        };
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y"
        });
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.ScriptConditionOperators.Should().Contain(ScriptConditionOperator.GreaterThan);
        action.ScriptConditionOperator.Should().Be(ScriptConditionOperator.GreaterThan);
    }

    [Fact]
    public void ScriptConditionOperators_WhenBooleanVariableSelected_FiltersToEqualityOperators()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "found",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Boolean,
            ScriptRightOperand = "true"
        };
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y"
        });
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        action.ScriptConditionOperator.Should().Be(ScriptConditionOperator.Equals);
        _viewModel.ScriptConditionOperators.Should().Equal(
            ScriptConditionOperator.Equals,
            ScriptConditionOperator.NotEquals);
    }

    [Fact]
    public void ConditionOperandTextBoxes_WhenVariablePickerIsAvailable_AreHiddenForVariableOperands()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "color",
            ScriptRightOperandType = ScriptOperandType.VariableReference
        };
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.PixelColor, ScreenColorVariableName = "color" });
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.ShowConditionLeftVariablePicker.Should().BeTrue();
        _viewModel.ShowConditionLeftOperandTextBox.Should().BeFalse();
        _viewModel.SelectedConditionLeftVariableSuggestion.Should().Be("color");
        _viewModel.ShowConditionRightVariablePicker.Should().BeTrue();
        _viewModel.ShowConditionRightOperandTextBox.Should().BeFalse();
    }

    [Fact]
    public void ConditionOperandTextBoxes_WhenNoVariablesExist_RemainVisibleForManualVariableEntry()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptRightOperandType = ScriptOperandType.VariableReference
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.ShowConditionLeftVariablePicker.Should().BeFalse();
        _viewModel.ShowConditionLeftOperandTextBox.Should().BeTrue();
        _viewModel.ShowConditionRightVariablePicker.Should().BeFalse();
        _viewModel.ShowConditionRightOperandTextBox.Should().BeTrue();
    }

    [Fact]
    public void ConditionColorPickers_WhenColorOperandsSelected_AreVisibleWithManualTextBoxes()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Color,
            ScriptRightOperandType = ScriptOperandType.Color
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.ShowConditionLeftOperandTextBox.Should().BeTrue();
        _viewModel.ShowConditionRightOperandTextBox.Should().BeTrue();
        _viewModel.ShowConditionLeftColorPicker.Should().BeTrue();
        _viewModel.ShowConditionRightColorPicker.Should().BeTrue();
    }

    [Fact]
    public void ConditionColorPickers_WhenOperandsAreNotColor_AreHidden()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Text,
            ScriptRightOperandType = ScriptOperandType.VariableReference
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.ShowConditionLeftColorPicker.Should().BeFalse();
        _viewModel.ShowConditionRightColorPicker.Should().BeFalse();
    }

    [Fact]
    public void CultureChanged_RefreshesLocalizedComputedPropertiesAndActionListPresentation()
    {
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();

        _localizationService["Editor_CurrentPositionUse"].Returns("[Editor_CurrentPositionUse:updated]");
        _localizationService["Editor_TextToType"].Returns("[Editor_TextToType:updated]");
        _localizationService["Editor_EnterTextToType"].Returns("[Editor_EnterTextToType:updated]");
        _localizationService["Editor_TextToTypeHint"].Returns("[Editor_TextToTypeHint:updated] {0}");
        _localizationService["Editor_BlockName_If"].Returns("IfTokenUpdated");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.CurrentPositionToggleLabel.Should().Be("[Editor_CurrentPositionUse:updated]");
        _viewModel.TextInputLabel.Should().Be("[Editor_TextToType:updated]");
        _viewModel.TextInputWatermark.Should().Be("[Editor_EnterTextToType:updated]");
        _viewModel.TextInputHint.Should().Contain("[Editor_TextToTypeHint:updated]");
        _viewModel.ActionListItems[1].DisplayName.Should().Be("End IfTokenUpdated");
    }

    [Fact]
    public void CultureChanged_RefreshesLocalizedRawScreenReadingHint()
    {
        var rawAction = new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = "waitcolor 10 20 00FF00 1000 wait_ok"
        };
        _localizationService["Editor_RawScreenReadingHint_WaitColor"].Returns("initial raw hint");
        _viewModel.Actions.Add(rawAction);
        _viewModel.SelectedAction = rawAction;
        _viewModel.ScreenReadingRawHint.Should().Be("initial raw hint");
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _localizationService["Editor_RawScreenReadingHint_WaitColor"].Returns("updated raw hint");
        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.ScreenReadingRawHint.Should().Be("updated raw hint");
        changed.Should().Contain(nameof(EditorViewModel.ScreenReadingRawHint));

    }

    [Fact]
    public void CultureChanged_RefreshesLocalizedConditionHintNotification()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Number,
            ScriptRightOperandType = ScriptOperandType.Number
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        action.ScriptRightOperandType = ScriptOperandType.Color;
        _localizationService["Editor_ConditionColorHint"].Returns("updated condition hint");
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.ConditionRightOperandHint.Should().Be("updated condition hint");
        changed.Should().Contain(nameof(EditorViewModel.ConditionRightOperandHint));
    }

    [Fact]
    public void CultureChanged_WhenMovementRunIsCondensed_RefreshesCondensedDisplayAndHint()
    {
        for (var index = 0; index < 6; index++)
        {
            _viewModel.Actions.Add(new EditorAction
            {
                Type = EditorActionType.MouseMove,
                X = index,
                Y = index + 1
            });
        }

        _viewModel.SimplifyMovement = true;
        var originalDisplay = _viewModel.ActionListItems[0].DisplayName;
        var originalHint = _viewModel.ActionListItems[0].CondensedHint;

        _localizationService["Editor_Action_MouseMoveAbsolute"].Returns("Mouvement vers ({0}, {1})");
        _localizationService["Editor_SimplifiedMovementHint"].Returns("{0} actions de mouvement masquées");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.ActionListItems[0].DisplayName.Should().NotBe(originalDisplay);
        _viewModel.ActionListItems[0].CondensedHint.Should().NotBe(originalHint);
        _viewModel.ActionListItems[0].DisplayName.Should().Be("Mouvement vers (5, 6)");
        _viewModel.ActionListItems[0].CondensedHint.Should().Be("5 actions de mouvement masquées");
        _viewModel.ActionListItems[0].DisplayTooltip.Should().Be("5 actions de mouvement masquées");
    }

    [Fact]
    public void CultureChanged_WhenReadyStatusDisplayed_RebuildsReadyStatusInNewLanguage()
    {
        _localizationService["Editor_StatusReady"].Returns("[Editor_StatusReady:updated]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.Status.Should().Be("[Editor_StatusReady:updated]");
    }

    [Fact]
    public void AddableActionTypes_HidesManagedBlockTokens()
    {
        _viewModel.AddableActionTypes.Should().NotContain(EditorActionType.BlockEnd);
        _viewModel.AddableActionTypes.Should().NotContain(EditorActionType.ElseBlockStart);
        _viewModel.AddableActionTypes.Should().NotContain(EditorActionType.RawScriptStep);
    }

    [Fact]
    public void AddableActionTypes_ContainsLoopControlActions()
    {
        _viewModel.AddableActionTypes.Should().Contain(EditorActionType.Break);
        _viewModel.AddableActionTypes.Should().Contain(EditorActionType.Continue);
    }

    [Fact]
    public void ActionListItems_ByDefault_ShowsMouseMovesAndShortDelays()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });

        _viewModel.HideMouseMoves.Should().BeFalse();
        _viewModel.HideShortWaits.Should().BeFalse();
        _viewModel.HiddenEventCount.Should().Be(0);
        _viewModel.HasHiddenEvents.Should().BeFalse();
        _viewModel.ActionListItems.Should().HaveCount(3);
        _viewModel.ActionListItems[0].IsNoise.Should().BeTrue();
        _viewModel.ActionListItems[1].IsNoise.Should().BeTrue();
        _viewModel.ActionListItems[2].IsNoise.Should().BeFalse();
    }

    [Fact]
    public void ActionListItems_WhenMovementAndShortWaitsHidden_ExcludesFilteredRows()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });

        HideMovementAndShortWaitRows();

        _viewModel.HiddenEventCount.Should().Be(2);
        _viewModel.HasHiddenEvents.Should().BeTrue();
        _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.ActionListItems[0].Action.Type.Should().Be(EditorActionType.Delay);
        _viewModel.ActionListItems[0].Action.DelayMs.Should().Be(20);
    }

    [Fact]
    public void ActionListItems_ByDefault_ProjectsAllRowsAndPreservesActionReferences()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(delay);
        _viewModel.Actions.Add(click);

        _viewModel.ActionListItems.Should().HaveCount(3);
        _viewModel.ActionListItems.Select(item => item.Action).Should().Equal(move, delay, click);
        _viewModel.ActionListItems.Should().OnlyContain(item => item.RepresentsSourceAction);
    }

    [Fact]
    public void ActionListItems_ForTextInput_EscapesControlCharactersInDisplayName()
    {
        // Arrange
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "\basd\r\nasd\t"
        });

        // Act / Assert
        _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.ActionListItems[0].DisplayName.Should().Be("Type \"⌫asd↵↵asd⇥\"");
    }

    [Theory]
    [InlineData(EditorActionType.MouseClick, true, "Editor_Action_MouseClickAbsolute")]
    [InlineData(EditorActionType.MouseClick, false, "Editor_Action_MouseClickRelative")]
    [InlineData(EditorActionType.MouseDown, true, "Editor_Action_MouseDownAbsolute")]
    [InlineData(EditorActionType.MouseDown, false, "Editor_Action_MouseDownRelative")]
    [InlineData(EditorActionType.MouseUp, true, "Editor_Action_MouseUpAbsolute")]
    [InlineData(EditorActionType.MouseUp, false, "Editor_Action_MouseUpRelative")]
    public void ActionListItems_ForCoordinateMouseButtonActions_ShowsCoordinateModeAndPosition(
        EditorActionType actionType,
        bool isAbsolute,
        string expectedResourceKey)
    {
        var action = new EditorAction
        {
            Type = actionType,
            IsAbsolute = isAbsolute,
            X = isAbsolute ? 100 : 5,
            Y = isAbsolute ? 200 : -3
        };

        _viewModel.Actions.Add(action);

        _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.ActionListItems[0].DisplayName.Should().Be(expectedResourceKey);
    }

    [Theory]
    [InlineData(EditorActionType.MouseDown, "Editor_Action_MouseDownCurrent")]
    [InlineData(EditorActionType.MouseUp, "Editor_Action_MouseUpCurrent")]
    public void ActionListItems_ForCurrentPositionMouseButtonActions_ShowsCurrentPositionMode(
        EditorActionType actionType,
        string expectedResourceKey)
    {
        _viewModel.Actions.Add(new EditorAction
        {
            Type = actionType,
            UseCurrentPosition = true,
            IsAbsolute = false
        });

        _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.ActionListItems[0].DisplayName.Should().Be(expectedResourceKey);
    }

    [Fact]
    public void ActionListItems_WhenNoiseVisible_ExposeZeroBasedUnderlyingIndexAndOneBasedIndex()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });

        _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(0, 1, 2);
        _viewModel.ActionListItems.Select(item => item.Index).Should().Equal(1, 2, 3);
        _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().Equal(0, 0, 0);
    }

    [Fact]
    public void ActionListItems_WhenMovementAndShortWaitsHidden_PreservesOriginalIndicesAndLeavesCondensedHiddenCountZero()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 8 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });

        HideMovementAndShortWaitRows();

        _viewModel.HiddenEventCount.Should().Be(3);
        _viewModel.ActionListItems.Should().HaveCount(2);
        _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(2, 4);
        _viewModel.ActionListItems.Select(item => item.Index).Should().Equal(3, 5);
        _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().Equal(0, 0);
    }

    [Fact]
    public void SelectedActionUnderlyingIndices_WhenRowsSelected_StoresSourceOrderAndKeepsPrimarySelectionSingle()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(click);
        _viewModel.Actions.Add(delay);

        _viewModel.SelectedActionUnderlyingIndices.Add(2);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _viewModel.SelectedAction.Should().BeSameAs(move);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
        _viewModel.HasSelectedAction.Should().BeTrue();
        _viewModel.HasSelectedActions.Should().BeTrue();
        _viewModel.SelectedActionCount.Should().Be(2);
    }

    [Fact]
    public void ReplaceSelectedActionUnderlyingIndices_WhenRowsSelectedFromListBox_KeepsClickedRowSelected()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(click);
        _viewModel.Actions.Add(delay);

        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 1 });

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _viewModel.SelectedAction.Should().BeSameAs(click);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[1]);
        _viewModel.HasSelectedAction.Should().BeTrue();
        _viewModel.HasSelectedActions.Should().BeTrue();
    }

    [Fact]
    public void ReplaceSelectedActionUnderlyingIndices_WhenRowsSelectedFromListBox_ReplacesWithoutIntermediateClearNotification()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var observedSelectedActions = new List<EditorAction?>();

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(click);
        _viewModel.Actions.Add(delay);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EditorViewModel.SelectedAction))
            {
                observedSelectedActions.Add(_viewModel.SelectedAction);
            }
        };

        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 1, 2 });

        observedSelectedActions.Should().NotContainNulls();
        observedSelectedActions.Should().ContainSingle().Which.Should().BeSameAs(click);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1, 2);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[1]);
    }

    [Fact]
    public void SelectedAction_WhenSetDirectly_ReplacesBatchSelectionWithPrimaryUnderlyingIndex()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(click);
        _viewModel.Actions.Add(delay);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        _viewModel.SelectedAction = click;

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _viewModel.SelectedAction.Should().BeSameAs(click);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[1]);
    }

    [Fact]
    public void SelectedActionUnderlyingIndices_WhenProjectionHidesPrimaryRow_SelectsFirstVisibleSelectedAction()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(delay);
        _viewModel.Actions.Add(click);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        HideMovementAndShortWaitRows();

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _viewModel.SelectedAction.Should().BeSameAs(click);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
    }

    [Fact]
    public void SelectedActionUnderlyingIndices_WhenAllSelectedRowsHidden_ClearsPrimarySelectionOnly()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(delay);
        _viewModel.Actions.Add(click);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);

        HideMovementAndShortWaitRows();

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.HasSelectedActions.Should().BeTrue();
        _viewModel.SelectedActionCount.Should().Be(2);
    }

    [Fact]
    public void SelectedActionCommandStateProperties_ReflectSelectedUnderlyingIndices()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });

        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.SelectedActionCount.Should().Be(0);
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.CanDuplicateSelectedActions.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();

        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        _viewModel.HasSelectedActions.Should().BeTrue();
        _viewModel.SelectedActionCount.Should().Be(2);
        _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _viewModel.CanMoveSelectedActionsUp.Should().BeTrue();
        _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
    }

    [Fact]
    public void SelectedActionCommandStateProperties_ForZeroOneAndMultipleSelections_UpdateAndNotify()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });
        var notifications = new List<string>();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != null)
            {
                notifications.Add(args.PropertyName);
            }
        };

        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.CanDuplicateSelectedActions.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();

        _viewModel.SelectedActionUnderlyingIndices.Add(1);

        _viewModel.SelectedActionCount.Should().Be(1);
        _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _viewModel.CanMoveSelectedActionsUp.Should().BeTrue();
        _viewModel.CanMoveSelectedActionsDown.Should().BeTrue();
        notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsUp));
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));

        notifications.Clear();
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        _viewModel.SelectedActionCount.Should().Be(2);
        _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _viewModel.CanMoveSelectedActionsUp.Should().BeTrue();
        _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));

        notifications.Clear();
        _viewModel.SelectedActionUnderlyingIndices.Clear();

        _viewModel.SelectedActionCount.Should().Be(0);
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.CanDuplicateSelectedActions.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
        notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsUp));
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));
    }

    [Fact]
    public void SelectedActionCommandStateProperties_WhenActionsCollectionChanges_NormalizeAndNotify()
    {
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 10, Y = 10 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        var notifications = new List<string>();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != null)
            {
                notifications.Add(args.PropertyName);
            }
        };

        _viewModel.Actions.RemoveAt(1);

        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.CanDuplicateSelectedActions.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
        notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsUp));
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));
    }

    [Fact]
    public void SelectedActionCommandStateProperties_WhenProjectionTogglesChange_NotifyAndPreserveBatchSelection()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };
        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(click);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        var notifications = new List<string>();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != null)
            {
                notifications.Add(args.PropertyName);
            }
        };

        HideMovementAndShortWaitRows();

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _viewModel.SelectedAction.Should().BeSameAs(click);
        _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
        notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsUp));
        notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));

        notifications.Clear();
        _viewModel.HideShortWaits = false;
        _viewModel.SimplifyMovement = true;

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
    }

    [Fact]
    public void DuplicateSelectedActions_WhenSingleRowSelected_DuplicatesSelectionAndSupportsUndo()
    {
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);

        _viewModel.DuplicateSelectedActions();

        _viewModel.Actions.Should().HaveCount(3);
        _viewModel.Actions[0].Should().BeSameAs(first);
        _viewModel.Actions[1].Should().NotBeSameAs(first);
        _viewModel.Actions[1].Type.Should().Be(first.Type);
        _viewModel.Actions[2].Should().BeSameAs(second);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[1]);
        _viewModel.Status.Should().Be("[Editor_StatusDuplicatedSelectedActions]");

        _viewModel.Undo();

        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.Actions.Select(action => action.Type).Should().Equal(EditorActionType.MouseClick, EditorActionType.Delay);
        _viewModel.Status.Should().Be("[Editor_StatusUndone]");
    }

    [Fact]
    public void MoveSelectedActions_WhenSingleRowSelected_MovesSelectionAndSupportsUndo()
    {
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);

        _viewModel.MoveSelectedActionsUp();

        _viewModel.Actions.Should().Equal(second, first, third);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _viewModel.SelectedAction.Should().BeSameAs(second);
        _viewModel.Status.Should().Be("[Editor_StatusMovedSelectedActionsUp]");

        _viewModel.MoveSelectedActionsDown();

        _viewModel.Actions.Should().Equal(first, second, third);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _viewModel.SelectedAction.Should().BeSameAs(second);
        _viewModel.Status.Should().Be("[Editor_StatusMovedSelectedActionsDown]");

        _viewModel.Undo();

        _viewModel.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.Delay,
            EditorActionType.MouseClick,
            EditorActionType.KeyPress);
        _viewModel.Status.Should().Be("[Editor_StatusUndone]");
    }

    [Fact]
    public void FilterToggleVisibility_HidesUnavailableInactiveFiltersAndKeepsActiveFiltersVisible()
    {
        _viewModel.ShowHideMouseMovesToggle.Should().BeFalse();
        _viewModel.ShowHideShortWaitsToggle.Should().BeFalse();
        _viewModel.ShowSimplifyMovementToggle.Should().BeFalse();

        _viewModel.HideMouseMoves = true;
        _viewModel.HideShortWaits = true;
        _viewModel.SimplifyMovement = true;

        _viewModel.ShowHideMouseMovesToggle.Should().BeTrue();
        _viewModel.ShowHideShortWaitsToggle.Should().BeTrue();
        _viewModel.ShowSimplifyMovementToggle.Should().BeTrue();
    }

    [Fact]
    public void FilterToggleVisibility_ShowsOnlyFiltersWithEligibleEvents()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 1 });
        _viewModel.ShowHideMouseMovesToggle.Should().BeTrue();
        _viewModel.ShowHideShortWaitsToggle.Should().BeFalse();
        _viewModel.ShowSimplifyMovementToggle.Should().BeFalse();

        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 5 });
        _viewModel.ShowHideShortWaitsToggle.Should().BeTrue();
        _viewModel.ShowSimplifyMovementToggle.Should().BeFalse();

        AddCondensibleRun(_viewModel, 6);

        _viewModel.ShowSimplifyMovementToggle.Should().BeTrue();
    }

    [Fact]
    public void FilterToggleVisibility_UpdatesWhenActionEligibilityChanges()
    {
        var action = new EditorAction { Type = EditorActionType.Delay, DelayMs = 10 };
        _viewModel.Actions.Add(action);
        _viewModel.ShowHideShortWaitsToggle.Should().BeFalse();

        action.DelayMs = 5;
        _viewModel.ShowHideShortWaitsToggle.Should().BeTrue();

        action.UseRandomDelay = true;
        _viewModel.ShowHideShortWaitsToggle.Should().BeFalse();
    }

    [Fact]
    public void ShowDeleteHiddenEvents_RequiresActiveHideFilterAndHiddenCandidates()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 1 });

        _viewModel.CanDeleteHiddenEvents.Should().BeFalse();
        _viewModel.ShowDeleteHiddenEvents.Should().BeFalse();

        _viewModel.HideMouseMoves = true;

        _viewModel.ShowDeleteHiddenEvents.Should().BeTrue();
    }

    [Fact]
    public void ReplaceSelectedActionUnderlyingIndices_WhenSelectionCleared_ClearsSelectedActionAndHidesProperties()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        _viewModel.Actions.Add(action);
        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 0 });

        _viewModel.SelectedAction.Should().BeSameAs(action);
        _viewModel.SelectedActionListItem.Should().NotBeNull();
        _viewModel.ShowSingleSelectedActionProperties.Should().BeTrue();

        _viewModel.ReplaceSelectedActionUnderlyingIndices(Array.Empty<int>());

        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.HasSelectedAction.Should().BeFalse();
        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
    }

    [Fact]
    public void TryDeselectSelectedSourceAction_WhenSelectedSourceRowClicked_RemovesRowFromListBoxSelection()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var row = CreateActionListItem(action, representsSourceAction: true);
        var listBox = new ListBox { SelectionMode = SelectionMode.Multiple };
        listBox.SelectedItems!.Add(row);

        var removed = ListBoxSelectedActionIndices.TryDeselectSelectedSourceAction(listBox, row);

        removed.Should().BeTrue();
        listBox.SelectedItems!.Cast<object>().Should().BeEmpty();
    }

    [Fact]
    public void TryDeselectSelectedSourceAction_WhenRowIsNotSourceAction_DoesNotChangeSelection()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var row = CreateActionListItem(action, representsSourceAction: false);
        var listBox = new ListBox { SelectionMode = SelectionMode.Multiple };
        listBox.SelectedItems!.Add(row);

        var removed = ListBoxSelectedActionIndices.TryDeselectSelectedSourceAction(listBox, row);

        removed.Should().BeFalse();
        listBox.SelectedItems!.Cast<object>().Should().ContainSingle().Which.Should().BeSameAs(row);
    }

    [Fact]
    public void SelectedActionUnderlyingIndices_WhenSelectedActionEditRebuildsRows_PreservesSelectedAction()
    {
        var first = new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 1 };
        var selected = new EditorAction { Type = EditorActionType.MouseClick, X = 2, Y = 2 };
        var third = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(selected);
        _viewModel.Actions.Add(third);
        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 1 });
        var previousSelectedRow = _viewModel.ActionListItems[1];

        selected.X = 25;
        var currentSelectedRow = _viewModel.ActionListItems[1];

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _viewModel.SelectedAction.Should().BeSameAs(selected);
        _viewModel.SelectedActionListItem.Should().BeSameAs(currentSelectedRow);
        _viewModel.SelectedActionListItem.Should().NotBeSameAs(previousSelectedRow);
    }

    [Fact]
    public void SelectedActionUnderlyingIndices_WhenMultipleSelectedRowsRebuild_PreservesSelections()
    {
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.MouseClick, X = 3, Y = 3 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 0, 2 });

        first.X = 11;

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _viewModel.SelectedAction.Should().BeSameAs(first);
        _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(0, 1, 2);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
    }

    [Fact]
    public void ReplaceSelectedActionUnderlyingIndices_WhenVisibleRowRemovedFromListSelection_WritesBackSubsetSelection()
    {
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.MouseClick, X = 3, Y = 3 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 0, 2 });

        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 0 });

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _viewModel.SelectedAction.Should().BeSameAs(first);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
    }

    [Fact]
    public void SelectedActionUnderlyingIndices_WhenSelectedRowsAreHidden_DoesNotClearUnderlyingSelection()
    {
        var hiddenMove = new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 1 };
        var visibleClick = new EditorAction { Type = EditorActionType.MouseClick, X = 2, Y = 2 };
        _viewModel.Actions.Add(hiddenMove);
        _viewModel.Actions.Add(visibleClick);
        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 0 });

        _viewModel.HideMouseMoves = true;

        _viewModel.ActionListItems.Should().ContainSingle().Which.Action.Should().BeSameAs(visibleClick);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
    }

    [Fact]
    public void SelectedActionProperties_WhenMultipleMixedActionsSelected_AreHidden()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 1 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });

        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 0, 1 });

        _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _viewModel.ShowBatchDelayProperties.Should().BeFalse();
        _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[0]);
    }

    [Fact]
    public void BatchDelayProperties_WhenMultipleDelayActionsSelected_AreVisibleAndApplyToAllSelectedDelays()
    {
        var first = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20, RandomDelayMinMs = 5, RandomDelayMaxMs = 30 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 40, RandomDelayMinMs = 10, RandomDelayMaxMs = 50 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(click);

        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 0, 1 });
        _viewModel.BatchDelayMs = 125;
        _viewModel.BatchDelayUseRandomDelay = true;
        _viewModel.BatchRandomDelayMinMs = 25;
        _viewModel.BatchRandomDelayMaxMs = 250;

        _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _viewModel.ShowBatchDelayProperties.Should().BeTrue();
        _viewModel.ShowBatchRandomDelayOptions.Should().BeTrue();
        first.DelayMs.Should().Be(125);
        second.DelayMs.Should().Be(125);
        first.UseRandomDelay.Should().BeTrue();
        second.UseRandomDelay.Should().BeTrue();
        first.RandomDelayMinMs.Should().Be(25);
        second.RandomDelayMinMs.Should().Be(25);
        first.RandomDelayMaxMs.Should().Be(250);
        second.RandomDelayMaxMs.Should().Be(250);
        click.DelayMs.Should().Be(0);
    }

    [Fact]
    public void BatchDelayProperties_WhenEdited_PreserveSelectedDelayRows()
    {
        var first = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 40 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(click);
        _viewModel.ReplaceSelectedActionUnderlyingIndices(new[] { 0, 1 });

        _viewModel.BatchDelayMs = 75;

        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _viewModel.SelectedAction.Should().BeSameAs(first);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
        _viewModel.ShowBatchDelayProperties.Should().BeTrue();
        _viewModel.BatchDelayMs.Should().Be(75);
    }

    [Fact]
    public void ActionListItems_SimplifyMovement_DefaultsOffAndShowsRawProjection()
    {
        AddCondensibleRun(_viewModel, 6);

        _viewModel.SimplifyMovement.Should().BeFalse();
        _viewModel.HiddenEventCount.Should().Be(0);
        _viewModel.ActionListItems.Should().HaveCount(6);
        _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().AllBeEquivalentTo(0);
    }

    [Fact]
    public void ActionListItems_WhenSimplifyMovementEnabled_CondensesSixActionRun()
    {
        AddCondensibleRun(_viewModel, 6);
        var originalCount = _viewModel.Actions.Count;

        _viewModel.SimplifyMovement = true;

        _viewModel.Actions.Should().HaveCount(originalCount);
        _viewModel.HiddenEventCount.Should().Be(0);
        _viewModel.ActionListItems.Should().ContainSingle();
        var item = _viewModel.ActionListItems[0];
        item.Action.Type.Should().Be(EditorActionType.MouseMove);
        item.UnderlyingIndex.Should().Be(4);
        item.Index.Should().Be(5);
        item.CondensedHiddenCount.Should().Be(5);
    }

    [Fact]
    public void ActionListItems_WhenSimplifyMovementEnabled_DoesNotCondenseFiveActionRun()
    {
        AddCondensibleRun(_viewModel, 5);

        _viewModel.SimplifyMovement = true;

        _viewModel.ActionListItems.Should().HaveCount(5);
        _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(0, 1, 2, 3, 4);
        _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().AllBeEquivalentTo(0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(10, false)]
    [InlineData(20, false)]
    [InlineData(4, true)]
    public void ActionListItems_WhenSimplifyMovementEnabled_DelayRulesDetermineRunBoundaries(int delayMs, bool useRandomDelay)
    {
        AddCondensibleRun(_viewModel, 3);
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.Delay,
            DelayMs = delayMs,
            UseRandomDelay = useRandomDelay,
            RandomDelayMinMs = 1,
            RandomDelayMaxMs = 9
        });
        AddCondensibleRun(_viewModel, 3);

        _viewModel.SimplifyMovement = true;

        _viewModel.ActionListItems.Should().HaveCount(7);
        _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(0, 1, 2, 3, 4, 5, 6);
        _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().AllBeEquivalentTo(0);
    }

    [Fact]
    public void ActionListItems_WhenCondensedRunIsFollowedByDifferentAction_KeepsFollowingActionVisible()
    {
        AddCondensibleRun(_viewModel, 6);
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 10, Y = 20 };
        _viewModel.Actions.Add(click);

        _viewModel.SimplifyMovement = true;

        _viewModel.ActionListItems.Should().HaveCount(2);
        _viewModel.ActionListItems[0].UnderlyingIndex.Should().Be(4);
        _viewModel.ActionListItems[0].CondensedHiddenCount.Should().Be(5);
        _viewModel.ActionListItems[1].Action.Should().BeSameAs(click);
        _viewModel.ActionListItems[1].UnderlyingIndex.Should().Be(6);
        _viewModel.ActionListItems[1].Index.Should().Be(7);
    }

    [Fact]
    public void ActionListItems_WhenRepresentativeIsNotFinalAction_CountsTrailingHiddenRows()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 0, Y = 1 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 2, Y = 3 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 4, Y = 5 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });

        _viewModel.SimplifyMovement = true;

        _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.ActionListItems[0].Action.Type.Should().Be(EditorActionType.MouseMove);
        _viewModel.ActionListItems[0].UnderlyingIndex.Should().Be(4);
        _viewModel.ActionListItems[0].Index.Should().Be(5);
        _viewModel.ActionListItems[0].CondensedHiddenCount.Should().Be(5);
    }

    [Fact]
    public void ActionListItems_WhenCondensedRunHasNoMouseMove_UsesFinalShortDelayAsRepresentative()
    {
        for (var index = 0; index < 6; index++)
        {
            _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        }

        _viewModel.SimplifyMovement = true;

        _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.ActionListItems[0].Action.Type.Should().Be(EditorActionType.Delay);
        _viewModel.ActionListItems[0].UnderlyingIndex.Should().Be(5);
        _viewModel.ActionListItems[0].Index.Should().Be(6);
        _viewModel.ActionListItems[0].CondensedHiddenCount.Should().Be(5);
    }

    [Fact]
    public void ActionListItems_WhenSimplifyMovementEnabled_DoesNotSummarizeDragMovement()
    {
        var down = new EditorAction { Type = EditorActionType.MouseDown };
        var up = new EditorAction { Type = EditorActionType.MouseUp };
        _viewModel.Actions.Add(down);
        AddCondensibleRun(_viewModel, 6);
        _viewModel.Actions.Add(up);

        _viewModel.SimplifyMovement = true;

        _viewModel.ActionListItems.Should().HaveCount(8);
        _viewModel.ActionListItems[0].Action.Should().BeSameAs(down);
        _viewModel.ActionListItems.Skip(1).Take(6).Select(item => item.Action.Type).Should().Equal(
            EditorActionType.MouseMove,
            EditorActionType.Delay,
            EditorActionType.MouseMove,
            EditorActionType.Delay,
            EditorActionType.MouseMove,
            EditorActionType.Delay);
        _viewModel.ActionListItems.Should().OnlyContain(item => item.CondensedHiddenCount == 0);
        _viewModel.ActionListItems[7].Action.Should().BeSameAs(up);
    }

    [Fact]
    public void ActionListItems_WhenHideMouseMovesEnabled_HidesDragMovementRows()
    {
        var down = new EditorAction { Type = EditorActionType.MouseDown };
        var dragMove = new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20 };
        var up = new EditorAction { Type = EditorActionType.MouseUp };
        _viewModel.Actions.Add(down);
        _viewModel.Actions.Add(dragMove);
        _viewModel.Actions.Add(up);

        _viewModel.HideMouseMoves = true;

        _viewModel.ActionListItems.Select(item => item.Action).Should().Equal(down, up);
        _viewModel.HiddenEventCount.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(ActionVisualMetadataCases))]
    public void ActionListItems_ExposeVisualMetadataForActionTaxonomy(
        EditorAction action,
        EditorActionVisualKind visualKind,
        bool isNoise,
        bool isImportant,
        bool isCleanupEligible)
    {
        _viewModel.Actions.Add(action);

        var item = _viewModel.ActionListItems.Should().ContainSingle().Subject;
        item.VisualKind.Should().Be(visualKind);
        item.IsNoise.Should().Be(isNoise);
        item.IsImportant.Should().Be(isImportant);
        item.IsCleanupEligible.Should().Be(isCleanupEligible);
        item.RepresentsSourceAction.Should().BeTrue();
        item.CondensedHiddenCount.Should().Be(0);
    }

    public static IEnumerable<object[]> ActionVisualMetadataCases()
    {
        yield return MetadataCase(new EditorAction { Type = EditorActionType.MouseMove }, EditorActionVisualKind.Movement, isNoise: true, isImportant: false, isCleanupEligible: true);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 1 }, EditorActionVisualKind.Noise, isNoise: true, isImportant: false, isCleanupEligible: true);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 9 }, EditorActionVisualKind.Noise, isNoise: true, isImportant: false, isCleanupEligible: true);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 0 }, EditorActionVisualKind.Timing, isNoise: false, isImportant: false, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 10 }, EditorActionVisualKind.Timing, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 }, EditorActionVisualKind.Timing, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 1, RandomDelayMaxMs = 9 }, EditorActionVisualKind.Timing, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.MouseClick }, EditorActionVisualKind.Pointer, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.MouseDown }, EditorActionVisualKind.Pointer, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.MouseUp }, EditorActionVisualKind.Pointer, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = 1 }, EditorActionVisualKind.Pointer, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.ScrollHorizontal, ScrollAmount = 1 }, EditorActionVisualKind.Pointer, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 }, EditorActionVisualKind.Keyboard, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.KeyDown, KeyCode = 65 }, EditorActionVisualKind.Keyboard, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.KeyUp, KeyCode = 65 }, EditorActionVisualKind.Keyboard, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.TextInput, Text = "abc" }, EditorActionVisualKind.Text, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.RepeatBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.IfBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.ElseBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.WhileBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.ForBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.BlockEnd }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Break }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Continue }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.SetVariable }, EditorActionVisualKind.Variable, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.IncrementVariable }, EditorActionVisualKind.Variable, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.DecrementVariable }, EditorActionVisualKind.Variable, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.RawScriptStep, Text = "raw" }, EditorActionVisualKind.Raw, isNoise: false, isImportant: true, isCleanupEligible: false);
    }

    private static void AddCondensibleRun(EditorViewModel viewModel, int actionCount)
    {
        for (var index = 0; index < actionCount; index++)
        {
            viewModel.Actions.Add(index % 2 == 0
                ? new EditorAction { Type = EditorActionType.MouseMove, X = index, Y = index + 1 }
                : new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        }
    }

    private void HideMovementAndShortWaitRows()
    {
        _viewModel.HideMouseMoves = true;
        _viewModel.HideShortWaits = true;
    }

    private static EditorActionListItem CreateActionListItem(EditorAction action, bool representsSourceAction)
    {
        return new EditorActionListItem(
            action,
            index: 0,
            underlyingIndex: 0,
            indentLevel: 0,
            displayName: "Action",
            condensedHint: string.Empty,
            visualKind: EditorActionVisualKind.Pointer,
            isImportant: false,
            isCleanupEligible: false,
            condensedHiddenCount: 0,
            representsSourceAction: representsSourceAction);
    }

    private static object[] MetadataCase(
        EditorAction action,
        EditorActionVisualKind visualKind,
        bool isNoise,
        bool isImportant,
        bool isCleanupEligible)
    {
        return new object[] { action, visualKind, isNoise, isImportant, isCleanupEligible };
    }

    [Fact]
    public void AddAction_WhenBlockStartAdded_AutoInsertsMatchingEnd()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.IfBlockStart;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.Actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _viewModel.Actions[1].Type.Should().Be(EditorActionType.BlockEnd);
        _viewModel.ActionListItems[1].DisplayName.Should().Be("End IfToken");
    }

    [Theory]
    [InlineData(EditorActionType.Break)]
    [InlineData(EditorActionType.Continue)]
    public void AddAction_WhenLoopControlAddedOutsideLoop_IsBlocked(EditorActionType actionType)
    {
        // Arrange
        _viewModel.NewActionType = actionType;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.Actions.Should().BeEmpty();
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
    }

    [Theory]
    [InlineData(EditorActionType.Break)]
    [InlineData(EditorActionType.Continue)]
    public void AddAction_WhenLoopControlAddedInsideLoop_DoesNotAutoInsertBlockEnd(EditorActionType actionType)
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[0];
        _viewModel.NewActionType = actionType;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.Actions.Should().HaveCount(3);
        _viewModel.Actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _viewModel.Actions[1].Type.Should().Be(actionType);
        _viewModel.Actions[2].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void AddAction_WhenActionSelected_InsertsAfterSelection()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        _viewModel.AddAction();
        var firstAction = _viewModel.Actions[0];
        _viewModel.SelectedAction = firstAction;
        _viewModel.NewActionType = EditorActionType.Delay;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.Actions.Should().HaveCount(3);
        _viewModel.Actions[1].Type.Should().Be(EditorActionType.Delay);
        _viewModel.SelectedAction.Should().Be(_viewModel.Actions[1]);
    }

    [Fact]
    public void RemoveAction_WhenSelected_RemovesAndClearsSelection()
    {
        // Arrange
        _viewModel.AddAction();
        _viewModel.Actions.Should().HaveCount(1);

        // Act
        _viewModel.RemoveAction();

        // Assert
        _viewModel.Actions.Should().BeEmpty();
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.HasSelectedAction.Should().BeFalse();
        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.HasActions.Should().BeFalse();
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _viewModel.ShowBatchDelayProperties.Should().BeFalse();
        _viewModel.Status.Should().Be("[Editor_StatusRemovedAction]");
    }

    [Fact]
    public void RemoveAction_WhenOtherActionsRemain_ClearsSelectionAndHidesProperties()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.SelectedAction = second;

        // Act
        _viewModel.RemoveAction();

        // Assert
        _viewModel.Actions.Should().Equal(first, third);
        _viewModel.Actions.Should().NotContain(second);
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.HasSelectedAction.Should().BeFalse();
        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _viewModel.ShowBatchDelayProperties.Should().BeFalse();
        _viewModel.ShowMultiSelectionPropertiesHint.Should().BeFalse();
        _viewModel.Status.Should().Be("[Editor_StatusRemovedAction]");
    }

    [Fact]
    public void RemoveSelectedActions_WhenMultipleSourceRowsSelected_RemovesDescendingWithOneUndoStateAndClearsSelection()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        var fourth = new EditorAction { Type = EditorActionType.MouseClick, X = 4, Y = 4 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.Actions.Add(fourth);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);

        // Act
        _viewModel.RemoveSelectedActions();

        // Assert
        _viewModel.Actions.Should().Equal(second, fourth);
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.HasSelectedAction.Should().BeFalse();
        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _viewModel.Status.Should().Be("[Editor_StatusRemovedSelectedActions]");

        _viewModel.Undo();
        _viewModel.Actions.Should().HaveCount(4);
    }

    [Fact]
    public void RemoveSelectedActions_WhenSelectionRemovesTail_ClearsSelection()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        // Act
        _viewModel.RemoveSelectedActions();

        // Assert
        _viewModel.Actions.Should().ContainSingle().Which.Should().BeSameAs(first);
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.HasSelectedAction.Should().BeFalse();
        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
    }

    [Fact]
    public void RemoveSelectedActions_WhenSelectedRowsAreHiddenAndPrimarySelectionIsNull_ClearsUnderlyingSelection()
    {
        // Arrange
        var firstHiddenMove = new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 1 };
        var secondHiddenMove = new EditorAction { Type = EditorActionType.MouseMove, X = 2, Y = 2 };
        _viewModel.Actions.Add(firstHiddenMove);
        _viewModel.Actions.Add(secondHiddenMove);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.HideMouseMoves = true;

        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.HasSelectedActions.Should().BeTrue();

        // Act
        _viewModel.RemoveSelectedActions();

        // Assert
        _viewModel.Actions.Should().ContainSingle().Which.Should().BeSameAs(secondHiddenMove);
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.HasSelectedAction.Should().BeFalse();
        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _viewModel.ShowBatchDelayProperties.Should().BeFalse();
    }

    [Fact]
    public void RemoveSelectedActions_WhenAllActionsRemoved_ClearsPrimaryAndBatchSelections()
    {
        // Arrange
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);

        // Act
        _viewModel.RemoveSelectedActions();

        // Assert
        _viewModel.Actions.Should().BeEmpty();
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
    }

    [Fact]
    public void DuplicateSelectedActions_WhenNonContiguousRowsSelected_InsertsClonesAsContiguousBlockAfterHighestSelectedIndex()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        var fourth = new EditorAction { Type = EditorActionType.MouseClick, X = 4, Y = 4 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.Actions.Add(fourth);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);

        // Act
        _viewModel.DuplicateSelectedActions();

        // Assert
        _viewModel.Actions.Should().HaveCount(6);
        _viewModel.Actions[0].Should().BeSameAs(first);
        _viewModel.Actions[1].Should().BeSameAs(second);
        _viewModel.Actions[2].Should().BeSameAs(third);
        _viewModel.Actions[3].Should().NotBeSameAs(first);
        _viewModel.Actions[3].Should().BeEquivalentTo(first, options => options.Excluding(action => action.Index).Excluding(action => action.Id));
        _viewModel.Actions[4].Should().NotBeSameAs(third);
        _viewModel.Actions[4].Should().BeEquivalentTo(third, options => options.Excluding(action => action.Index).Excluding(action => action.Id));
        _viewModel.Actions[5].Should().BeSameAs(fourth);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(3, 4);
        _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[3]);
        _viewModel.Status.Should().Be("[Editor_StatusDuplicatedSelectedActions]");
    }

    [Fact]
    public void DuplicateSelectedActions_WhenUndone_RestoresOriginalActionsWithOneUndoState()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        // Act
        _viewModel.DuplicateSelectedActions();
        _viewModel.Undo();

        // Assert
        _viewModel.Actions.Should().HaveCount(3);
        _viewModel.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.MouseClick,
            EditorActionType.Delay,
            EditorActionType.KeyPress);
        _viewModel.Status.Should().Be("[Editor_StatusUndone]");
    }

    [Fact]
    public void MoveSelectedActionsUp_WhenContiguousRowsSelected_MovesStableBlockUpOneSlot()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        var fourth = new EditorAction { Type = EditorActionType.MouseClick, X = 4 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.Actions.Add(fourth);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        // Act
        _viewModel.MoveSelectedActionsUp();

        // Assert
        _viewModel.Actions.Should().Equal(second, third, first, fourth);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _viewModel.SelectedAction.Should().BeSameAs(second);
        _viewModel.Status.Should().Be("[Editor_StatusMovedSelectedActionsUp]");
    }

    [Fact]
    public void MoveSelectedActionsDown_WhenContiguousRowsSelected_MovesStableBlockDownOneSlot()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        var fourth = new EditorAction { Type = EditorActionType.MouseClick, X = 4 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.Actions.Add(fourth);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        // Act
        _viewModel.MoveSelectedActionsDown();

        // Assert
        _viewModel.Actions.Should().Equal(first, fourth, second, third);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(2, 3);
        _viewModel.SelectedAction.Should().BeSameAs(second);
        _viewModel.Status.Should().Be("[Editor_StatusMovedSelectedActionsDown]");
    }

    [Fact]
    public void MoveSelectedActionsUp_WhenNonContiguousRowsSelected_MovesEachSelectedActionOneSlotWithoutCrossingSelection()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        var fourth = new EditorAction { Type = EditorActionType.TextInput, Text = "text" };
        var fifth = new EditorAction { Type = EditorActionType.MouseClick, X = 5 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.Actions.Add(fourth);
        _viewModel.Actions.Add(fifth);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        _viewModel.SelectedActionUnderlyingIndices.Add(3);

        // Act
        _viewModel.MoveSelectedActionsUp();

        // Assert
        _viewModel.Actions.Should().Equal(second, first, fourth, third, fifth);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _viewModel.SelectedAction.Should().BeSameAs(second);
    }

    [Fact]
    public void MoveSelectedActionsDown_WhenNonContiguousRowsSelected_MovesEachSelectedActionOneSlotWithoutCrossingSelection()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        var fourth = new EditorAction { Type = EditorActionType.TextInput, Text = "text" };
        var fifth = new EditorAction { Type = EditorActionType.MouseClick, X = 5 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.Actions.Add(fourth);
        _viewModel.Actions.Add(fifth);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        _viewModel.SelectedActionUnderlyingIndices.Add(3);

        // Act
        _viewModel.MoveSelectedActionsDown();

        // Assert
        _viewModel.Actions.Should().Equal(first, third, second, fifth, fourth);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(2, 4);
        _viewModel.SelectedAction.Should().BeSameAs(second);
    }

    [Fact]
    public void MoveSelectedActionsUp_WhenAnySelectedActionIsAtTop_RejectsWithoutPartialMutation()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        // Act
        _viewModel.MoveSelectedActionsUp();

        // Assert
        _viewModel.Actions.Should().Equal(first, second, third);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _viewModel.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void MoveSelectedActionsDown_WhenAnySelectedActionIsAtBottom_RejectsWithoutPartialMutation()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        // Act
        _viewModel.MoveSelectedActionsDown();

        // Assert
        _viewModel.Actions.Should().Equal(first, second, third);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _viewModel.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void MoveSelectedActionsDown_WhenCandidateBreaksBlockStructure_DoesNotPartiallyMutate()
    {
        // Arrange
        var ifStart = new EditorAction { Type = EditorActionType.IfBlockStart };
        var ifBody = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var ifEnd = new EditorAction { Type = EditorActionType.BlockEnd };
        var elseStart = new EditorAction { Type = EditorActionType.ElseBlockStart };
        var elseBody = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var elseEnd = new EditorAction { Type = EditorActionType.BlockEnd };
        _viewModel.Actions.Add(ifStart);
        _viewModel.Actions.Add(ifBody);
        _viewModel.Actions.Add(ifEnd);
        _viewModel.Actions.Add(elseStart);
        _viewModel.Actions.Add(elseBody);
        _viewModel.Actions.Add(elseEnd);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        // Act
        _viewModel.MoveSelectedActionsDown();

        // Assert
        _viewModel.Actions.Should().Equal(ifStart, ifBody, ifEnd, elseStart, elseBody, elseEnd);
        _viewModel.SelectedAction.Should().BeSameAs(ifEnd);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(2);
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _viewModel.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void MoveSelectedActionsUp_WhenUndone_RestoresOriginalOrderWithOneUndoState()
    {
        // Arrange
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        // Act
        _viewModel.MoveSelectedActionsUp();
        _viewModel.Undo();

        // Assert
        _viewModel.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.MouseClick,
            EditorActionType.Delay,
            EditorActionType.KeyPress);
        _viewModel.Status.Should().Be("[Editor_StatusUndone]");
    }

    [Fact]
    public void DeleteHiddenEvents_RemovesOnlyEventsHiddenByActiveFilters()
    {
        // Arrange
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 2 };
        var shortDelay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 9 };
        var zeroDelay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 0 };
        var tenMsDelay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 10 };
        var randomShortDelay = new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 1, RandomDelayMaxMs = 9 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 3, Y = 4 };
        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(shortDelay);
        _viewModel.Actions.Add(zeroDelay);
        _viewModel.Actions.Add(tenMsDelay);
        _viewModel.Actions.Add(randomShortDelay);
        _viewModel.Actions.Add(click);
        HideMovementAndShortWaitRows();
        _viewModel.SimplifyMovement = true;

        // Act
        _viewModel.DeleteHiddenEvents();

        // Assert
        _viewModel.Actions.Should().Equal(zeroDelay, tenMsDelay, randomShortDelay, click);
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");

        _viewModel.Undo();
        _viewModel.Actions.Should().HaveCount(6);
    }

    [Fact]
    public void DeleteHiddenEvents_WhenSelectedVisibleActionSurvives_PreservesSelectionByActionIdentity()
    {
        // Arrange
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 2 };
        var selectedClick = new EditorAction { Type = EditorActionType.MouseClick, X = 3, Y = 4 };
        var shortDelay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 9 };
        var finalClick = new EditorAction { Type = EditorActionType.MouseClick, X = 5, Y = 6 };
        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(selectedClick);
        _viewModel.Actions.Add(shortDelay);
        _viewModel.Actions.Add(finalClick);
        _viewModel.HideMouseMoves = true;
        _viewModel.HideShortWaits = true;
        _viewModel.SelectedAction = selectedClick;

        // Act
        _viewModel.DeleteHiddenEvents();

        // Assert
        _viewModel.Actions.Should().Equal(selectedClick, finalClick);
        _viewModel.SelectedAction.Should().BeSameAs(selectedClick);
        _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _viewModel.HasSelectedActions.Should().BeTrue();
        _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _viewModel.ShowSingleSelectedActionProperties.Should().BeTrue();
        _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");
    }

    [Fact]
    public void DeleteHiddenEvents_WhenSelectedActionIsDeleted_ClearsSelection()
    {
        // Arrange
        var selectedMove = new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 2 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 3, Y = 4 };
        _viewModel.Actions.Add(selectedMove);
        _viewModel.Actions.Add(click);
        _viewModel.HideMouseMoves = true;
        _viewModel.SelectedAction = selectedMove;

        // Act
        _viewModel.DeleteHiddenEvents();

        // Assert
        _viewModel.Actions.Should().ContainSingle().Which.Should().BeSameAs(click);
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.SelectedActionListItem.Should().BeNull();
        _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _viewModel.HasSelectedAction.Should().BeFalse();
        _viewModel.HasSelectedActions.Should().BeFalse();
        _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");
    }

    [Fact]
    public void DeleteHiddenEvents_WhenOnlySimplifyMovementEnabled_DoesNotDeleteSimplifiedRows()
    {
        AddCondensibleRun(_viewModel, 6);
        var originalActions = _viewModel.Actions.ToArray();
        _viewModel.SimplifyMovement = true;

        _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.DeleteHiddenEvents();

        _viewModel.Actions.Should().Equal(originalActions);
        _viewModel.Status.Should().Be("[Editor_StatusNoHiddenEventsToDelete]");
        _viewModel.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void DeleteHiddenEvents_WhenNoCandidates_LeavesActionsUnchangedAndSetsNoOpStatus()
    {
        // Arrange
        var zeroDelay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 0 };
        var tenMsDelay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 10 };
        var randomShortDelay = new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 1, RandomDelayMaxMs = 9 };
        _viewModel.Actions.Add(zeroDelay);
        _viewModel.Actions.Add(tenMsDelay);
        _viewModel.Actions.Add(randomShortDelay);

        // Act
        _viewModel.DeleteHiddenEvents();

        // Assert
        _viewModel.Actions.Should().Equal(zeroDelay, tenMsDelay, randomShortDelay);
        _viewModel.Status.Should().Be("[Editor_StatusNoHiddenEventsToDelete]");
        _viewModel.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void DeleteHiddenEvents_WhenHideMouseMovesEnabled_DeletesDragAndIdleMovement()
    {
        var down = new EditorAction { Type = EditorActionType.MouseDown };
        var dragMove = new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20 };
        var up = new EditorAction { Type = EditorActionType.MouseUp };
        var idleMove = new EditorAction { Type = EditorActionType.MouseMove, X = 30, Y = 40 };
        _viewModel.Actions.Add(down);
        _viewModel.Actions.Add(dragMove);
        _viewModel.Actions.Add(up);
        _viewModel.Actions.Add(idleMove);
        _viewModel.HideMouseMoves = true;

        _viewModel.DeleteHiddenEvents();

        _viewModel.Actions.Should().Equal(down, up);
        _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");
    }

    [Fact]
    public void UndoAndRedo_RestorePreviousStates()
    {
        // Arrange
        _viewModel.AddAction();
        _viewModel.AddAction();
        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.CanUndo.Should().BeTrue();
        _viewModel.CanRedo.Should().BeFalse();

        // Act
        _viewModel.Undo();

        // Assert
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.Status.Should().Be("[Editor_StatusUndone]");
        _viewModel.CanUndo.Should().BeTrue();
        _viewModel.CanRedo.Should().BeTrue();

        // Act
        _viewModel.Redo();

        // Assert
        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.Status.Should().Be("[Editor_StatusRedone]");
        _viewModel.CanUndo.Should().BeTrue();
        _viewModel.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_AfterPropertyEdit_RestoresPreviousValue()
    {
        // Arrange
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;

        // Act
        action.DelayMs = 120;
        _viewModel.Undo();

        // Assert
        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.SelectedAction!.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Undo_AfterScreenReadingPropertyEdit_RestoresPreviousValue()
    {
        _viewModel.NewActionType = EditorActionType.PixelSearch;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScreenColorHex = "00FF00";
        action.ScreenFoundVariableName = "found";
        action.ScreenFoundXVariableName = "found_x";
        action.ScreenFoundYVariableName = "found_y";

        action.ScreenFoundVariableName = "is_found";
        _viewModel.Undo();

        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.SelectedAction!.ScreenFoundVariableName.Should().Be("found");
    }

    [Fact]
    public void Undo_AfterScreenReadingCoordinateEdit_RestoresPreviousValue()
    {
        _viewModel.NewActionType = EditorActionType.PixelColor;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScreenX = 10;
        action.ScreenY = 20;

        action.ScreenX = 30;
        _viewModel.Undo();

        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.SelectedAction!.ScreenX.Should().Be(10);
        _viewModel.SelectedAction.ScreenY.Should().Be(20);
    }

    [Theory]
    [InlineData(EditorActionType.PixelColor)]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void EditorActionClone_ForScreenReadingActions_CopiesPayload(EditorActionType actionType)
    {
        var action = new EditorAction
        {
            Type = actionType,
            IsAbsolute = false,
            ScreenX = -3,
            ScreenY = 4,
            ScreenLeft = 5,
            ScreenTop = 6,
            ScreenWidth = 7,
            ScreenHeight = 8,
            ScreenColorHex = "00aaee",
            ScreenColorVariableName = "sample_color",
            ScreenTimeoutMs = 1234,
            ScreenTolerance = 9,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y"
        };

        var clone = action.Clone();

        clone.Id.Should().NotBe(action.Id);
        clone.TryGetScreenReadingPayload(out var clonePayload).Should().BeTrue();
        action.TryGetScreenReadingPayload(out var originalPayload).Should().BeTrue();
        clonePayload.Should().Be(originalPayload);
    }

    [Fact]
    public void Undo_CoalescesRapidEditsOfSameProperty()
    {
        // Arrange
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;

        // Act
        action.DelayMs = 100;
        action.DelayMs = 200;
        _viewModel.Undo();

        // Assert
        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.SelectedAction!.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Undo_AfterHistoryExceedsLimit_KeepsMostRecentStates()
    {
        // Arrange
        for (var index = 0; index < 52; index++)
        {
            _viewModel.AddAction();
        }

        _viewModel.Actions.Should().HaveCount(52);

        // Act
        _viewModel.Undo();
        _viewModel.Undo();

        // Assert
        _viewModel.Actions.Should().HaveCount(50);
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectionChanges_IgnoresCapturedPosition()
    {
        // Arrange
        _viewModel.AddAction();
        var firstAction = _viewModel.SelectedAction!;
        _viewModel.AddAction();
        var secondAction = _viewModel.SelectedAction!;
        _viewModel.SelectedAction = firstAction;

        var captureResult = new TaskCompletionSource<(int X, int Y)?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>()).Returns(_ => captureResult.Task);

        // Act
        var captureTask = _viewModel.CaptureMouseAsync();
        _viewModel.SelectedAction = secondAction;
        captureResult.SetResult((640, 480));
        await captureTask;

        // Assert
        firstAction.X.Should().Be(0);
        firstAction.Y.Should().Be(0);
        secondAction.X.Should().Be(0);
        secondAction.Y.Should().Be(0);
        _viewModel.Status.Should().Be("[Editor_StatusCaptureSelectionChanged]");
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectedActionIsRelative_StoresCapturedPositionAsAbsoluteAction()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MouseButton.Left,
            IsAbsolute = false,
            X = 3,
            Y = -2
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((640, 480)));

        // Act
        await _viewModel.CaptureMouseAsync();

        // Assert
        action.IsAbsolute.Should().BeTrue();
        action.X.Should().Be(640);
        action.Y.Should().Be(480);
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectedActionIsPixelColor_StoresCapturedPositionAsAbsoluteScreenPoint()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            IsAbsolute = false,
            X = 3,
            Y = -2,
            ScreenX = 10,
            ScreenY = 20
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((640, 480)));

        // Act
        await _viewModel.CaptureMouseAsync();

        // Assert
        action.IsAbsolute.Should().BeTrue();
        action.ScreenX.Should().Be(640);
        action.ScreenY.Should().Be(480);
        action.X.Should().Be(3);
        action.Y.Should().Be(-2);
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectedActionIsWaitColor_StoresCapturedPositionAsScreenPoint()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.WaitColor,
            X = 3,
            Y = -2,
            ScreenX = 10,
            ScreenY = 20
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((640, 480)));

        // Act
        await _viewModel.CaptureMouseAsync();

        // Assert
        action.ScreenX.Should().Be(640);
        action.ScreenY.Should().Be(480);
        action.X.Should().Be(3);
        action.Y.Should().Be(-2);
    }

    [Fact]
    public async Task CaptureTargetColorAsync_WhenSelectedActionIsWaitColor_StoresCapturedPixelColor()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.WaitColor, ScreenColorHex = "000000" };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((10, 20)));
        _screenPixelReader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(new ScreenPixelColor(0x12, 0xAB, 0xEF))));

        // Act
        await _viewModel.CaptureTargetColorAsync();

        // Assert
        action.ScreenColorHex.Should().Be("12ABEF");
        _ = _screenPixelReader.Received(1).GetPixelAsync(
            Arg.Is<ScreenPoint>(point => point.X == 10 && point.Y == 20),
            Arg.Any<ScreenReadOptions>());
        _viewModel.Status.Should().Be("[Editor_StatusCapturedColor] 12ABEF 10 20");
    }

    [Fact]
    public async Task CaptureTargetColorAsync_WhenSelectedActionIsPixelSearch_StoresCapturedPixelColor()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.PixelSearch, ScreenColorHex = "000000" };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((30, 40)));
        _screenPixelReader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(new ScreenPixelColor(0x01, 0x23, 0x45))));

        // Act
        await _viewModel.CaptureTargetColorAsync();

        // Assert
        action.ScreenColorHex.Should().Be("012345");
        _ = _screenPixelReader.Received(1).GetPixelAsync(
            Arg.Is<ScreenPoint>(point => point.X == 30 && point.Y == 40),
            Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task CaptureTargetColorAsync_WhenSelectionChanges_DoesNotMutateColor()
    {
        // Arrange
        var firstAction = new EditorAction { Type = EditorActionType.WaitColor, ScreenColorHex = "111111" };
        var secondAction = new EditorAction { Type = EditorActionType.WaitColor, ScreenColorHex = "222222" };
        _viewModel.Actions.Add(firstAction);
        _viewModel.Actions.Add(secondAction);
        _viewModel.SelectedAction = firstAction;

        var captureResult = new TaskCompletionSource<(int X, int Y)?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>()).Returns(_ => captureResult.Task);

        // Act
        var captureTask = _viewModel.CaptureTargetColorAsync();
        _viewModel.SelectedAction = secondAction;
        captureResult.SetResult((50, 60));
        await captureTask;

        // Assert
        firstAction.ScreenColorHex.Should().Be("111111");
        secondAction.ScreenColorHex.Should().Be("222222");
        _viewModel.Status.Should().Be("[Editor_StatusCaptureSelectionChanged]");
        _ = _screenPixelReader.DidNotReceive().GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task CaptureTargetColorAsync_WhenSelectedActionDoesNotUseTargetColor_BlocksCapture()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.PixelColor, ScreenColorHex = "111111" };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Act
        await _viewModel.CaptureTargetColorAsync();

        // Assert
        action.ScreenColorHex.Should().Be("111111");
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _captureService.DidNotReceive().CaptureMousePositionAsync(Arg.Any<CancellationToken>());
        _ = _screenPixelReader.DidNotReceive().GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task CaptureConditionRightColorAsync_WhenRightOperandIsColor_StoresCapturedPixelColor()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptRightOperandType = ScriptOperandType.Color,
            ScriptRightOperand = "000000"
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((70, 80)));
        _screenPixelReader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(new ScreenPixelColor(0xDE, 0xAD, 0xBE))));

        await _viewModel.CaptureConditionRightColorAsync();

        action.ScriptRightOperand.Should().Be("DEADBE");
        _ = _screenPixelReader.Received(1).GetPixelAsync(
            Arg.Is<ScreenPoint>(point => point.X == 70 && point.Y == 80),
            Arg.Any<ScreenReadOptions>());
        _viewModel.Status.Should().Be("[Editor_StatusCapturedColor] DEADBE 70 80");
    }

    [Fact]
    public async Task CaptureConditionLeftColorAsync_WhenLeftOperandIsColor_StoresCapturedPixelColor()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WhileBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Color,
            ScriptLeftOperand = "000000"
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((71, 81)));
        _screenPixelReader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResult<ScreenPixelColor>.Success(new ScreenPixelColor(0x12, 0x34, 0x56))));

        await _viewModel.CaptureConditionLeftColorAsync();

        action.ScriptLeftOperand.Should().Be("123456");
    }

    [Fact]
    public async Task CaptureConditionRightColorAsync_WhenOperandTypeChanges_DoesNotMutateOperand()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptRightOperandType = ScriptOperandType.Color,
            ScriptRightOperand = "111111"
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            action.ScriptRightOperandType = ScriptOperandType.Text;
            return Task.FromResult<(int X, int Y)?>((73, 83));
        });

        await _viewModel.CaptureConditionRightColorAsync();

        action.ScriptRightOperand.Should().Be("111111");
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _screenPixelReader.DidNotReceive().GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task CapturePixelSearchTopLeftAsync_PreservesExistingBottomRightWhenPossible()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenLeft = 10,
            ScreenTop = 20,
            ScreenWidth = 11,
            ScreenHeight = 21
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((15, 25)));

        // Act
        await _viewModel.CapturePixelSearchTopLeftAsync();

        // Assert
        action.ScreenLeft.Should().Be(15);
        action.ScreenTop.Should().Be(25);
        action.ScreenWidth.Should().Be(6);
        action.ScreenHeight.Should().Be(16);
        _viewModel.Status.Should().Be("[Editor_StatusCapturedRegionTopLeft] 15 25");
    }

    [Fact]
    public async Task CapturePixelSearchBottomRightAsync_StoresInclusiveRegionDimensions()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenLeft = 10,
            ScreenTop = 20,
            ScreenWidth = 1,
            ScreenHeight = 1
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((14, 24)));

        // Act
        await _viewModel.CapturePixelSearchBottomRightAsync();

        // Assert
        action.ScreenWidth.Should().Be(5);
        action.ScreenHeight.Should().Be(5);
        _viewModel.Status.Should().Be("[Editor_StatusCapturedRegionBottomRight] 14 24");
    }

    [Fact]
    public async Task CapturePixelSearchBottomRightAsync_WhenCapturedPointIsInvalid_PreservesDimensionsAndSetsStatus()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenLeft = 10,
            ScreenTop = 20,
            ScreenWidth = 7,
            ScreenHeight = 8
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((9, 25)));

        // Act
        await _viewModel.CapturePixelSearchBottomRightAsync();

        // Assert
        action.ScreenWidth.Should().Be(7);
        action.ScreenHeight.Should().Be(8);
        _viewModel.Status.Should().Be("[Editor_StatusCaptureRegionInvalidBottomRight]");
    }

    [Fact]
    public async Task CapturePixelSearchTopLeftAsync_WhenSelectedActionIsNotPixelSearch_BlocksCapture()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.WaitColor, ScreenLeft = 10, ScreenTop = 20 };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Act
        await _viewModel.CapturePixelSearchTopLeftAsync();

        // Assert
        action.ScreenLeft.Should().Be(10);
        action.ScreenTop.Should().Be(20);
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _captureService.DidNotReceive().CaptureMousePositionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectedActionUsesCurrentPosition_ConvertsToCapturedAbsolutePosition()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MouseButton.Left,
            UseCurrentPosition = true
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((640, 480)));

        // Act
        await _viewModel.CaptureMouseAsync();

        // Assert
        action.UseCurrentPosition.Should().BeFalse();
        action.IsAbsolute.Should().BeTrue();
        action.X.Should().Be(640);
        action.Y.Should().Be(480);
        _viewModel.ShowCoordinates.Should().BeTrue();
        _viewModel.ShowCoordModeToggle.Should().BeTrue();
    }

    [Fact]
    public async Task CaptureKeyAsync_WhenSelectionChanges_IgnoresCapturedKey()
    {
        // Arrange
        _viewModel.AddAction();
        var firstAction = _viewModel.SelectedAction!;
        _viewModel.AddAction();
        var secondAction = _viewModel.SelectedAction!;
        _viewModel.SelectedAction = firstAction;

        var captureResult = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureService.CaptureKeyCodeAsync(Arg.Any<CancellationToken>()).Returns(_ => captureResult.Task);

        // Act
        var captureTask = _viewModel.CaptureKeyAsync();
        _viewModel.SelectedAction = secondAction;
        captureResult.SetResult(30);
        await captureTask;

        // Assert
        firstAction.KeyCode.Should().Be(0);
        secondAction.KeyCode.Should().Be(0);
        _viewModel.Status.Should().Be("[Editor_StatusCaptureSelectionChanged]");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenNoActions_ShowsMessage()
    {
        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _dialogService.Received(1).ShowMessageAsync(
            Arg.Is<string>(m => m.Contains("NoActions", StringComparison.Ordinal)),
            Arg.Is<string>(m => m.Contains("NoActions", StringComparison.Ordinal)),
            "OK");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenValidationFails_ShowsValidationMessage()
    {
        // Arrange
        _viewModel.AddAction();
        _validator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>())
            .Returns((false, new List<string> { "Error A", "Error B" }));

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _dialogService.Received(1).ShowMessageAsync(
            Arg.Is<string>(m => m.Contains("ValidationErrors", StringComparison.Ordinal)),
            Arg.Is<string>(m => m.Contains("Error A", StringComparison.Ordinal)),
            "OK");
        _viewModel.Status.Should().Contain("[Editor_StatusValidationFailed]");
    }

    [Fact]
    public void LoadMacroSequence_ClearsTrackedLoadedMacroSession()
    {
        // Arrange
        var sequence = new MacroSequence { Name = "Loaded Macro" };
        _viewModel.TrackLoadedMacroSession(Guid.NewGuid());
        _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(new List<EditorAction>(), new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _viewModel.LinkedLoadedMacroSessionId.Should().BeNull();
    }

    [Fact]
    public void LoadMacroSequence_LoadsConvertedActionsAndName()
    {
        // Arrange
        var sequence = new MacroSequence { Name = "Loaded Macro", SkipInitialZeroZero = true };
        var converted = new List<EditorAction>
        {
            new() { Type = EditorActionType.MouseMove, X = 10, Y = 20 }
        };
        _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _viewModel.MacroName.Should().Be("Loaded Macro");
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.HasActions.Should().BeTrue();
    }

    [Fact]
    public void LoadMacroSequence_WhenConverterRestoresMixedModes_PreservesPerActionModes()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            Name = "Mixed Macro",
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 20,
                    CoordinateMode = MouseCoordinateMode.Absolute
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MouseButton.Left,
                    X = 5,
                    Y = -3,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            ]
        };
        var converted = new List<EditorAction>
        {
            new() { Type = EditorActionType.MouseMove, X = 10, Y = 20, IsAbsolute = true },
            new() { Type = EditorActionType.MouseClick, X = 5, Y = -3, IsAbsolute = false }
        };
        _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.Actions[0].IsAbsolute.Should().BeTrue();
        _viewModel.Actions[1].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void LoadMacroSequence_WhenRestoreReturnsWarnings_ExposesWarningsInViewModel()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            Name = "Loaded Macro",
            ScriptSteps = ["tap ctrl+c"]
        };
        var converted = new List<EditorAction>
        {
            new() { Type = EditorActionType.RawScriptStep, Text = "tap ctrl+c" }
        };
        var warnings = new List<EditorActionRestoreWarning>
        {
            new(1, "tap ctrl+c", "Unsupported step restored as raw script text.")
        };
        _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, warnings, restoredFromScriptSteps: true));

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _viewModel.HasLoadWarnings.Should().BeTrue();
        _viewModel.LoadWarnings.Should().ContainSingle();
        _viewModel.LoadWarnings[0].Should().Contain("Step 1");
    }

    [Fact]
    public void DelayVisibility_TogglesBetweenFixedAndRandomInputs()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.Delay;
        _viewModel.AddAction();

        // Assert
        _viewModel.ShowDelay.Should().BeTrue();
        _viewModel.ShowFixedDelayInput.Should().BeTrue();
        _viewModel.ShowRandomDelayOptions.Should().BeFalse();

        // Act
        _viewModel.SelectedAction!.UseRandomDelay = true;

        // Assert
        _viewModel.ShowFixedDelayInput.Should().BeFalse();
        _viewModel.ShowRandomDelayOptions.Should().BeTrue();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove)]
    [InlineData(EditorActionType.MouseClick)]
    [InlineData(EditorActionType.MouseDown)]
    [InlineData(EditorActionType.MouseUp)]
    public void ShowCoordinates_ForCoordinateBasedMouseActions_IsTrue(EditorActionType actionType)
    {
        // Arrange
        _viewModel.NewActionType = actionType;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.ShowCoordinates.Should().BeTrue();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove)]
    [InlineData(EditorActionType.MouseClick)]
    [InlineData(EditorActionType.MouseDown)]
    [InlineData(EditorActionType.MouseUp)]
    public void ShowCoordModeToggle_ForCoordinateBasedMouseActions_IsTrue(EditorActionType actionType)
    {
        // Arrange
        _viewModel.NewActionType = actionType;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.ShowCoordModeToggle.Should().BeTrue();
    }

    [Fact]
    public void ScriptSetVariableAction_ShowsStructuredFieldsOnly()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.ShowSetVariableFields.Should().BeTrue();
        _viewModel.ShowTextInput.Should().BeFalse();
        _viewModel.ShowIncDecFields.Should().BeFalse();
        _viewModel.ShowConditionFields.Should().BeFalse();
    }

    [Fact]
    public void ForAction_WhenStepEnabled_ShowsStepFields()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.ForBlockStart;
        _viewModel.AddAction();

        // Act
        _viewModel.SelectedAction!.ForHasStep = true;

        // Assert
        _viewModel.ShowForFields.Should().BeTrue();
        _viewModel.ShowForStepFields.Should().BeTrue();
    }

    [Fact]
    public void AvailableVariableNames_WhenSetActionsExist_ReturnsNamesFromPreviousActions()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.ScriptVariableName = "speed";

        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.ScriptVariableName = "mode";

        // Act
        var names = _viewModel.AvailableVariableNames;

        // Assert
        names.Should().Contain("speed");
        names.Should().Contain("mode");
    }

    [Fact]
    public void AvailableVariableNames_WhenSingleSetActionExists_IncludesCurrentVariable()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.ScriptVariableName = "i";

        // Act
        var names = _viewModel.AvailableVariableNames;

        // Assert
        names.Should().Contain("i");
        _viewModel.HasAvailableVariableNames.Should().BeTrue();
    }

    [Fact]
    public void AvailableColorVariableNames_WhenScreenReadingActionsExist_ReturnsOnlyPixelColorOutputs()
    {
        var pixelColor = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            ScreenColorVariableName = "sample_color"
        };
        var waitColor = new EditorAction
        {
            Type = EditorActionType.WaitColor,
            ScreenColorVariableName = "wait_ok"
        };
        var pixelSearch = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y"
        };

        _viewModel.Actions.Add(pixelColor);
        _viewModel.Actions.Add(waitColor);
        _viewModel.Actions.Add(pixelSearch);
        _viewModel.NewActionType = EditorActionType.TextInput;
        _viewModel.AddAction();

        var names = _viewModel.AvailableColorVariableNames;

        names.Should().Contain("sample_color");
        names.Should().NotContain("wait_ok");
        names.Should().NotContain("found");
        names.Should().NotContain("found_x");
        names.Should().NotContain("found_y");
    }

    [Fact]
    public void SetVariableSuggestions_WhenSelectingSecondAction_DoesNotMutateFirstAction()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var first = _viewModel.SelectedAction!;
        first.ScriptVariableName = "alpha";

        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var second = _viewModel.SelectedAction!;
        second.ScriptVariableName = "beta";

        _viewModel.SelectedAction = second;

        // Act
        _viewModel.SelectedSetVariableSuggestion = "alpha";

        // Assert
        first.ScriptVariableName.Should().Be("alpha");
        second.ScriptVariableName.Should().Be("alpha");
    }

    [Fact]
    public void VariableSuggestionBindings_ForScriptFields_WriteBackToSelectedActions()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.ScriptVariableName = "shared";

        _viewModel.NewActionType = EditorActionType.IncrementVariable;
        _viewModel.AddAction();
        var incrementAction = _viewModel.SelectedAction!;

        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();
        var conditionAction = _viewModel.SelectedAction!;
        conditionAction.ScriptLeftOperandType = ScriptOperandType.VariableReference;
        conditionAction.ScriptRightOperandType = ScriptOperandType.VariableReference;

        _viewModel.NewActionType = EditorActionType.ForBlockStart;
        _viewModel.AddAction();
        var forAction = _viewModel.SelectedAction!;

        // Act / Assert
        _viewModel.SelectedAction = incrementAction;
        _viewModel.ShowIncDecVariablePicker.Should().BeTrue();
        _viewModel.SelectedIncDecVariableSuggestion = "shared";
        _viewModel.SelectedIncDecVariableSuggestion.Should().BeNull();
        incrementAction.ScriptVariableName.Should().Be("shared");

        _viewModel.SelectedAction = conditionAction;
        _viewModel.ShowConditionLeftVariablePicker.Should().BeTrue();
        _viewModel.ShowConditionLeftOperandTextBox.Should().BeFalse();
        _viewModel.ShowConditionRightVariablePicker.Should().BeTrue();
        _viewModel.ShowConditionRightOperandTextBox.Should().BeFalse();
        _viewModel.SelectedConditionLeftVariableSuggestion = "shared";
        _viewModel.SelectedConditionRightVariableSuggestion = "shared";
        _viewModel.SelectedConditionLeftVariableSuggestion.Should().Be("shared");
        _viewModel.SelectedConditionRightVariableSuggestion.Should().Be("shared");
        conditionAction.ScriptLeftOperand.Should().Be("shared");
        conditionAction.ScriptRightOperand.Should().Be("shared");

        _viewModel.SelectedAction = forAction;
        _viewModel.ShowForVariablePicker.Should().BeTrue();
        _viewModel.SelectedForVariableSuggestion = "shared";
        _viewModel.SelectedForVariableSuggestion.Should().BeNull();
        forAction.ForVariableName.Should().Be("shared");

        _viewModel.AvailableVariableNames.Should().Contain("shared");
        _viewModel.HasAvailableVariableNames.Should().BeTrue();
    }

    [Fact]
    public void AvailableVariableNames_WhenPixelSearchFoundVariableChanges_RefreshesSuggestions()
    {
        var pixelSearch = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y"
        };
        _viewModel.Actions.Add(pixelSearch);

        _viewModel.AvailableVariableNames.Should().Contain("found");

        pixelSearch.ScreenFoundVariableName = "located";

        _viewModel.AvailableVariableNames.Should().Contain("located");
        _viewModel.AvailableVariableNames.Should().NotContain("found");
    }

    [Fact]
    public void AvailableColorVariableNames_WhenPixelColorVariableChanges_RefreshesSuggestions()
    {
        var pixelColor = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            ScreenColorVariableName = "sample_color"
        };
        _viewModel.Actions.Add(pixelColor);
        _viewModel.NewActionType = EditorActionType.TextInput;
        _viewModel.AddAction();

        _viewModel.AvailableColorVariableNames
            .Should()
            .Contain("sample_color");

        pixelColor.ScreenColorVariableName = "sample_color_next";

        var names = _viewModel.AvailableColorVariableNames;

        names.Should().Contain("sample_color_next");
        names.Should().NotContain("sample_color");
    }

    [Fact]
    public void SelectedScreenTargetColorVariableSuggestion_WritesBackToSelectedAction()
    {
        var pixelColor = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            ScreenColorVariableName = "sample_color"
        };
        _viewModel.Actions.Add(pixelColor);

        _viewModel.NewActionType = EditorActionType.WaitColor;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;

        action.ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable;

        _viewModel.SelectedScreenTargetColorVariableSuggestion = "sample_color";

        action.ScreenTargetColorVariableName.Should().Be("sample_color");
        _viewModel.SelectedScreenTargetColorVariableSuggestion.Should().BeNull();
    }

    [Fact]
    public void VariableNameChangeOnOtherAction_DoesNotOverwriteCurrentSelection()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var first = _viewModel.SelectedAction!;
        first.ScriptVariableName = "one";

        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var second = _viewModel.SelectedAction!;
        second.ScriptVariableName = "two";
        _viewModel.SelectedAction = second;

        // Act
        first.ScriptVariableName = "three";

        // Assert
        second.ScriptVariableName.Should().Be("two");
        _viewModel.AvailableVariableNames.Should().Contain("three");
    }

    [Fact]
    public void SelectedAction_WhenEditingVariableName_RemainsSelected()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var selected = _viewModel.SelectedAction!;

        // Act
        selected.ScriptVariableName = "i";
        selected.ScriptVariableName = "it";
        selected.ScriptVariableName = "iter";

        // Assert
        _viewModel.SelectedAction.Should().BeSameAs(selected);
        _viewModel.SelectedActionListItem.Should().NotBeNull();
        _viewModel.SelectedActionListItem!.Action.Should().BeSameAs(selected);
    }

    [Fact]
    public void InsertElseBlock_WhenIfSelected_InsertsElseSkeleton()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[0];
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();

        _viewModel.SelectedAction = _viewModel.Actions[0];

        // Act
        _viewModel.InsertElseBlock();

        // Assert
        _viewModel.Actions.Should().HaveCount(5);
        _viewModel.Actions[2].Type.Should().Be(EditorActionType.BlockEnd);
        _viewModel.Actions[3].Type.Should().Be(EditorActionType.ElseBlockStart);
        _viewModel.Actions[4].Type.Should().Be(EditorActionType.BlockEnd);
        _viewModel.Status.Should().Be("[Editor_StatusInsertedElseBlock]");
    }

    [Fact]
    public void RemoveAction_WhenItWouldBreakBlockStructure_IsBlocked()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[1];

        // Act
        _viewModel.RemoveAction();

        // Assert
        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
    }

    [Fact]
    public void RemoveBlock_WhenIfSelected_RemovesWholeBlock()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[0];
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.SelectedAction = _viewModel.Actions[^1];
        _viewModel.AddAction();

        _viewModel.SelectedAction = _viewModel.Actions[0];

        // Act
        _viewModel.RemoveBlock();

        // Assert
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.Actions[0].Type.Should().Be(EditorActionType.MouseClick);
        _viewModel.Status.Should().Be("[Editor_StatusRemovedBlock]");
    }

    [Fact]
    public void RemoveBlock_WhenIfHasElse_RemovesIfAndElseSectionsTogether()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[0];
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[0];
        _viewModel.InsertElseBlock();
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();

        _viewModel.SelectedAction = _viewModel.Actions[0];

        // Act
        _viewModel.RemoveBlock();

        // Assert
        _viewModel.Actions.Should().BeEmpty();
        _viewModel.Status.Should().Be("[Editor_StatusRemovedBlock]");
    }

    [Fact]
    public void RemoveSelectedActions_WhenCandidatesBreakBlockStructure_DoesNotPartiallyMutate()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();
        var blockStart = _viewModel.Actions[0];
        var blockEnd = _viewModel.Actions[1];
        _viewModel.SelectedActionUnderlyingIndices.Add(0);

        // Act
        _viewModel.RemoveSelectedActions();

        // Assert
        _viewModel.Actions.Should().Equal(blockStart, blockEnd);
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _viewModel.CanUndo.Should().BeTrue();
        _viewModel.Undo();
        _viewModel.Actions.Should().BeEmpty();
    }

    [Fact]
    public void DeleteHiddenEvents_WhenCandidateStructureIsInvalid_DoesNotPartiallyMutate()
    {
        // Arrange
        var noiseMove = new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20 };
        var unmatchedBlockEnd = new EditorAction { Type = EditorActionType.BlockEnd };
        _viewModel.Actions.Add(noiseMove);
        _viewModel.Actions.Add(unmatchedBlockEnd);
        _viewModel.HideMouseMoves = true;

        // Act
        _viewModel.DeleteHiddenEvents();

        // Assert
        _viewModel.Actions.Should().Equal(noiseMove, unmatchedBlockEnd);
        _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
    }

    [Fact]
    public void ActionListPresentation_WhenNestedBlocksExist_ShowsIndentAndContextualEndLabels()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[0];
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();

        // Assert
        _viewModel.ActionListItems[0].IndentLevel.Should().Be(0);
        _viewModel.ActionListItems[1].IndentLevel.Should().Be(1);
        _viewModel.ActionListItems[2].DisplayName.Should().Be("End IfToken");
        _viewModel.ActionListItems[2].IndentLevel.Should().Be(1);
        _viewModel.ActionListItems[3].DisplayName.Should().Be("End RepeatToken");
        _viewModel.ActionListItems[3].IndentLevel.Should().Be(0);
    }

    [Fact]
    public void ClearAll_WhenCollectionResetOccurs_UnsubscribesRemovedActions()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        var removedAction = _viewModel.SelectedAction!;

        _viewModel.ClearAll();
        _viewModel.NewActionType = EditorActionType.Delay;
        _viewModel.AddAction();

        var collectionChangeCount = 0;
        void OnActionListChanged(object? _, NotifyCollectionChangedEventArgs __) => collectionChangeCount++;
        _viewModel.ActionListItems.CollectionChanged += OnActionListChanged;

        // Act
        try
        {
            removedAction.X += 1;
            removedAction.Y += 1;
        }
        finally
        {
            _viewModel.ActionListItems.CollectionChanged -= OnActionListChanged;
        }

        // Assert
        collectionChangeCount.Should().Be(0);
        _viewModel.ActionListItems.Should().HaveCount(1);
        _viewModel.ActionListItems[0].Action.Should().BeSameAs(_viewModel.Actions[0]);
    }

    [Fact]
    public void AddAction_WhenSelectedCoordinateActionIsRelative_NewCoordinateActionInheritsRelativeModeWithoutMutatingExistingActions()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();
        var moveAction = _viewModel.SelectedAction!;
        moveAction.IsAbsolute = false;

        // Act
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        var clickAction = _viewModel.SelectedAction!;

        // Assert
        moveAction.IsAbsolute.Should().BeFalse();
        clickAction.IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void AddAction_WhenSelectionIsNotCoordinate_UsesPreviousCoordinateModeWithoutMutatingExistingActions()
    {
        // Arrange
        var moveAction = new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 10, Y = 20 };
        var delayAction = new EditorAction { Type = EditorActionType.Delay, DelayMs = 25 };
        _viewModel.Actions.Add(moveAction);
        _viewModel.Actions.Add(delayAction);
        _viewModel.SelectedAction = delayAction;
        _viewModel.NewActionType = EditorActionType.MouseClick;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.Actions.Should().HaveCount(3);
        _viewModel.Actions[0].Should().BeSameAs(moveAction);
        _viewModel.Actions[1].Should().BeSameAs(delayAction);
        _viewModel.SelectedAction!.IsAbsolute.Should().BeFalse();
        moveAction.IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void AddAction_WhenNoPreviousCoordinateAction_UsesFirstCoordinateModeWithoutMutatingExistingActions()
    {
        // Arrange
        var delayAction = new EditorAction { Type = EditorActionType.Delay, DelayMs = 25 };
        var laterMoveAction = new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 10, Y = 20 };
        _viewModel.Actions.Add(delayAction);
        _viewModel.Actions.Add(laterMoveAction);
        _viewModel.SelectedAction = delayAction;
        _viewModel.NewActionType = EditorActionType.MouseClick;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.Actions.Should().HaveCount(3);
        _viewModel.Actions[1].Should().Be(_viewModel.SelectedAction);
        _viewModel.SelectedAction!.IsAbsolute.Should().BeFalse();
        laterMoveAction.IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void AddAction_WhenNoCoordinateModeSource_DefaultsToAbsolute()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseMove;

        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.SelectedAction!.IsAbsolute.Should().BeTrue();
    }

    [Fact]
    public void AddCurrentPositionClick_AddsRelativeClickAndEnablesSkipInitialZeroZero()
    {
        // Act
        _viewModel.AddCurrentPositionClick();

        // Assert
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.SelectedAction!.Type.Should().Be(EditorActionType.MouseClick);
        _viewModel.SelectedAction.UseCurrentPosition.Should().BeTrue();
        _viewModel.SelectedAction.IsAbsolute.Should().BeFalse();
        _viewModel.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void CurrentPositionClick_HidesCoordinateInputsAndCoordinateModeToggle()
    {
        // Arrange
        _viewModel.AddCurrentPositionClick();

        // Assert
        _viewModel.ShowCoordinates.Should().BeFalse();
        _viewModel.ShowCoordModeToggle.Should().BeFalse();
        _viewModel.ShowCurrentPositionToggle.Should().BeTrue();
    }

    [Fact]
    public void CurrentPositionToggle_IsVisibleForMouseDownAndMouseUp()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseDown;
        _viewModel.AddAction();

        // Assert
        _viewModel.ShowCurrentPositionToggle.Should().BeTrue();
        _viewModel.CurrentPositionToggleLabel.Should().Be("[Editor_CurrentPositionHold]");

        // Act
        _viewModel.NewActionType = EditorActionType.MouseUp;
        _viewModel.AddAction();

        // Assert
        _viewModel.ShowCurrentPositionToggle.Should().BeTrue();
        _viewModel.CurrentPositionToggleLabel.Should().Be("[Editor_CurrentPositionRelease]");
    }

    [Fact]
    public void CurrentPositionClick_DoesNotChangeExistingAbsoluteMode()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();
        var moveAction = _viewModel.SelectedAction!;
        moveAction.IsAbsolute = true;

        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        var clickAction = _viewModel.SelectedAction!;

        // Act
        clickAction.UseCurrentPosition = true;

        // Assert
        clickAction.IsAbsolute.Should().BeFalse();
        moveAction.IsAbsolute.Should().BeTrue();
        _viewModel.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void CurrentPositionClick_WhenAnotherActionSetToAbsolute_KeepsCurrentPositionClickRelative()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();
        var moveAction = _viewModel.SelectedAction!;
        moveAction.IsAbsolute = false;

        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        var clickAction = _viewModel.SelectedAction!;
        clickAction.UseCurrentPosition = true;

        // Act
        _viewModel.SelectedAction = moveAction;
        moveAction.IsAbsolute = true;

        // Assert
        moveAction.IsAbsolute.Should().BeTrue();
        clickAction.IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void CurrentPositionClick_WhenDisabled_RestoresPreviousSkipInitialZeroZeroValue()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        var clickAction = _viewModel.SelectedAction!;
        _viewModel.SkipInitialZeroZero.Should().BeFalse();

        // Act
        clickAction.UseCurrentPosition = true;
        clickAction.UseCurrentPosition = false;

        // Assert
        _viewModel.SkipInitialZeroZero.Should().BeFalse();
    }

    [Fact]
    public void CurrentPositionClick_WhenDisabledInRelativeMode_RestoresPreviousSkipInitialZeroZeroValue()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.IsAbsolute = false;

        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        var clickAction = _viewModel.SelectedAction!;
        clickAction.IsAbsolute.Should().BeFalse();
        _viewModel.SkipInitialZeroZero.Should().BeFalse();

        // Act
        clickAction.UseCurrentPosition = true;
        clickAction.UseCurrentPosition = false;

        // Assert
        _viewModel.SkipInitialZeroZero.Should().BeFalse();
    }

    [Fact]
    public void CoordinateModeChange_OnSelectedCoordinateAction_DoesNotChangeOtherCoordinateActions()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();
        var moveAction = _viewModel.SelectedAction!;

        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        var clickAction = _viewModel.SelectedAction!;
        moveAction.IsAbsolute.Should().BeTrue();
        clickAction.IsAbsolute.Should().BeTrue();

        // Act
        clickAction.IsAbsolute = false;

        // Assert
        moveAction.IsAbsolute.Should().BeTrue();
        clickAction.IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void SelectedActionCoordinateModeProperties_IgnoreRadioUncheckWritesAndPersistCheckedModeAcrossSelectionChanges()
    {
        // Arrange
        var moveAction = new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 10, Y = 20 };
        var clickAction = new EditorAction { Type = EditorActionType.MouseClick, IsAbsolute = true, X = 30, Y = 40 };
        _viewModel.Actions.Add(moveAction);
        _viewModel.Actions.Add(clickAction);
        _viewModel.SelectedAction = moveAction;

        // Act: Avalonia first checks the relative radio, then may uncheck the absolute radio during rebind.
        _viewModel.SelectedActionIsRelative = true;
        _viewModel.SelectedActionIsAbsolute = false;
        _viewModel.SelectedAction = clickAction;
        _viewModel.SelectedAction = moveAction;

        // Assert
        moveAction.IsAbsolute.Should().BeFalse();
        clickAction.IsAbsolute.Should().BeTrue();
        _viewModel.SelectedActionIsRelative.Should().BeTrue();
        _viewModel.SelectedActionIsAbsolute.Should().BeFalse();
    }

    [Fact]
    public async Task SaveMacroAsync_WhenSuccessful_RaisesMacroCreatedWithSourcePath()
    {
        _viewModel.AddAction();
        _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-raised-path.macro");

        var generatedSequence = new MacroSequence
        {
            Name = "Generated",
            Events = [new MacroEvent { Type = EventType.Click, Button = MouseButton.Left, X = 10, Y = 10 }]
        };
        _converter
            .ToMacroSequence(Arg.Any<IEnumerable<EditorAction>>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(generatedSequence);

        EditorMacroCreatedEventArgs? raisedArgs = null;
        _viewModel.MacroCreated += (_, args) => raisedArgs = args;

        await _viewModel.SaveMacroAsync();

        raisedArgs.Should().NotBeNull();
        raisedArgs!.Macro.Should().BeSameAs(generatedSequence);
        raisedArgs.SourcePath.Should().Be("/tmp/editor-raised-path.macro");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenOnlyMouseClickAction_UsesCoordinateModeFromAction()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.IsAbsolute = true;

        _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-click-absolute.macro");

        var generatedSequence = new MacroSequence
        {
            Name = "Generated",
            Events = [new MacroEvent { Type = EventType.Click, Button = MouseButton.Left, X = 10, Y = 10 }]
        };
        _converter
            .ToMacroSequence(Arg.Any<IEnumerable<EditorAction>>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(generatedSequence);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _converter.Received(1).ToMacroSequence(
            Arg.Any<IEnumerable<EditorAction>>(),
            Arg.Any<string>(),
            true,
            Arg.Any<bool>());
    }

    [Fact]
    public async Task SaveMacroAsync_WhenActionsUseMixedCoordinateModes_PassesActionsPreservingPerActionModes()
    {
        // Arrange
        var absoluteMove = new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 200 };
        var relativeMove = new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 5, Y = -3 };
        _viewModel.Actions.Add(absoluteMove);
        _viewModel.Actions.Add(relativeMove);

        _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-mixed-modes.macro");

        IReadOnlyList<EditorAction>? capturedActions = null;
        bool? capturedLegacyDefault = null;
        _converter
            .ToMacroSequence(
                Arg.Do<IEnumerable<EditorAction>>(actions => capturedActions = actions.ToList()),
                Arg.Any<string>(),
                Arg.Do<bool>(isAbsolute => capturedLegacyDefault = isAbsolute),
                Arg.Any<bool>())
            .Returns(new MacroSequence { Name = "Generated" });

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        capturedActions.Should().NotBeNull();
        capturedActions!.Should().HaveCount(2);
        capturedActions[0].Should().NotBeSameAs(absoluteMove);
        capturedActions[0].IsAbsolute.Should().BeTrue();
        capturedActions[0].X.Should().Be(100);
        capturedActions[0].Y.Should().Be(200);
        capturedActions[1].Should().NotBeSameAs(relativeMove);
        capturedActions[1].IsAbsolute.Should().BeFalse();
        capturedActions[1].X.Should().Be(5);
        capturedActions[1].Y.Should().Be(-3);
        capturedLegacyDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionClickExists_ForcesSkipInitialZeroZero()
    {
        // Arrange
        _viewModel.AddCurrentPositionClick();

        _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-current-position-click.macro");

        var generatedSequence = new MacroSequence
        {
            Name = "Generated",
            Events = [new MacroEvent { Type = EventType.Click, Button = MouseButton.Left, X = 0, Y = 0 }]
        };
        _converter
            .ToMacroSequence(Arg.Any<IEnumerable<EditorAction>>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(generatedSequence);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _converter.Received(1).ToMacroSequence(
            Arg.Any<IEnumerable<EditorAction>>(),
            Arg.Any<string>(),
            false,
            true);
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionClickHasStaleAbsoluteCoordinates_NormalizesSnapshotBeforeValidationAndSave()
    {
        // Arrange
        var currentPositionClick = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MouseButton.Left,
            UseCurrentPosition = true,
            IsAbsolute = true,
            X = 123,
            Y = 456
        };
        _viewModel.Actions.Add(currentPositionClick);

        _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-stale-current-position.macro");

        IReadOnlyList<EditorAction>? validatedActions = null;
        _validator
            .ValidateAll(Arg.Do<IEnumerable<EditorAction>>(actions => validatedActions = actions.ToList()))
            .Returns((true, new List<string>()));

        IReadOnlyList<EditorAction>? convertedActions = null;
        _converter
            .ToMacroSequence(
                Arg.Do<IEnumerable<EditorAction>>(actions => convertedActions = actions.ToList()),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>())
            .Returns(new MacroSequence { Name = "Generated" });

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        currentPositionClick.X.Should().Be(123);
        currentPositionClick.Y.Should().Be(456);
        currentPositionClick.IsAbsolute.Should().BeTrue();
        validatedActions.Should().ContainSingle().Which.Should().NotBeSameAs(currentPositionClick);
        validatedActions![0].IsAbsolute.Should().BeFalse();
        validatedActions[0].X.Should().Be(0);
        validatedActions[0].Y.Should().Be(0);
        convertedActions.Should().ContainSingle().Which.Should().NotBeSameAs(currentPositionClick);
        convertedActions![0].IsAbsolute.Should().BeFalse();
        convertedActions[0].X.Should().Be(0);
        convertedActions[0].Y.Should().Be(0);
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionClickHasStaleCoordinatesAndDialogCancels_DoesNotMutateBoundAction()
    {
        // Arrange
        var currentPositionClick = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MouseButton.Left,
            UseCurrentPosition = true,
            IsAbsolute = true,
            X = 123,
            Y = 456
        };
        _viewModel.Actions.Add(currentPositionClick);

        _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns((string?)null);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        currentPositionClick.IsAbsolute.Should().BeTrue();
        currentPositionClick.X.Should().Be(123);
        currentPositionClick.Y.Should().Be(456);
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionClickIsFirstAndOtherActionsAreAbsolute_UsesAbsoluteMacroMode()
    {
        // Arrange
        _viewModel.AddCurrentPositionClick();
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.IsAbsolute = true;

        _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-current-position-with-absolute.macro");

        var generatedSequence = new MacroSequence
        {
            Name = "Generated",
            IsAbsoluteCoordinates = true,
            Events =
            [
                new MacroEvent { Type = EventType.Click, Button = MouseButton.Left, X = 0, Y = 0, UseCurrentPosition = true },
                new MacroEvent { Type = EventType.MouseMove, X = 120, Y = 90 }
            ]
        };
        _converter
            .ToMacroSequence(Arg.Any<IEnumerable<EditorAction>>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(generatedSequence);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _converter.Received(1).ToMacroSequence(
            Arg.Any<IEnumerable<EditorAction>>(),
            Arg.Any<string>(),
            true,
            true);
    }
}
