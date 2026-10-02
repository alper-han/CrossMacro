using Avalonia.Controls;
using Avalonia.VisualTree;
using CrossMacro.UI.Tests.Services;
using CrossMacro.UI.Views.Tabs;

namespace CrossMacro.UI.Tests.ViewModels;

[Collection("Headless startup")]
public sealed partial class EditorViewModelTests
{
    [Fact]
    public Task MovementCondensationThreshold_IsVisibleBeforePreviewIsEnabled() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(_ =>
        {
            _viewModel.Actions.Add(AbsoluteMove(100, 100));
            _viewModel.Actions.Add(FixedDelay(12_000));
            _viewModel.Actions.Add(AbsoluteMove(120, 120));

            _viewModel.MovementCondensationThresholdMilliseconds.Should().Be(10);
            _viewModel.CanSimplifyMovement.Should().BeFalse();
            _viewModel.ShowSimplifyMovementToggle.Should().BeFalse();

            var view = new EditorDocumentView { DataContext = _viewModel };
            var window = new Window { Content = view };
            window.Show();
            try
            {
                var thresholdInput = view.GetVisualDescendants()
                    .OfType<NumericUpDown>()
                    .Should()
                    .ContainSingle()
                    .Which;

                thresholdInput.IsEffectivelyVisible.Should().BeTrue();
                thresholdInput.Value.Should().Be(10m);
                thresholdInput.Bounds.Width.Should().BeGreaterThan(100);
                thresholdInput.Value = 20m;

                _viewModel.MovementCondensationThresholdMilliseconds.Should().Be(20);
                _viewModel.ShowSimplifyMovementToggle.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });


    [Fact]
    public void SimplifyMovement_ShowsSafeFinalMoveWithoutChangingActions()
    {
        var first = AbsoluteMove(100, 100);
        var middle = AbsoluteMove(110, 110);
        var last = AbsoluteMove(120, 120);
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(FixedDelay(4_000));
        _viewModel.Actions.Add(middle);
        _viewModel.Actions.Add(FixedDelay(3_000));
        _viewModel.Actions.Add(last);

        var original = _viewModel.Actions.ToArray();
        _viewModel.MovementCondensationThresholdMilliseconds = 10;
        _viewModel.SimplifyMovement = true;

        _viewModel.ActionListItems.Should().ContainSingle()
            .Which.Action.Should().BeSameAs(last);
        _viewModel.Actions.Should().Equal(original);
        _viewModel.CanApplyMovementCondensation.Should().BeTrue();
    }

    [Fact]
    public void ApplyMovementCondensation_RemovesOnlyInternalIdleRowsAndUndoRestoresAllActions()
    {
        var leadingWait = FixedDelay(30_000);
        var first = AbsoluteMove(100, 100);
        var firstGap = FixedDelay(4_000);
        var middle = AbsoluteMove(110, 110);
        var secondGap = FixedDelay(3_000);
        var last = AbsoluteMove(120, 120);
        var trailingWait = FixedDelay(50_000);
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 130, Y = 130 };
        _viewModel.Actions.Add(leadingWait);
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(firstGap);
        _viewModel.Actions.Add(middle);
        _viewModel.Actions.Add(secondGap);
        _viewModel.Actions.Add(last);
        _viewModel.Actions.Add(trailingWait);
        _viewModel.Actions.Add(click);
        var original = _viewModel.Actions.ToArray();
        _viewModel.MovementCondensationThresholdMilliseconds = 10;

        _viewModel.ApplyMovementCondensation();

        _viewModel.Actions.Should().Equal(leadingWait, last, trailingWait, click);
        _viewModel.CanUndo.Should().BeTrue();
        _viewModel.Undo();

        _viewModel.Actions.Select(action => (action.Type, action.X, action.Y, action.DelayMicroseconds, action.Button)).Should().Equal(
            original.Select(action => (action.Type, action.X, action.Y, action.DelayMicroseconds, action.Button)));
    }

    [Fact]
    public void DeleteHiddenEvents_PreservesEveryMoveAndWaitInsideMultiButtonDrag()
    {
        var dragStart = new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Left };
        var firstDragMove = AbsoluteMove(100, 100);
        var dragWait = FixedDelay(5_000);
        var secondButtonDown = new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Right };
        var secondButtonUp = new EditorAction { Type = EditorActionType.MouseUp, Button = MacroMouseButton.Right };
        var secondDragMove = AbsoluteMove(110, 110);
        var dragEnd = new EditorAction { Type = EditorActionType.MouseUp, Button = MacroMouseButton.Left };
        var idleMove = AbsoluteMove(300, 300);
        var idleWait = FixedDelay(5_000);
        var idleFinalMove = AbsoluteMove(320, 320);
        foreach (var action in new[]
        {
            dragStart, firstDragMove, dragWait, secondButtonDown, secondButtonUp,
            secondDragMove, dragEnd, idleMove, idleWait, idleFinalMove,
        })
        {
            _viewModel.Actions.Add(action);
        }

        _viewModel.HideMouseMoves = true;
        _viewModel.HideShortWaits = true;
        _viewModel.DeleteHiddenEvents();

        _viewModel.Actions.Should().Equal(
            dragStart, firstDragMove, dragWait, secondButtonDown, secondButtonUp,
            secondDragMove, dragEnd);
    }

    private static EditorAction AbsoluteMove(int x, int y) => new()
    {
        Type = EditorActionType.MouseMove,
        X = x,
        Y = y,
        IsAbsolute = true,
    };

    private static EditorAction FixedDelay(long microseconds) => new()
    {
        Type = EditorActionType.Delay,
        DelayMicroseconds = microseconds,
    };
    [Fact]
    public Task MovementCleanupControls_HideWhenEmptyAndKeepFiltersForLaterActions() =>
        DesktopQuickSetupDialogTests.RunHeadlessAsync(_ =>
        {
            _viewModel.HideMouseMoves = true;
            _viewModel.HideShortWaits = true;
            _viewModel.SimplifyMovement = true;
            _viewModel.MovementCondensationThresholdMilliseconds = 25;

            var view = new EditorDocumentView { DataContext = _viewModel };
            var window = new Window { Content = view };
            window.Show();
            try
            {
                var thresholdInput = view.GetVisualDescendants()
                    .OfType<NumericUpDown>()
                    .Should()
                    .ContainSingle()
                    .Subject;

                thresholdInput.IsEffectivelyVisible.Should().BeFalse();
                _viewModel.HideMouseMoves.Should().BeTrue();
                _viewModel.HideShortWaits.Should().BeTrue();
                _viewModel.SimplifyMovement.Should().BeTrue();
                _viewModel.MovementCondensationThresholdMilliseconds.Should().Be(25);

                _viewModel.Actions.Add(AbsoluteMove(100, 100));
                _viewModel.Actions.Add(AbsoluteMove(120, 120));

                _viewModel.ActionListItems.Should().BeEmpty();
                thresholdInput.IsEffectivelyVisible.Should().BeTrue();
                thresholdInput.Value.Should().Be(25m);
                _viewModel.HideMouseMoves.Should().BeTrue();
                _viewModel.HideShortWaits.Should().BeTrue();
                _viewModel.SimplifyMovement.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
            return Task.CompletedTask;
        });
}
