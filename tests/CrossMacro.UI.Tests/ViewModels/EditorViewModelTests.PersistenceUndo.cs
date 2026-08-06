// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{

    [Fact]
    public void DuplicateSelectedActions_WhenSingleRowSelected_DuplicatesSelectionAndSupportsUndo()
    {
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);

        _viewModel.DuplicateSelectedActions();

        _ = _viewModel.Actions.Should().HaveCount(3);
        _ = _viewModel.Actions[0].Should().BeSameAs(first);
        _ = _viewModel.Actions[1].Should().NotBeSameAs(first);
        _ = _viewModel.Actions[1].Type.Should().Be(first.Type);
        _ = _viewModel.Actions[2].Should().BeSameAs(second);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[1]);
        _ = _viewModel.Status.Should().Be("[Editor_StatusDuplicatedSelectedActions]");

        _viewModel.Undo();

        _ = _viewModel.Actions.Should().HaveCount(2);
        _ = _viewModel.Actions.Select(action => action.Type).Should().Equal(EditorActionType.MouseClick, EditorActionType.Delay);
        _ = _viewModel.Status.Should().Be("[Editor_StatusUndone]");
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

        _ = _viewModel.Actions.Should().Equal(second, first, third);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _ = _viewModel.SelectedAction.Should().BeSameAs(second);
        _ = _viewModel.Status.Should().Be("[Editor_StatusMovedSelectedActionsUp]");

        _viewModel.MoveSelectedActionsDown();

        _ = _viewModel.Actions.Should().Equal(first, second, third);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(second);
        _ = _viewModel.Status.Should().Be("[Editor_StatusMovedSelectedActionsDown]");

        _viewModel.Undo();

        _ = _viewModel.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.Delay,
            EditorActionType.MouseClick,
            EditorActionType.KeyPress);
        _ = _viewModel.Status.Should().Be("[Editor_StatusUndone]");
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
        _ = _viewModel.Actions.Should().Equal(second, fourth);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.HasSelectedAction.Should().BeFalse();
        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _ = _viewModel.Status.Should().Be("[Editor_StatusRemovedSelectedActions]");

        _viewModel.Undo();
        _ = _viewModel.Actions.Should().HaveCount(4);
    }

    [Fact]
    public void RemoveSelectedActions_WhenRemovingThousandsOfRows_PreservesUndoSnapshot()
    {
        var sequence = new MacroSequence { Name = "Large Macro" };
        var converted = new List<EditorAction>(capacity: 5_000);
        for (var index = 0; index < 5_000; index++)
        {
            converted.Add(new EditorAction { Type = EditorActionType.MouseMove, X = index, Y = index });
        }

        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));
        _viewModel.LoadMacroSequence(sequence);
        _viewModel.ReplaceSelectedActionUnderlyingIndices(Enumerable.Range(0, 5_000).Where(index => index % 2 is 0));

        var presentationResetCount = 0;
        _viewModel.ActionListItems.CollectionChanged += (_, args) =>
        {
            if (args.Action is NotifyCollectionChangedAction.Reset)
            {
                presentationResetCount++;
            }
        };

        _viewModel.RemoveSelectedActions();

        _ = presentationResetCount.Should().Be(1);
        _ = _viewModel.Actions.Should().HaveCount(2_500);
        _ = _viewModel.Actions[0].X.Should().Be(1);
        _ = _viewModel.Actions[^1].X.Should().Be(4_999);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();

        _viewModel.Undo();

        _ = _viewModel.Actions.Should().HaveCount(5_000);
        _ = _viewModel.Actions[0].X.Should().Be(0);
        _ = _viewModel.Actions[4_999].X.Should().Be(4_999);
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
        _ = _viewModel.Actions.Should().HaveCount(6);
        _ = _viewModel.Actions[0].Should().BeSameAs(first);
        _ = _viewModel.Actions[1].Should().BeSameAs(second);
        _ = _viewModel.Actions[2].Should().BeSameAs(third);
        _ = _viewModel.Actions[3].Should().NotBeSameAs(first);
        _ = _viewModel.Actions[3].Should().BeEquivalentTo(first, options => options.Excluding(action => action.Index).Excluding(action => action.Id));
        _ = _viewModel.Actions[4].Should().NotBeSameAs(third);
        _ = _viewModel.Actions[4].Should().BeEquivalentTo(third, options => options.Excluding(action => action.Index).Excluding(action => action.Id));
        _ = _viewModel.Actions[5].Should().BeSameAs(fourth);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(3, 4);
        _ = _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[3]);
        _ = _viewModel.Status.Should().Be("[Editor_StatusDuplicatedSelectedActions]");
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
        _ = _viewModel.Actions.Should().HaveCount(3);
        _ = _viewModel.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.MouseClick,
            EditorActionType.Delay,
            EditorActionType.KeyPress);
        _ = _viewModel.Status.Should().Be("[Editor_StatusUndone]");
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
        _ = _viewModel.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.MouseClick,
            EditorActionType.Delay,
            EditorActionType.KeyPress);
        _ = _viewModel.Status.Should().Be("[Editor_StatusUndone]");
    }

    [Fact]
    public void UndoAndRedo_RestorePreviousStates()
    {
        // Arrange
        _viewModel.AddAction();
        _viewModel.AddAction();
        _ = _viewModel.Actions.Should().HaveCount(2);
        _ = _viewModel.CanUndo.Should().BeTrue();
        _ = _viewModel.CanRedo.Should().BeFalse();

        // Act
        _viewModel.Undo();

        // Assert
        _ = _viewModel.Actions.Should().HaveCount(1);
        _ = _viewModel.Status.Should().Be("[Editor_StatusUndone]");
        _ = _viewModel.CanUndo.Should().BeTrue();
        _ = _viewModel.CanRedo.Should().BeTrue();

        // Act
        _viewModel.Redo();

        // Assert
        _ = _viewModel.Actions.Should().HaveCount(2);
        _ = _viewModel.Status.Should().Be("[Editor_StatusRedone]");
        _ = _viewModel.CanUndo.Should().BeTrue();
        _ = _viewModel.CanRedo.Should().BeFalse();
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
        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.SelectedAction!.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Undo_AfterCoordinateVariableEdit_RestoresLiteralToken()
    {
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();

        _viewModel.SelectedAction!.CoordinateXToken = "$found_x";
        _viewModel.Undo();

        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.SelectedAction.CoordinateXToken.Should().Be("0");
    }

    [Fact]
    public void Undo_AfterLiteralCoordinateTokenEdit_RestoresPreviousCoordinate()
    {
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();

        _viewModel.SelectedAction!.CoordinateXToken = "25";
        _viewModel.Undo();

        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.SelectedAction.CoordinateXToken.Should().Be("0");
        _ = _viewModel.SelectedAction.X.Should().Be(0);
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
        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.SelectedAction!.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Undo_AfterHistoryExceedsLimit_KeepsMostRecentStates()
    {
        // Arrange
        for (var index = 0; index < 52; index++)
        {
            _viewModel.AddAction();
        }

        _ = _viewModel.Actions.Should().HaveCount(52);

        // Act
        _viewModel.Undo();
        _viewModel.Undo();

        // Assert
        _ = _viewModel.Actions.Should().HaveCount(50);
    }

    [Fact]
    public void LoadMacroSequence_ClearsTrackedLoadedMacroSession()
    {
        // Arrange
        var sequence = new MacroSequence { Name = "Loaded Macro" };
        _viewModel.TrackLoadedMacroSession(Guid.NewGuid());
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(new List<EditorAction>(), new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _ = _viewModel.LinkedLoadedMacroSessionId.Should().BeNull();
    }

    [Fact]
    public void LoadMacroSequence_BatchesActionListPresentation()
    {
        var sequence = new MacroSequence { Name = "Loaded Macro" };
        var converted = new List<EditorAction>
        {
            new() { Type = EditorActionType.MouseMove, X = 10, Y = 20 },
            new() { Type = EditorActionType.MouseClick, X = 10, Y = 20 },
            new() { Type = EditorActionType.Delay, DelayMs = 25 },
        };
        var addedRowCount = 0;
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));
        _viewModel.ActionListItems.CollectionChanged += (_, args) =>
        {
            if (args.Action is NotifyCollectionChangedAction.Add)
            {
                addedRowCount += args.NewItems?.Count ?? 0;
            }
        };

        _viewModel.LoadMacroSequence(sequence);

        _ = addedRowCount.Should().Be(converted.Count);
    }

    [Fact]
    public void LoadMacroSequence_WhenLoadingFiveThousandActions_PresentsEachActionOnce()
    {
        var sequence = new MacroSequence { Name = "Large Macro" };
        var converted = new List<EditorAction>(capacity: 5_000);
        for (var index = 0; index < 5_000; index++)
        {
            converted.Add(new EditorAction { Type = EditorActionType.MouseMove, X = index, Y = index });
        }

        var addedRowCount = 0;
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));
        _viewModel.ActionListItems.CollectionChanged += (_, args) =>
        {
            if (args.Action is NotifyCollectionChangedAction.Add)
            {
                addedRowCount += args.NewItems?.Count ?? 0;
            }
        };

        _viewModel.LoadMacroSequence(sequence);

        _ = _viewModel.Actions.Should().HaveCount(5_000);
        _ = _viewModel.ActionListItems.Should().HaveCount(5_000);
        _ = addedRowCount.Should().Be(5_000);
    }

    [Fact]
    public void UndoAndRedo_WhenRestoringActionSnapshot_RebuildsPresentationOnce()
    {
        var sequence = new MacroSequence { Name = "Large Macro" };
        var converted = new List<EditorAction>(capacity: 250);
        for (var index = 0; index < 250; index++)
        {
            converted.Add(new EditorAction { Type = EditorActionType.MouseMove, X = index, Y = index });
        }

        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));
        _viewModel.LoadMacroSequence(sequence);
        _viewModel.AddAction();

        var addedRowCount = 0;
        _viewModel.ActionListItems.CollectionChanged += (_, args) =>
        {
            if (args.Action is NotifyCollectionChangedAction.Add)
            {
                addedRowCount += args.NewItems?.Count ?? 0;
            }
        };

        _viewModel.Undo();

        _ = _viewModel.Actions.Should().HaveCount(250);
        _ = _viewModel.ActionListItems.Should().HaveCount(250);
        _ = _viewModel.Actions[249].X.Should().Be(249);
        _ = _viewModel.Actions[249].Y.Should().Be(249);
        _ = _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[0]);
        _ = _viewModel.CanUndo.Should().BeTrue();
        _ = _viewModel.CanRedo.Should().BeTrue();
        _ = addedRowCount.Should().Be(250);

        addedRowCount = 0;
        _viewModel.Redo();

        _ = _viewModel.Actions.Should().HaveCount(251);
        _ = _viewModel.ActionListItems.Should().HaveCount(251);
        _ = _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[0]);
        _ = _viewModel.CanUndo.Should().BeTrue();
        _ = _viewModel.CanRedo.Should().BeFalse();
        _ = addedRowCount.Should().Be(251);
    }

    [Fact]
    public void UndoAndRedo_WhenRestoringFiveThousandActions_PreservesSnapshotContents()
    {
        var sequence = new MacroSequence { Name = "Large Macro" };
        var converted = new List<EditorAction>(capacity: 5_000);
        for (var index = 0; index < 5_000; index++)
        {
            converted.Add(new EditorAction { Type = EditorActionType.MouseMove, X = index, Y = index });
        }

        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));
        _viewModel.LoadMacroSequence(sequence);
        _viewModel.AddAction();

        _viewModel.Undo();

        _ = _viewModel.Actions.Should().HaveCount(5_000);
        _ = _viewModel.ActionListItems.Should().HaveCount(5_000);
        _ = _viewModel.Actions[0].X.Should().Be(0);
        _ = _viewModel.Actions[2_499].X.Should().Be(2_499);
        _ = _viewModel.Actions[4_999].X.Should().Be(4_999);
        _ = _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[0]);
        _ = _viewModel.CanUndo.Should().BeTrue();
        _ = _viewModel.CanRedo.Should().BeTrue();

        _viewModel.Redo();

        _ = _viewModel.Actions.Should().HaveCount(5_001);
        _ = _viewModel.ActionListItems.Should().HaveCount(5_001);
        _ = _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[0]);
        _ = _viewModel.CanUndo.Should().BeTrue();
        _ = _viewModel.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void LoadMacroSequence_LoadsConvertedActionsAndName()
    {
        // Arrange
        var sequence = new MacroSequence { Name = "Loaded Macro", SkipInitialZeroZero = true };
        var converted = new List<EditorAction>
        {
            new() { Type = EditorActionType.MouseMove, X = 10, Y = 20 },
        };
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _ = _viewModel.MacroName.Should().Be("Loaded Macro");
        _ = _viewModel.Actions.Should().HaveCount(1);
        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.HasActions.Should().BeTrue();
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
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 20,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    X = 5,
                    Y = -3,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };
        var converted = new List<EditorAction>
        {
            new() { Type = EditorActionType.MouseMove, X = 10, Y = 20, IsAbsolute = true },
            new() { Type = EditorActionType.MouseClick, X = 5, Y = -3, IsAbsolute = false },
        };
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: false));

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _ = _viewModel.Actions.Should().HaveCount(2);
        _ = _viewModel.Actions[0].IsAbsolute.Should().BeTrue();
        _ = _viewModel.Actions[1].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void LoadMacroSequence_WhenRestoreReturnsWarnings_ExposesWarningsInViewModel()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            Name = "Loaded Macro",
            ScriptSteps = { "tap ctrl+c" },
        };
        var converted = new List<EditorAction>
        {
            new() { Type = EditorActionType.RawScriptStep, Text = "tap ctrl+c" },
        };
        var warnings = new List<EditorActionRestoreWarning>
        {
            new(1, "tap ctrl+c", "Unsupported step restored as raw script text."),
        };
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, warnings, restoredFromScriptSteps: true));

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _ = _viewModel.HasLoadWarnings.Should().BeTrue();
        _ = _viewModel.LoadWarnings.Should().ContainSingle();
        _ = _viewModel.LoadWarnings[0].Should().Contain("Step 1");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenSuccessful_RaisesMacroCreatedWithSourcePath()
    {
        _viewModel.AddAction();
        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-raised-path.macro");

        var generatedSequence = new MacroSequence
        {
            Name = "Generated",
            Events = { new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 10, Y = 10 } },
        };
        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(generatedSequence);

        EditorMacroCreatedEventArgs? raisedArgs = null;
        _viewModel.MacroCreated += (_, args) => raisedArgs = args;

        await _viewModel.SaveMacroAsync();

        _ = raisedArgs.Should().NotBeNull();
        _ = raisedArgs!.Macro.Should().BeSameAs(generatedSequence);
        _ = raisedArgs.SourcePath.Should().Be("/tmp/editor-raised-path.macro");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenOnlyMouseClickAction_UsesCoordinateModeFromAction()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.IsAbsolute = true;

        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-click-absolute.macro");

        var generatedSequence = new MacroSequence
        {
            Name = "Generated",
            Events = { new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 10, Y = 10 } },
        };
        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(generatedSequence);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _ = _converter.Received(1).ToMacroSequence(
            Arg.Is<EditorMacroProjection>(projection =>
                projection.IsAbsoluteCoordinates && projection.Name == _viewModel.MacroName));
    }

    [Fact]
    public async Task SaveMacroAsync_WhenActionsUseMixedCoordinateModes_PassesActionsPreservingPerActionModes()
    {
        // Arrange
        var absoluteMove = new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 100, Y = 200 };
        var relativeMove = new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 5, Y = -3 };
        _viewModel.Actions.Add(absoluteMove);
        _viewModel.Actions.Add(relativeMove);

        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-mixed-modes.macro");

        EditorMacroProjection? capturedProjection = null;
        _ = _converter
            .ToMacroSequence(
                Arg.Do<EditorMacroProjection>(projection => capturedProjection = projection))
            .Returns(new MacroSequence { Name = "Generated" });

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _ = capturedProjection.Should().NotBeNull();
        _ = capturedProjection!.Actions.Should().HaveCount(2);
        _ = capturedProjection.Actions[0].Should().NotBeSameAs(absoluteMove);
        _ = capturedProjection.Actions[0].IsAbsolute.Should().BeTrue();
        _ = capturedProjection.Actions[0].X.Should().Be(100);
        _ = capturedProjection.Actions[0].Y.Should().Be(200);
        _ = capturedProjection.Actions[1].Should().NotBeSameAs(relativeMove);
        _ = capturedProjection.Actions[1].IsAbsolute.Should().BeFalse();
        _ = capturedProjection.Actions[1].X.Should().Be(5);
        _ = capturedProjection.Actions[1].Y.Should().Be(-3);
        _ = capturedProjection.IsAbsoluteCoordinates.Should().BeTrue();
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionClickExists_ForcesSkipInitialZeroZero()
    {
        // Arrange
        _viewModel.AddCurrentPositionClick();

        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-current-position-click.macro");

        var generatedSequence = new MacroSequence
        {
            Name = "Generated",
            Events = { new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 0, Y = 0 } },
        };
        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(generatedSequence);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _ = _converter.Received(1).ToMacroSequence(
            Arg.Is<EditorMacroProjection>(projection =>
                !projection.IsAbsoluteCoordinates && projection.SkipInitialZeroZero));
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionClickHasStaleAbsoluteCoordinates_NormalizesSnapshotBeforeValidationAndSave()
    {
        // Arrange
        var currentPositionClick = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.Left,
            UseCurrentPosition = true,
            IsAbsolute = true,
            X = 123,
            Y = 456,
        };
        _viewModel.Actions.Add(currentPositionClick);

        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-stale-current-position.macro");

        IReadOnlyList<EditorAction>? validatedActions = null;
        _ = _validator
            .ValidateAll(Arg.Do<IEnumerable<EditorAction>>(actions => validatedActions = actions.ToList()))
            .Returns((true, new List<string>()));

        EditorMacroProjection? convertedProjection = null;
        _ = _converter
            .ToMacroSequence(
                Arg.Do<EditorMacroProjection>(projection => convertedProjection = projection))
            .Returns(new MacroSequence { Name = "Generated" });

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _ = currentPositionClick.X.Should().Be(123);
        _ = currentPositionClick.Y.Should().Be(456);
        _ = currentPositionClick.IsAbsolute.Should().BeTrue();
        _ = validatedActions.Should().ContainSingle().Which.Should().NotBeSameAs(currentPositionClick);
        _ = validatedActions![0].IsAbsolute.Should().BeFalse();
        _ = validatedActions[0].X.Should().Be(0);
        _ = validatedActions[0].Y.Should().Be(0);
        _ = convertedProjection.Should().NotBeNull();
        _ = convertedProjection!.Actions.Should().ContainSingle().Which.Should().NotBeSameAs(currentPositionClick);
        _ = convertedProjection.Actions[0].IsAbsolute.Should().BeFalse();
        _ = convertedProjection.Actions[0].X.Should().Be(0);
        _ = convertedProjection.Actions[0].Y.Should().Be(0);
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionClickHasStaleCoordinatesAndDialogCancels_DoesNotMutateBoundAction()
    {
        // Arrange
        var currentPositionClick = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.Left,
            UseCurrentPosition = true,
            IsAbsolute = true,
            X = 123,
            Y = 456,
        };
        _viewModel.Actions.Add(currentPositionClick);

        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(new MacroSequence { Name = "Generated" });

        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns((string?)null);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _ = currentPositionClick.IsAbsolute.Should().BeTrue();
        _ = currentPositionClick.X.Should().Be(123);
        _ = currentPositionClick.Y.Should().Be(456);
    }

    [Fact]
    public async Task SaveMacroAsync_WhenConverterReturnsNull_DoesNotThrowOrMutateBoundAction()
    {
        // Arrange
        var currentPositionClick = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.Left,
            UseCurrentPosition = true,
            IsAbsolute = true,
            X = 123,
            Y = 456,
        };
        _viewModel.Actions.Add(currentPositionClick);

        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns((MacroSequence?)null);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _ = currentPositionClick.IsAbsolute.Should().BeTrue();
        _ = currentPositionClick.X.Should().Be(123);
        _ = currentPositionClick.Y.Should().Be(456);
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionClickIsFirstAndOtherActionsAreAbsolute_UsesAbsoluteMacroMode()
    {
        // Arrange
        _viewModel.AddCurrentPositionClick();
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.IsAbsolute = true;

        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/editor-viewmodel-current-position-with-absolute.macro");

        var generatedSequence = new MacroSequence
        {
            Name = "Generated",
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 0, Y = 0, UseCurrentPosition = true },
                new MacroEvent { Type = EventType.MouseMove, X = 120, Y = 90 },
            },
        };
        _ = _converter
            .ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(generatedSequence);

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _ = _converter.Received(1).ToMacroSequence(
            Arg.Is<EditorMacroProjection>(projection =>
                projection.IsAbsoluteCoordinates && projection.SkipInitialZeroZero));
    }
}
