using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
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
    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly ILocalizationService _localizationService;
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
        _keyCodeMapper.GetKeyName(Arg.Any<int>()).Returns("A");
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
            _localizationService,
            new EditorActionDisplayFormatter(_localizationService));
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
        var listBox = new ListBox { SelectionMode = SelectionMode.Multiple, ItemsSource = new[] { row } };
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
        var listBox = new ListBox { SelectionMode = SelectionMode.Multiple, ItemsSource = new[] { row } };
        listBox.SelectedItems!.Add(row);

        var removed = ListBoxSelectedActionIndices.TryDeselectSelectedSourceAction(listBox, row);

        removed.Should().BeFalse();
        listBox.SelectedItems!.Cast<object>().Should().ContainSingle().Which.Should().BeSameAs(row);
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
        _viewModel.HasActions.Should().BeFalse();
        _viewModel.Status.Should().Be("[Editor_StatusRemovedAction]");
    }

    [Fact]
    public void RemoveSelectedActions_WhenMultipleSourceRowsSelected_RemovesDescendingWithOneUndoStateAndSelectsLowestRemainingIndex()
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
        _viewModel.SelectedAction.Should().BeSameAs(second);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _viewModel.Status.Should().Be("[Editor_StatusRemovedSelectedActions]");

        _viewModel.Undo();
        _viewModel.Actions.Should().HaveCount(4);
    }

    [Fact]
    public void RemoveSelectedActions_WhenSelectionRemovesTail_SelectsPreviousLastAction()
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
        _viewModel.SelectedAction.Should().BeSameAs(first);
        _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
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
        _viewModel.SelectedAction.Should().BeSameAs(zeroDelay);
        _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");

        _viewModel.Undo();
        _viewModel.Actions.Should().HaveCount(6);
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

        // Act
        _viewModel.Undo();

        // Assert
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.Status.Should().Be("[Editor_StatusUndone]");

        // Act
        _viewModel.Redo();

        // Assert
        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.Status.Should().Be("[Editor_StatusRedone]");
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
        _viewModel.Status.Should().Be("[Editor_StatusCaptureSelectionChanged]");
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
