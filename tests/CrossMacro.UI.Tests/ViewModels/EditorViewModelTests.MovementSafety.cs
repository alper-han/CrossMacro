namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{
    public static TheoryData<string, EditorAction[]> UncertainDragPaths => new()
    {
        {
            "conditional release",
            [
                ButtonAction(EditorActionType.MouseDown),
                FlowAction(EditorActionType.IfBlockStart, "1 == 0"),
                ButtonAction(EditorActionType.MouseUp),
                FlowAction(EditorActionType.BlockEnd),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseUp),
            ]
        },
        {
            "loop-carried press",
            [
                FlowAction(EditorActionType.RepeatBlockStart, "2"),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseDown),
                FlowAction(EditorActionType.BlockEnd),
                ButtonAction(EditorActionType.MouseUp),
            ]
        },
        {
            "break bypasses release",
            [
                FlowAction(EditorActionType.RepeatBlockStart, "2"),
                ButtonAction(EditorActionType.MouseDown),
                FlowAction(EditorActionType.IfBlockStart, "1 == 0"),
                FlowAction(EditorActionType.Break),
                FlowAction(EditorActionType.BlockEnd),
                ButtonAction(EditorActionType.MouseUp),
                FlowAction(EditorActionType.BlockEnd),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseUp),
            ]
        },
        {
            "continue bypasses release",
            [
                FlowAction(EditorActionType.RepeatBlockStart, "2"),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseDown),
                FlowAction(EditorActionType.Continue),
                ButtonAction(EditorActionType.MouseUp),
                FlowAction(EditorActionType.BlockEnd),
                ButtonAction(EditorActionType.MouseUp),
            ]
        },
        {
            "opaque script press",
            [
                FlowAction(EditorActionType.RawScriptStep, "down current left"),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Left),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Right),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Middle),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side1),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side2),
            ]
        },
        {
            "None means left in script playback",
            [
                FlowAction(EditorActionType.IfBlockStart, "1 == 1"),
                ButtonAction(EditorActionType.MouseDown, MacroMouseButton.None),
                FlowAction(EditorActionType.BlockEnd),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.None),
            ]
        },
        {
            "if/else can leave a button held",
            [
                FlowAction(EditorActionType.IfBlockStart, "1 == 0"),
                ButtonAction(EditorActionType.MouseDown),
                FlowAction(EditorActionType.BlockEnd),
                FlowAction(EditorActionType.ElseBlockStart),
                ButtonAction(EditorActionType.MouseUp),
                FlowAction(EditorActionType.BlockEnd),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseUp),
            ]
        },
        {
            "nested-loop break bypasses release",
            [
                FlowAction(EditorActionType.RepeatBlockStart, "2"),
                FlowAction(EditorActionType.RepeatBlockStart, "2"),
                ButtonAction(EditorActionType.MouseDown),
                FlowAction(EditorActionType.IfBlockStart, "1 == 0"),
                FlowAction(EditorActionType.Break),
                FlowAction(EditorActionType.BlockEnd),
                ButtonAction(EditorActionType.MouseUp),
                FlowAction(EditorActionType.BlockEnd),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                FlowAction(EditorActionType.BlockEnd),
                ButtonAction(EditorActionType.MouseUp),
            ]
        },
        {
            "unreachable moves are not proven idle",
            [
                FlowAction(EditorActionType.RepeatBlockStart, "2"),
                FlowAction(EditorActionType.Break),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                FlowAction(EditorActionType.BlockEnd),
            ]
        },
        {
            "raw break bypasses all releases",
            [
                FlowAction(EditorActionType.RepeatBlockStart, "2"),
                ButtonAction(EditorActionType.MouseDown),
                FlowAction(EditorActionType.RawScriptStep, "  BrEaK  "),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Left),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Right),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Middle),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side1),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side2),
                FlowAction(EditorActionType.BlockEnd),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Left),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Right),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Middle),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side1),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side2),
            ]
        },
        {
            "raw continue bypasses all releases",
            [
                FlowAction(EditorActionType.RepeatBlockStart, "2"),
                AbsoluteMove(100, 100), FixedDelay(5_000), AbsoluteMove(200, 200),
                ButtonAction(EditorActionType.MouseDown),
                FlowAction(EditorActionType.RawScriptStep, "  CoNtInUe  "),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Left),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Right),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Middle),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side1),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side2),
                FlowAction(EditorActionType.BlockEnd),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Left),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Right),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Middle),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side1),
                ButtonAction(EditorActionType.MouseUp, MacroMouseButton.Side2),
            ]
        },
    };

    [Theory]
    [MemberData(nameof(UncertainDragPaths))]
    public void MovementCleanup_PreservesPossibleDragPathsAndStillCleansProvenIdleSuffix(
        string scenario, EditorAction[] protectedActions)
    {
        ArgumentNullException.ThrowIfNull(protectedActions);
        var idleFirst = AbsoluteMove(300, 300);
        var idleWait = FixedDelay(5_000);
        var idleFinal = AbsoluteMove(400, 400);
        foreach (var action in protectedActions.Concat([idleFirst, idleWait, idleFinal]))
        {
            _viewModel.Actions.Add(action);
        }

        _viewModel.SimplifyMovement = true;
        _viewModel.ActionListItems.Select(row => row.Action).Should()
            .Equal(protectedActions.Concat([idleFinal]), scenario);
        _viewModel.Actions.Should().Equal(protectedActions.Concat([idleFirst, idleWait, idleFinal]));

        _viewModel.ApplyMovementCondensation();
        _viewModel.Actions.Should().Equal(protectedActions.Concat([idleFinal]), scenario);
        _viewModel.Undo();
        _viewModel.Actions.Select(action => (action.Type, action.X, action.Y, action.DelayMicroseconds, action.Button, action.Text))
            .Should().Equal(protectedActions.Concat([idleFirst, idleWait, idleFinal])
                .Select(action => (action.Type, action.X, action.Y, action.DelayMicroseconds, action.Button, action.Text)));

        var restoredProtected = _viewModel.Actions.Take(protectedActions.Length).ToArray();
        _viewModel.HideMouseMoves = true;
        _viewModel.HideShortWaits = true;
        _viewModel.ActionListItems.Select(row => row.Action).Should().Equal(restoredProtected, scenario);
        _viewModel.DeleteHiddenEvents();
        _viewModel.Actions.Should().Equal(restoredProtected, scenario);
        _viewModel.Undo();
        _viewModel.Actions.Select(action => action.Type).Should()
            .Equal(protectedActions.Select(action => action.Type).Concat(
                [EditorActionType.MouseMove, EditorActionType.Delay, EditorActionType.MouseMove]));
    }

    [Fact]
    public void MovementCleanup_CleansIdleSuffixWhenBothConditionalBranchesRelease()
    {
        EditorAction[] prefix =
        [
            ButtonAction(EditorActionType.MouseDown),
            FlowAction(EditorActionType.IfBlockStart, "1 == 0"),
            ButtonAction(EditorActionType.MouseUp),
            FlowAction(EditorActionType.BlockEnd),
            FlowAction(EditorActionType.ElseBlockStart),
            ButtonAction(EditorActionType.MouseUp),
            FlowAction(EditorActionType.BlockEnd),
        ];
        var final = AbsoluteMove(400, 400);
        foreach (var action in prefix.Concat([AbsoluteMove(300, 300), FixedDelay(5_000), final]))
        {
            _viewModel.Actions.Add(action);
        }

        _viewModel.SimplifyMovement = true;
        _viewModel.ActionListItems.Select(row => row.Action).Should().Equal(prefix.Concat([final]));
        _viewModel.ApplyMovementCondensation();
        _viewModel.Actions.Should().Equal(prefix.Concat([final]));
        _viewModel.HideMouseMoves = true;
        _viewModel.DeleteHiddenEvents();
        _viewModel.Actions.Should().Equal(prefix);
    }

    private static EditorAction ButtonAction(
        EditorActionType type, MacroMouseButton button = MacroMouseButton.Left) =>
        new() { Type = type, Button = button };

    private static EditorAction FlowAction(EditorActionType type, string text = "") =>
        new() { Type = type, Text = text };
}
