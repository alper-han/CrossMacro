namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{

    [Fact]
    public void AddAction_AddsActionAndSelectsIt()
    {
        // Act
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.Actions.Should().HaveCount(1);
        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.HasActions.Should().BeTrue();
        _ = _viewModel.Status.Should().Contain("[Editor_StatusAddedAction]");
    }

    [Fact]
    public void NewActionGroup_WhenChanged_SelectsFirstActionInGroup()
    {
        var timingGroup = _viewModel.AddableActionGroups.Single(group =>
            group.Choices.Any(choice => choice.ActionType is EditorActionType.Delay));

        _viewModel.NewActionGroup = timingGroup;

        _ = _viewModel.NewActionChoice.Should().NotBeNull();
        _ = _viewModel.NewActionChoice!.ActionType.Should().Be(EditorActionType.Delay);
        _ = _viewModel.NewActionType.Should().Be(EditorActionType.Delay);
        _ = _viewModel.NewActionChoices.Should().Equal(timingGroup.Choices);
    }

    [Fact]
    public void NewActionType_WhenChanged_SynchronizesGroupedPicker()
    {
        _viewModel.NewActionType = EditorActionType.PixelSearch;

        _ = _viewModel.NewActionChoice.Should().NotBeNull();
        _ = _viewModel.NewActionChoice!.ActionType.Should().Be(EditorActionType.PixelSearch);
        _ = _viewModel.NewActionGroup.Should().NotBeNull();
        _ = _viewModel.NewActionGroup!.Choices.Select(choice => choice.ActionType).Should().Contain(EditorActionType.PixelSearch);
    }

    [Theory]
    [InlineData(EditorActionType.RawScriptStep)]
    [InlineData(EditorActionType.BlockEnd)]
    [InlineData(EditorActionType.ElseBlockStart)]
    public void AddAction_WhenNewActionTypeIsExcluded_DoesNotAddFallbackAction(EditorActionType excludedActionType)
    {
        _viewModel.NewActionType = excludedActionType;

        _viewModel.AddAction();

        _ = _viewModel.NewActionType.Should().Be(excludedActionType);
        _ = _viewModel.Actions.Should().BeEmpty();
        _ = _viewModel.Status.Should().Be("[Editor_StatusAutoManagedAction]");
    }

    [Theory]
    [InlineData(EditorActionType.ClipboardGet)]
    [InlineData(EditorActionType.ClipboardSet)]
    public void AddAction_ForClipboardActions_InitializesDefaults(EditorActionType actionType)
    {
        _viewModel.NewActionType = actionType;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.Type.Should().Be(actionType);
        if (actionType is EditorActionType.ClipboardGet)
        {
            _ = action.ScriptVariableName.Should().Be("clipboardText");
            _ = _viewModel.ShowClipboardGetFields.Should().BeTrue();
            _ = _viewModel.ShowTextInput.Should().BeFalse();
        }
        else
        {
            _ = action.Text.Should().Be("clipboard text");
            _ = _viewModel.ShowClipboardGetFields.Should().BeFalse();
            _ = _viewModel.ShowTextInput.Should().BeTrue();
            _ = _viewModel.TextInputLabel.Should().Be("Editor_ClipboardText");
        }
    }

    [Fact]
    public void AddAction_ForMousePosition_InitializesDestinationsAndShowsFields()
    {
        _viewModel.NewActionType = EditorActionType.MousePosition;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.MousePositionXVariableName.Should().Be("mouse_x");
        _ = action.MousePositionYVariableName.Should().Be("mouse_y");
        _ = _viewModel.ShowMousePositionFields.Should().BeTrue();
    }

    [Fact]
    public void AddAction_ForCopySelectionToVariable_InitializesShortcutAndDestination()
    {
        _viewModel.NewActionType = EditorActionType.CopySelectionToVariable;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.ClipboardCopyShortcut.Should().Be(ClipboardCopyShortcut.CtrlC);
        _ = action.ScriptVariableName.Should().Be("clipboardText");
        _ = _viewModel.ShowCopySelectionToVariableFields.Should().BeTrue();
        _ = _viewModel.ClipboardCopyShortcuts.Select(option => option.Value)
            .Should().Equal(ClipboardCopyShortcut.CtrlC, ClipboardCopyShortcut.CtrlShiftC);
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void AddAction_ForTargetColorActions_DefaultsTargetColorSourceToManualAndKeepsDefaultHex(EditorActionType actionType)
    {
        _viewModel.NewActionType = actionType;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.ScreenColorHex.Should().Be("FFFFFF");
        _ = action.ScreenTargetColorSource.Should().Be(EditorActionScreenTargetColorSource.ManualHex);
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

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _ = _viewModel.SelectedAction.Should().BeSameAs(move);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
        _ = _viewModel.HasSelectedAction.Should().BeTrue();
        _ = _viewModel.HasSelectedActions.Should().BeTrue();
        _ = _viewModel.SelectedActionCount.Should().Be(2);
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

        _viewModel.ReplaceSelectedActionUnderlyingIndices([1]);

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(click);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[1]);
        _ = _viewModel.HasSelectedAction.Should().BeTrue();
        _ = _viewModel.HasSelectedActions.Should().BeTrue();
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
            if (string.Equals(args.PropertyName, nameof(EditorViewModel.SelectedAction), StringComparison.Ordinal))
            {
                observedSelectedActions.Add(_viewModel.SelectedAction);
            }
        };

        _viewModel.ReplaceSelectedActionUnderlyingIndices([1, 2]);

        _ = observedSelectedActions.Should().NotContainNulls();
        _ = observedSelectedActions.Should().ContainSingle().Which.Should().BeSameAs(click);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1, 2);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[1]);
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

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(click);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[1]);
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

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.HasSelectedActions.Should().BeTrue();
        _ = _viewModel.SelectedActionCount.Should().Be(2);
    }

    [Fact]
    public void SelectedActionCommandStateProperties_ReflectSelectedUnderlyingIndices()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });

        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.SelectedActionCount.Should().Be(0);
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();

        _viewModel.SelectedActionUnderlyingIndices.Add(1);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        _ = _viewModel.HasSelectedActions.Should().BeTrue();
        _ = _viewModel.SelectedActionCount.Should().Be(2);
        _ = _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _ = _viewModel.CanMoveSelectedActionsUp.Should().BeTrue();
        _ = _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
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
            if (args.PropertyName is not null)
            {
                notifications.Add(args.PropertyName);
            }
        };

        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();

        _viewModel.SelectedActionUnderlyingIndices.Add(1);

        _ = _viewModel.SelectedActionCount.Should().Be(1);
        _ = _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _ = _viewModel.CanMoveSelectedActionsUp.Should().BeTrue();
        _ = _viewModel.CanMoveSelectedActionsDown.Should().BeTrue();
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsUp));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));

        notifications.Clear();
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        _ = _viewModel.SelectedActionCount.Should().Be(2);
        _ = _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _ = _viewModel.CanMoveSelectedActionsUp.Should().BeTrue();
        _ = _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));

        notifications.Clear();
        _viewModel.SelectedActionUnderlyingIndices.Clear();

        _ = _viewModel.SelectedActionCount.Should().Be(0);
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsUp));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));
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
            if (args.PropertyName is not null)
            {
                notifications.Add(args.PropertyName);
            }
        };

        _viewModel.Actions.RemoveAt(1);

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsUp));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));
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
            if (args.PropertyName is not null)
            {
                notifications.Add(args.PropertyName);
            }
        };

        HideMovementAndShortWaitRows();

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(click);
        _ = _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _ = _viewModel.CanMoveSelectedActionsUp.Should().BeFalse();
        _ = _viewModel.CanMoveSelectedActionsDown.Should().BeFalse();
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsUp));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanMoveSelectedActionsDown));

        notifications.Clear();
        _viewModel.HideShortWaits = false;
        _viewModel.SimplifyMovement = true;

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _ = _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _ = _viewModel.CanDuplicateSelectedActions.Should().BeTrue();
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanRemoveSelectedActions));
        _ = notifications.Should().Contain(nameof(EditorViewModel.CanDuplicateSelectedActions));
    }

    [Fact]
    public void ReplaceSelectedActionUnderlyingIndices_WhenSelectionCleared_ClearsSelectedActionAndHidesProperties()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        _viewModel.Actions.Add(action);
        _viewModel.ReplaceSelectedActionUnderlyingIndices([0]);

        _ = _viewModel.SelectedAction.Should().BeSameAs(action);
        _ = _viewModel.SelectedActionListItem.Should().NotBeNull();
        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeTrue();

        _viewModel.ReplaceSelectedActionUnderlyingIndices([]);

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.HasSelectedAction.Should().BeFalse();
        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
    }

    [Fact]
    public void TryDeselectSelectedSourceAction_WhenSelectedSourceRowClicked_RemovesRowFromListBoxSelection()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var row = CreateActionListItem(action, representsSourceAction: true);
        var listBox = new ListBox { SelectionMode = SelectionMode.Multiple };
        _ = listBox.SelectedItems!.Add(row);

        var removed = ListBoxSelectedActionIndices.TryDeselectSelectedSourceAction(listBox, row);

        _ = removed.Should().BeTrue();
        _ = listBox.SelectedItems!.Cast<object>().Should().BeEmpty();
    }

    [Fact]
    public void TryDeselectSelectedSourceAction_WhenRowIsNotSourceAction_DoesNotChangeSelection()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var row = CreateActionListItem(action, representsSourceAction: false);
        var listBox = new ListBox { SelectionMode = SelectionMode.Multiple };
        _ = listBox.SelectedItems!.Add(row);

        var removed = ListBoxSelectedActionIndices.TryDeselectSelectedSourceAction(listBox, row);

        _ = removed.Should().BeFalse();
        _ = listBox.SelectedItems!.Cast<object>().Should().ContainSingle().Which.Should().BeSameAs(row);
    }

    [Fact]
    public void SelectedUnderlyingIndices_WhenListBoxReattaches_RestoresCollectionHandler()
    {
        var listBox = new ListBox { SelectionMode = SelectionMode.Multiple };
        var selectedIndices = new ObservableCollection<int> { 0 };
        ListBoxSelectedActionIndices.SetSelectedUnderlyingIndices(listBox, selectedIndices);

        var behaviorType = typeof(ListBoxSelectedActionIndices);
        var handlerProperty = (AvaloniaProperty<NotifyCollectionChangedEventHandler?>)behaviorType
            .GetField("BoundSelectionChangedHandlerProperty", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        _ = listBox.GetValue(handlerProperty).Should().NotBeNull();

        _ = behaviorType
            .GetMethod("OnDetachedFromVisualTree", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [listBox, null]);
        _ = listBox.GetValue(handlerProperty).Should().BeNull();

        var attachedHandler = behaviorType.GetMethod("OnAttachedToVisualTree", BindingFlags.NonPublic | BindingFlags.Static);
        _ = attachedHandler.Should().NotBeNull();
        _ = attachedHandler!.Invoke(null, [listBox, null]);

        _ = listBox.GetValue(handlerProperty).Should().NotBeNull();
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
        _viewModel.ReplaceSelectedActionUnderlyingIndices([1]);
        var previousSelectedRow = _viewModel.ActionListItems[1];

        selected.X = 25;
        var currentSelectedRow = _viewModel.ActionListItems[1];

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(selected);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(currentSelectedRow);
        _ = _viewModel.SelectedActionListItem.Should().NotBeSameAs(previousSelectedRow);
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
        _viewModel.ReplaceSelectedActionUnderlyingIndices([0, 2]);

        first.X = 11;

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _ = _viewModel.SelectedAction.Should().BeSameAs(first);
        _ = _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(0, 1, 2);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
    }

    [Fact]
    public void SelectedActionUnderlyingIndices_WhenSelectedRowsAreHidden_DoesNotClearUnderlyingSelection()
    {
        var hiddenMove = new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 1 };
        var visibleClick = new EditorAction { Type = EditorActionType.MouseClick, X = 2, Y = 2 };
        _viewModel.Actions.Add(hiddenMove);
        _viewModel.Actions.Add(visibleClick);
        _viewModel.ReplaceSelectedActionUnderlyingIndices([0]);

        _viewModel.HideMouseMoves = true;

        _ = _viewModel.ActionListItems.Should().ContainSingle().Which.Action.Should().BeSameAs(visibleClick);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
    }

    [Fact]
    public void SelectedActionProperties_WhenMultipleMixedActionsSelected_AreHidden()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 1 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });

        _viewModel.ReplaceSelectedActionUnderlyingIndices([0, 1]);

        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _ = _viewModel.ShowBatchDelayProperties.Should().BeFalse();
        _ = _viewModel.SelectedAction.Should().BeSameAs(_viewModel.Actions[0]);
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
        _viewModel.ReplaceSelectedActionUnderlyingIndices([0, 1]);

        _viewModel.BatchDelayMs = 75;

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(first);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
        _ = _viewModel.ShowBatchDelayProperties.Should().BeTrue();
        _ = _viewModel.BatchDelayMs.Should().Be(75);
    }

    [Fact]
    public void AddAction_WhenBlockStartAdded_AutoInsertsMatchingEnd()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.IfBlockStart;

        // Act
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.Actions.Should().HaveCount(2);
        _ = _viewModel.Actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = _viewModel.Actions[1].Type.Should().Be(EditorActionType.BlockEnd);
        _ = _viewModel.ActionListItems[1].DisplayName.Should().Be("End IfToken");
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
        _ = _viewModel.Actions.Should().BeEmpty();
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
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
        _ = _viewModel.Actions.Should().HaveCount(3);
        _ = _viewModel.Actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = _viewModel.Actions[1].Type.Should().Be(actionType);
        _ = _viewModel.Actions[2].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void AddAction_WhenActionSelected_InsertsAfterSelection()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[0];
        _viewModel.NewActionType = EditorActionType.Delay;

        // Act
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.Actions.Should().HaveCount(3);
        _ = _viewModel.Actions[1].Type.Should().Be(EditorActionType.Delay);
        _ = _viewModel.SelectedAction.Should().Be(_viewModel.Actions[1]);
    }

    [Fact]
    public void RemoveAction_WhenSelected_RemovesAndClearsSelection()
    {
        // Arrange
        _viewModel.AddAction();
        _ = _viewModel.Actions.Should().HaveCount(1);

        // Act
        _viewModel.RemoveAction();

        // Assert
        _ = _viewModel.Actions.Should().BeEmpty();
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.HasSelectedAction.Should().BeFalse();
        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.HasActions.Should().BeFalse();
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _ = _viewModel.ShowBatchDelayProperties.Should().BeFalse();
        _ = _viewModel.Status.Should().Be("[Editor_StatusRemovedAction]");
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
        _ = _viewModel.Actions.Should().Equal(first, third);
        _ = _viewModel.Actions.Should().NotContain(second);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.HasSelectedAction.Should().BeFalse();
        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _ = _viewModel.ShowBatchDelayProperties.Should().BeFalse();
        _ = _viewModel.ShowMultiSelectionPropertiesHint.Should().BeFalse();
        _ = _viewModel.Status.Should().Be("[Editor_StatusRemovedAction]");
    }

    [Fact]
    public void RemoveSelectedActions_WhenMultipleRowsRemoved_RebuildsPresentationOnce()
    {
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 };
        var fourth = new EditorAction { Type = EditorActionType.MouseClick, X = 4, Y = 4 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.Actions.Add(fourth);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

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
        _ = _viewModel.Actions.Should().Equal(second, fourth);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.CanUndo.Should().BeTrue();

        _viewModel.Undo();
        _ = _viewModel.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.MouseClick,
            EditorActionType.Delay,
            EditorActionType.KeyPress,
            EditorActionType.MouseClick);
        _ = _viewModel.Actions.Select(action => action.X).Should().Equal(1, 0, 0, 4);
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
        _ = _viewModel.Actions.Should().ContainSingle().Which.Should().BeSameAs(first);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.HasSelectedAction.Should().BeFalse();
        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
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

        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.HasSelectedActions.Should().BeTrue();

        // Act
        _viewModel.RemoveSelectedActions();

        // Assert
        _ = _viewModel.Actions.Should().ContainSingle().Which.Should().BeSameAs(secondHiddenMove);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.HasSelectedAction.Should().BeFalse();
        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _ = _viewModel.ShowBatchDelayProperties.Should().BeFalse();
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
        _ = _viewModel.Actions.Should().BeEmpty();
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
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
        _ = _viewModel.Actions.Should().Equal(second, third, first, fourth);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(second);
        _ = _viewModel.Status.Should().Be("[Editor_StatusMovedSelectedActionsUp]");
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
        _ = _viewModel.Actions.Should().Equal(first, fourth, second, third);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(2, 3);
        _ = _viewModel.SelectedAction.Should().BeSameAs(second);
        _ = _viewModel.Status.Should().Be("[Editor_StatusMovedSelectedActionsDown]");
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
        _ = _viewModel.Actions.Should().Equal(second, first, fourth, third, fifth);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _ = _viewModel.SelectedAction.Should().BeSameAs(second);
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
        _ = _viewModel.Actions.Should().Equal(first, third, second, fifth, fourth);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(2, 4);
        _ = _viewModel.SelectedAction.Should().BeSameAs(second);
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
        _ = _viewModel.Actions.Should().Equal(first, second, third);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _viewModel.CanUndo.Should().BeFalse();
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
        _ = _viewModel.Actions.Should().Equal(first, second, third);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _viewModel.CanUndo.Should().BeFalse();
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
        _ = _viewModel.Actions.Should().Equal(ifStart, ifBody, ifEnd, elseStart, elseBody, elseEnd);
        _ = _viewModel.SelectedAction.Should().BeSameAs(ifEnd);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(2);
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _viewModel.CanUndo.Should().BeFalse();
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
        _ = _viewModel.Actions.Should().Equal(zeroDelay, tenMsDelay, randomShortDelay, click);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");

        _viewModel.Undo();
        _ = _viewModel.Actions.Should().HaveCount(6);
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
        _ = _viewModel.Actions.Should().ContainSingle().Which.Should().BeSameAs(click);
        _ = _viewModel.SelectedAction.Should().BeNull();
        _ = _viewModel.SelectedActionListItem.Should().BeNull();
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().BeEmpty();
        _ = _viewModel.HasSelectedAction.Should().BeFalse();
        _ = _viewModel.HasSelectedActions.Should().BeFalse();
        _ = _viewModel.CanRemoveSelectedActions.Should().BeFalse();
        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _ = _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");
    }

    [Fact]
    public void DeleteHiddenEvents_WhenOnlySimplifyMovementEnabled_DoesNotDeleteSimplifiedRows()
    {
        AddCondensibleRun(_viewModel, 6);
        var originalActions = _viewModel.Actions.ToArray();
        _viewModel.SimplifyMovement = true;

        _ = _viewModel.ActionListItems.Should().ContainSingle();
        _viewModel.DeleteHiddenEvents();

        _ = _viewModel.Actions.Should().Equal(originalActions);
        _ = _viewModel.Status.Should().Be("[Editor_StatusNoHiddenEventsToDelete]");
        _ = _viewModel.CanUndo.Should().BeFalse();
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
        _ = _viewModel.Actions.Should().Equal(zeroDelay, tenMsDelay, randomShortDelay);
        _ = _viewModel.Status.Should().Be("[Editor_StatusNoHiddenEventsToDelete]");
        _ = _viewModel.CanUndo.Should().BeFalse();
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

        _ = _viewModel.Actions.Should().Equal(down, up);
        _ = _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");
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
        _ = _viewModel.Actions.Should().HaveCount(5);
        _ = _viewModel.Actions[2].Type.Should().Be(EditorActionType.BlockEnd);
        _ = _viewModel.Actions[3].Type.Should().Be(EditorActionType.ElseBlockStart);
        _ = _viewModel.Actions[4].Type.Should().Be(EditorActionType.BlockEnd);
        _ = _viewModel.Status.Should().Be("[Editor_StatusInsertedElseBlock]");
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
        _ = _viewModel.Actions.Should().HaveCount(2);
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
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
        _ = _viewModel.Actions.Should().HaveCount(1);
        _ = _viewModel.Actions[0].Type.Should().Be(EditorActionType.MouseClick);
        _ = _viewModel.Status.Should().Be("[Editor_StatusRemovedBlock]");
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
        _ = _viewModel.Actions.Should().BeEmpty();
        _ = _viewModel.Status.Should().Be("[Editor_StatusRemovedBlock]");
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
        _ = _viewModel.Actions.Should().Equal(blockStart, blockEnd);
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _viewModel.CanUndo.Should().BeTrue();
        _viewModel.Undo();
        _ = _viewModel.Actions.Should().BeEmpty();
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
        _ = _viewModel.Actions.Should().Equal(noiseMove, unmatchedBlockEnd);
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
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
        NotifyCollectionChangedEventHandler onActionListChanged = (_, _) => collectionChangeCount++;
        _viewModel.ActionListItems.CollectionChanged += onActionListChanged;

        // Act
        try
        {
            removedAction.X++;
            removedAction.Y++;
        }
        finally
        {
            _viewModel.ActionListItems.CollectionChanged -= onActionListChanged;
        }

        // Assert
        _ = collectionChangeCount.Should().Be(0);
        _ = _viewModel.ActionListItems.Should().HaveCount(1);
        _ = _viewModel.ActionListItems[0].Action.Should().BeSameAs(_viewModel.Actions[0]);
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
        _ = moveAction.IsAbsolute.Should().BeFalse();
        _ = clickAction.IsAbsolute.Should().BeFalse();
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
        _ = _viewModel.Actions.Should().HaveCount(3);
        _ = _viewModel.Actions[0].Should().BeSameAs(moveAction);
        _ = _viewModel.Actions[1].Should().BeSameAs(delayAction);
        _ = _viewModel.SelectedAction!.IsAbsolute.Should().BeFalse();
        _ = moveAction.IsAbsolute.Should().BeFalse();
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
        _ = _viewModel.Actions.Should().HaveCount(3);
        _ = _viewModel.Actions[1].Should().Be(_viewModel.SelectedAction);
        _ = _viewModel.SelectedAction!.IsAbsolute.Should().BeFalse();
        _ = laterMoveAction.IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void AddAction_WhenNoCoordinateModeSource_DefaultsToAbsolute()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseMove;

        // Act
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.SelectedAction!.IsAbsolute.Should().BeTrue();
    }

    [Fact]
    public void SelectedActionCoordinateMode_WhenChangedToRelative_UsesLiveCursorStart()
    {
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();

        _viewModel.SelectedActionIsRelative = true;

        _ = _viewModel.SelectedAction!.IsAbsolute.Should().BeFalse();
        _ = _viewModel.SelectedAction.CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = _viewModel.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void SelectedActionCoordinateMode_WhenChangedToLogicalRelative_UsesLogicalPixelSpace()
    {
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();

        _viewModel.SelectedActionIsLogicalRelative = true;

        _ = _viewModel.SelectedAction!.IsAbsolute.Should().BeFalse();
        _ = _viewModel.SelectedAction.CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = _viewModel.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void Undo_WhenRelativeMouseMoveIsEdited_PreservesItsLiveCursorStartMode()
    {
        _viewModel.NewActionType = EditorActionType.MouseMove;
        _viewModel.AddAction();
        _viewModel.SelectedActionIsRelative = true;
        _viewModel.SelectedAction!.X = 3;

        _viewModel.Undo();

        _ = _viewModel.SelectedAction!.IsAbsolute.Should().BeFalse();
        _ = _viewModel.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void AddCurrentPositionClick_AddsRelativeClickAndEnablesSkipInitialZeroZero()
    {
        // Act
        _viewModel.AddCurrentPositionClick();

        // Assert
        _ = _viewModel.Actions.Should().HaveCount(1);
        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.SelectedAction!.Type.Should().Be(EditorActionType.MouseClick);
        _ = _viewModel.SelectedAction.UseCurrentPosition.Should().BeTrue();
        _ = _viewModel.SelectedAction.IsAbsolute.Should().BeFalse();
        _ = _viewModel.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void CurrentPositionClick_HidesCoordinateInputsAndCoordinateModeToggle()
    {
        // Arrange
        _viewModel.AddCurrentPositionClick();

        // Assert
        _ = _viewModel.ShowCoordinates.Should().BeFalse();
        _ = _viewModel.ShowCoordModeToggle.Should().BeFalse();
        _ = _viewModel.ShowCurrentPositionToggle.Should().BeTrue();
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
        _ = clickAction.IsAbsolute.Should().BeFalse();
        _ = moveAction.IsAbsolute.Should().BeTrue();
        _ = _viewModel.SkipInitialZeroZero.Should().BeTrue();
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
        _ = moveAction.IsAbsolute.Should().BeTrue();
        _ = clickAction.IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void CurrentPositionClick_WhenDisabled_RestoresPreviousSkipInitialZeroZeroValue()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseClick;
        _viewModel.AddAction();
        var clickAction = _viewModel.SelectedAction!;
        _ = _viewModel.SkipInitialZeroZero.Should().BeFalse();

        // Act
        clickAction.UseCurrentPosition = true;
        clickAction.UseCurrentPosition = false;

        // Assert
        _ = _viewModel.SkipInitialZeroZero.Should().BeFalse();
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
        _ = clickAction.IsAbsolute.Should().BeFalse();
        _ = _viewModel.SkipInitialZeroZero.Should().BeFalse();

        // Act
        clickAction.UseCurrentPosition = true;
        clickAction.UseCurrentPosition = false;

        // Assert
        _ = _viewModel.SkipInitialZeroZero.Should().BeFalse();
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
        _ = moveAction.IsAbsolute.Should().BeTrue();
        _ = clickAction.IsAbsolute.Should().BeTrue();

        // Act
        clickAction.IsAbsolute = false;

        // Assert
        _ = moveAction.IsAbsolute.Should().BeTrue();
        _ = clickAction.IsAbsolute.Should().BeFalse();
    }
}
