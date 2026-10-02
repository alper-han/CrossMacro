namespace CrossMacro.UI.Tests.ViewModels;

public sealed class MouseMovementCondensationPlanTests
{
    [Fact]
    public void Create_CondensesSafeAbsoluteMovesAndOnlyTheirInternalWaits()
    {
        var actions = new[]
        {
            Move(100, 100),
            Delay(4_000),
            Move(110, 110),
            Delay(3_000),
            Move(120, 120),
            Delay(40_000),
            new EditorAction { Type = EditorActionType.KeyDown, KeyCode = 30 },
        };

        var plan = MouseMovementCondensationPlan.Create(actions, thresholdMicroseconds: 10_000);

        var run = plan.Runs.Should().ContainSingle().Which;
        run.StartIndex.Should().Be(0);
        run.EndIndex.Should().Be(4);
        run.FinalMoveIndex.Should().Be(4);
        run.RemovedMoveCount.Should().Be(2);
        run.RemovedDelayMicroseconds.Should().Be(7_000);
        run.RemovedDelayCount.Should().Be(2);
        plan.RemovedActionIndices.Should().Equal(0, 1, 2, 3);
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(12_000)]
    public void Create_PreservesGapAtOrAboveThreshold(long thresholdMicroseconds)
    {
        var actions = new[]
        {
            Move(100, 100),
            Delay(6_000),
            Delay(6_000),
            Move(120, 120),
        };

        var plan = MouseMovementCondensationPlan.Create(actions, thresholdMicroseconds);

        plan.Runs.Should().BeEmpty();
        plan.RemovedActionIndices.Should().BeEmpty();
    }

    [Fact]
    public void Create_PreservesLeadingAndTrailingWaits()
    {
        var actions = new[]
        {
            Delay(2_000),
            Move(100, 100),
            Delay(5_000),
            Move(120, 120),
            Delay(9_000),
        };

        var plan = MouseMovementCondensationPlan.Create(actions, thresholdMicroseconds: 10_000);

        plan.RemovedActionIndices.Should().Equal(1, 2);
        plan.Runs.Should().ContainSingle().Which.RemovedDelayMicroseconds.Should().Be(5_000);
    }

    [Fact]
    public void Create_ZeroThresholdDisablesCondensationEvenForAdjacentMoves()
    {
        var plan = MouseMovementCondensationPlan.Create(
            [Move(100, 100), Move(120, 120)],
            thresholdMicroseconds: 0);

        plan.Runs.Should().BeEmpty();
    }

    [Fact]
    public void Create_RequiresTwoMovesAndDoesNotCondenseWaitOnlyRuns()
    {
        var actions = new[]
        {
            Delay(1_000), Delay(1_000), Delay(1_000), Delay(1_000), Delay(1_000), Delay(1_000),
            Move(120, 120),
        };

        var plan = MouseMovementCondensationPlan.Create(actions, thresholdMicroseconds: 10_000);

        plan.Runs.Should().BeEmpty();
    }

    [Fact]
    public void Create_DoesNotCondenseMovementWhileAnyMouseButtonIsHeld()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Left },
            Move(100, 100),
            Delay(2_000),
            Move(110, 110),
            new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Right },
            new EditorAction { Type = EditorActionType.MouseUp, Button = MacroMouseButton.Right },
            Move(120, 120),
            Delay(2_000),
            Move(130, 130),
            new EditorAction { Type = EditorActionType.MouseUp, Button = MacroMouseButton.Left },
        };

        var plan = MouseMovementCondensationPlan.Create(actions, thresholdMicroseconds: 10_000);

        plan.Runs.Should().BeEmpty();
        plan.RemovedActionIndices.Should().BeEmpty();
    }

    [Fact]
    public void Create_DoesNotCondenseRelativeOrVariableCoordinateMoves()
    {
        var relativeMoves = new[]
        {
            Move(10, 10, absolute: false), Delay(1_000), Move(20, 20, absolute: false),
        };
        var variableMoves = new[]
        {
            Move(100, 100),
            Delay(1_000),
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                CoordinateXToken = "$targetX",
                CoordinateYToken = "$targetY",
            },
        };

        MouseMovementCondensationPlan.Create(relativeMoves, 10_000).Runs.Should().BeEmpty();
        MouseMovementCondensationPlan.Create(variableMoves, 10_000).Runs.Should().BeEmpty();
    }

    [Fact]
    public void Create_TreatsRandomWaitAsABoundary()
    {
        var actions = new[]
        {
            Move(100, 100),
            new EditorAction
            {
                Type = EditorActionType.Delay,
                UseRandomDelay = true,
                RandomDelayMinMs = 1,
                RandomDelayMaxMs = 2,
            },
            Move(120, 120),
        };

        var plan = MouseMovementCondensationPlan.Create(actions, thresholdMicroseconds: 10_000);

        plan.Runs.Should().BeEmpty();
    }
    [Fact]
    public void Create_DoesNotCondenseAcrossClicksOrCoordinateModeChanges()
    {
        var actionsAroundClick = new[]
        {
            Move(100, 100), Delay(1_000),
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left },
            Delay(1_000), Move(120, 120),
        };
        var mixedCoordinateModes = new[]
        {
            Move(100, 100), Delay(1_000), Move(10, 10, absolute: false),
            Delay(1_000), Move(120, 120),
        };

        MouseMovementCondensationPlan.Create(actionsAroundClick, 10_000).Runs.Should().BeEmpty();
        MouseMovementCondensationPlan.Create(mixedCoordinateModes, 10_000).Runs.Should().BeEmpty();
    }

    private static EditorAction Move(int x, int y, bool absolute = true) => new()
    {
        Type = EditorActionType.MouseMove,
        X = x,
        Y = y,
        IsAbsolute = absolute,
    };

    private static EditorAction Delay(long microseconds) => new()
    {
        Type = EditorActionType.Delay,
        DelayMicroseconds = microseconds,
    };
}
