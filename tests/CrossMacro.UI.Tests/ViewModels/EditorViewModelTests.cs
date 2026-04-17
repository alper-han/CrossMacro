using System.Collections.Specialized;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class EditorViewModelTests
{
    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly EditorViewModel _viewModel;

    public EditorViewModelTests()
    {
        _converter = Substitute.For<IEditorActionConverter>();
        _validator = Substitute.For<IEditorActionValidator>();
        _captureService = Substitute.For<ICoordinateCaptureService>();
        _fileManager = Substitute.For<IMacroFileManager>();
        _dialogService = Substitute.For<IDialogService>();
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _keyCodeMapper.GetKeyName(Arg.Any<int>()).Returns("A");

        _validator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>()).Returns((true, new List<string>()));

        _viewModel = new EditorViewModel(
            _converter,
            _validator,
            _captureService,
            _fileManager,
            _dialogService,
            _keyCodeMapper);
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
        _viewModel.Status.Should().Contain("Added");
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
        _viewModel.ActionListItems[1].DisplayName.Should().Be("End If");
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
        _viewModel.Status.Should().Be("Operation blocked: would break block structure");
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
        _viewModel.HasActions.Should().BeFalse();
        _viewModel.Status.Should().Be("Removed action");
    }

    [Fact]
    public void UndoAndRedo_RestorePreviousStates()
    {
        // Arrange
        _viewModel.AddAction();
        _viewModel.AddAction();
        _viewModel.Actions.Should().HaveCount(2);

        // Act
        _viewModel.Undo();

        // Assert
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.Status.Should().Be("Undone");

        // Act
        _viewModel.Redo();

        // Assert
        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.Status.Should().Be("Redone");
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
        _viewModel.Status.Should().Be("Capture ignored: selected action changed");
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
        _viewModel.Status.Should().Be("Capture ignored: selected action changed");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenNoActions_ShowsMessage()
    {
        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _dialogService.Received(1).ShowMessageAsync(
            "No Actions",
            Arg.Is<string>(m => m.Contains("Please add at least one action", StringComparison.Ordinal)),
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
            "Validation Errors",
            Arg.Is<string>(m => m.Contains("Error A", StringComparison.Ordinal)),
            "OK");
        _viewModel.Status.Should().Contain("Validation failed");
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
        _viewModel.Status.Should().Be("Inserted else block");
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
        _viewModel.Status.Should().Be("Operation blocked: would break block structure");
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
        _viewModel.Status.Should().Be("Removed block");
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
        _viewModel.Status.Should().Be("Removed block");
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
        _viewModel.ActionListItems[2].DisplayName.Should().Be("End If");
        _viewModel.ActionListItems[2].IndentLevel.Should().Be(1);
        _viewModel.ActionListItems[3].DisplayName.Should().Be("End Repeat");
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
    public void AddAction_WhenMacroModeIsRelative_NewCoordinateActionInheritsRelativeMode()
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
        _viewModel.CurrentPositionToggleLabel.Should().Be("Hold at current cursor position");

        // Act
        _viewModel.NewActionType = EditorActionType.MouseUp;
        _viewModel.AddAction();

        // Assert
        _viewModel.ShowCurrentPositionToggle.Should().BeTrue();
        _viewModel.CurrentPositionToggleLabel.Should().Be("Release at current cursor position");
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
    public void CoordinateModeChange_OnSelectedCoordinateAction_PropagatesToAllCoordinateActions()
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
        moveAction.IsAbsolute.Should().BeFalse();
        clickAction.IsAbsolute.Should().BeFalse();
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
