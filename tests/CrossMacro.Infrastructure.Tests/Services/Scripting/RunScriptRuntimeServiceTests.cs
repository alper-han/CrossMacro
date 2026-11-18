
using CrossMacro.Application.Execution;

namespace CrossMacro.Infrastructure.Tests.Services.Scripting;

public sealed class RunScriptRuntimeServiceTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("crossmacro-run-script-");
    private readonly IMacroPlayer _player;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly RunScriptExecutionWorkflow _service;
    private MacroSequence? Played { get; set; }

    public RunScriptRuntimeServiceTests()
    {
        _player = Substitute.For<IMacroPlayer>();
        _ = _player.PlayAsync(Arg.Do<MacroSequence>(macro => Played = macro), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = _keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = _keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);

        _service = new RunScriptExecutionWorkflow(new RunScriptRuntimeService(() => _player, _keyCodeMapper));
    }

    private MacroSequence PlayedMacro => Assert.IsType<MacroSequence>(Played);

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public async Task ExecuteAsync_WhenDryRunWithBasicSteps_ReturnsSuccess()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "move abs 100 120",
                "delay 50",
                "click left",
            ],
            DryRun = true,
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
        Assert.Equal("Run script parsed successfully (dry-run).", result.Message);
        var payload = Assert.IsType<RunScriptExecutionData>(result.Data);
        Assert.Equal("absolute", payload.CoordinateMode);
        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenScreenReadingScriptUsesRuntimeMappedKey_ReturnsSuccess()
    {
        _ = _keyCodeMapper.GetKeyCode("Backspace").Returns(InputEventCode.KEY_BACKSPACE);

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["pixelcolor 1 2 sampled", "tap Backspace"],
            DryRun = true,
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRunHasMixedCoordinateModes_ReportsMixedCoordinateMode()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "move abs 100 200",
                "move rel 10 -5",
            ],
            DryRun = true,
        }, CancellationToken.None);

        Assert.True(result.Success);
        var payload = Assert.IsType<RunScriptExecutionData>(result.Data);
        Assert.Equal("mixed", payload.CoordinateMode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetAndConditionUseEscapedDollarLiteral_TreatsAsText()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set mode=$$foo",
                "if $mode == $$foo {",
                "click left",
                "}",
            ],
            DryRun = true,
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsoluteMoveThenClick_ClickUsesLastAbsoluteCoordinates()
    {
        // Act
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "move abs 100 120",
                "click left",
            ],
        }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, PlayedMacro.Events.Count);
        Assert.True(PlayedMacro.IsAbsoluteCoordinates);
        var payload = Assert.IsType<RunScriptExecutionData>(result.Data);
        Assert.Equal("absolute", payload.CoordinateMode);
        Assert.Equal(EventType.Click, PlayedMacro.Events[1].Type);
        Assert.Equal(100, PlayedMacro.Events[1].X);
        Assert.Equal(120, PlayedMacro.Events[1].Y);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsoluteMoveThenCurrentClick_PreservesCurrentPositionSemantics()
    {
        // Act
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "move abs 100 120",
                "click current left",
            ],
        }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, PlayedMacro.Events.Count);
        Assert.True(PlayedMacro.IsAbsoluteCoordinates);
        Assert.Equal(EventType.Click, PlayedMacro.Events[1].Type);
        Assert.True(PlayedMacro.Events[1].UseCurrentPosition);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClickHasNoPositioningStep_UsesCurrentPositionSemantics()
    {
        // Act
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["click left"],
        }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        _ = Assert.Single(PlayedMacro.Events);
        Assert.True(PlayedMacro.Events[0].UseCurrentPosition);
        Assert.True(PlayedMacro.SkipInitialZeroZero);
        Assert.False(PlayedMacro.IsAbsoluteCoordinates);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTapCombo_CompilesAndPlays()
    {
        _ = _keyCodeMapper.GetKeyCode("ctrl").Returns(29);
        _ = _keyCodeMapper.GetKeyCode("c").Returns(46);
        _ = _keyCodeMapper.IsModifierKeyCode(29).Returns(returnThis: true);

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["tap ctrl+c"],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(4, PlayedMacro.Events.Count);
        Assert.Equal(EventType.KeyPress, PlayedMacro.Events[0].Type);
        Assert.Equal(29, PlayedMacro.Events[0].KeyCode);
        Assert.Equal(EventType.KeyPress, PlayedMacro.Events[1].Type);
        Assert.Equal(46, PlayedMacro.Events[1].KeyCode);
        Assert.Equal(EventType.KeyRelease, PlayedMacro.Events[2].Type);
        Assert.Equal(46, PlayedMacro.Events[2].KeyCode);
        Assert.Equal(EventType.KeyRelease, PlayedMacro.Events[3].Type);
        Assert.Equal(29, PlayedMacro.Events[3].KeyCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMixingAbsAndRelMove_CompilesPerEventCoordinateModes()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "move abs 100 100",
                "move rel 10 10",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
        Assert.False(PlayedMacro.IsAbsoluteCoordinates);
        var payload = Assert.IsType<RunScriptExecutionData>(result.Data);
        Assert.Equal("mixed", payload.CoordinateMode);
        Assert.Equal(MouseCoordinateMode.Absolute, PlayedMacro.Events[0].CoordinateMode);
        Assert.Equal(MouseCoordinateMode.Relative, PlayedMacro.Events[1].CoordinateMode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsoluteModeIntroducedAfterButtonStep_CompilesMixedCurrentAndAbsoluteEvents()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "click left",
                "move abs 100 100",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
        Assert.Equal(2, PlayedMacro.Events.Count);
        Assert.True(PlayedMacro.Events[0].UseCurrentPosition);
        Assert.Null(PlayedMacro.Events[0].CoordinateMode);
        Assert.Equal(MouseCoordinateMode.Absolute, PlayedMacro.Events[1].CoordinateMode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThirtyStepsDryRun_ReturnsSuccess()
    {
        var steps = Enumerable.Repeat("click left", 30).ToArray();

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = steps,
            DryRun = true,
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDelayBetweenEvents_CompilesDeterministicDelay()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "click left",
                "delay 75",
                "click left",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, PlayedMacro.Events.Count);
        Assert.Equal(0, PlayedMacro.Events[0].DelayMs);
        Assert.Equal(75, PlayedMacro.Events[1].DelayMs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLeadingDelayAndSpeedProvided_ScalesInitialDelayBeforePlayback()
    {
        var observedDelays = new List<TimeSpan>();
        var service = new RunScriptExecutionWorkflow(new RunScriptRuntimeService(
            () => _player,
            _keyCodeMapper,
            delayAsync: (duration, _) =>
            {
                observedDelays.Add(duration);
                return Task.CompletedTask;
            }));

        var result = await service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "delay 200",
                "click left",
            ],
            SpeedMultiplier = 4.0,
        }, CancellationToken.None);

        Assert.True(result.Success);
        _ = Assert.Single(observedDelays);
        Assert.Equal(50, observedDelays[0].TotalMilliseconds);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTypeStep_CompilesCharacterEvents()
    {
        _ = _keyCodeMapper.GetKeyCodeForCharacter('a').Returns(30);
        _ = _keyCodeMapper.RequiresShift('a').Returns(returnThis: false);
        _ = _keyCodeMapper.RequiresAltGr('a').Returns(returnThis: false);

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["type a"],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, PlayedMacro.Events.Count);
        Assert.Equal(EventType.KeyPress, PlayedMacro.Events[0].Type);
        Assert.Equal(30, PlayedMacro.Events[0].KeyCode);
        Assert.Equal(EventType.KeyRelease, PlayedMacro.Events[1].Type);
        Assert.Equal(30, PlayedMacro.Events[1].KeyCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTypeStepHasTrailingSpace_PreservesTrailingWhitespace()
    {
        _ = _keyCodeMapper.GetKeyCodeForCharacter('c').Returns(46);
        _ = _keyCodeMapper.GetKeyCodeForCharacter('m').Returns(50);
        _ = _keyCodeMapper.GetKeyCodeForCharacter('d').Returns(32);
        _ = _keyCodeMapper.GetKeyCodeForCharacter(' ').Returns(57);
        _ = _keyCodeMapper.RequiresShift(Arg.Any<char>()).Returns(returnThis: false);
        _ = _keyCodeMapper.RequiresAltGr(Arg.Any<char>()).Returns(returnThis: false);

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["type cmd "],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(8, PlayedMacro.Events.Count);
        Assert.Equal(46, PlayedMacro.Events[0].KeyCode);
        Assert.Equal(50, PlayedMacro.Events[2].KeyCode);
        Assert.Equal(32, PlayedMacro.Events[4].KeyCode);
        Assert.Equal(57, PlayedMacro.Events[6].KeyCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTypeStepHasUnmappableCharacter_ReturnsInvalidArguments()
    {
        _ = _keyCodeMapper.GetKeyCodeForCharacter('x').Returns(-1);

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["type x"],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.ExitCode);
        Assert.Contains("cannot map character", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepsFileProvided_LoadsAndExecutesSteps()
    {
        var tempFile = Path.Combine(_directory.FullName, "steps.txt");
        await File.WriteAllLinesAsync(tempFile,
        [
            "# comment",
            "",
            "click left",
            "delay 20",
            "click left",
        ], CancellationToken.None);

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            StepFilePath = tempFile,
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, PlayedMacro.Events.Count);
        Assert.Equal(20, PlayedMacro.Events[1].DelayMs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepsFileHasInvalidLine_ReturnsLineNumberInError()
    {
        var tempFile = Path.Combine(_directory.FullName, "steps.txt");
        await File.WriteAllLinesAsync(tempFile,
        [
            "click left",
            "bad command",
        ], CancellationToken.None);

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            StepFilePath = tempFile,
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.ExitCode);
        Assert.Contains("line 2", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetVariableUsedInMove_ResolvesValue()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set x=123",
                "move abs $x 200",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        _ = Assert.Single(PlayedMacro.Events);
        Assert.Equal(EventType.MouseMove, PlayedMacro.Events[0].Type);
        Assert.Equal(123, PlayedMacro.Events[0].X);
        Assert.Equal(200, PlayedMacro.Events[0].Y);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnknownVariableUsed_ReturnsInvalidArguments()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["move abs $missing 10"],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.ExitCode);
        Assert.Contains("Unknown variable '$missing'", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepeatBlockUsed_ExpandsSteps()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set n 3",
                "repeat $n {",
                "click left",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, PlayedMacro.Events.Count);
        Assert.All(PlayedMacro.Events, ev => Assert.Equal(EventType.Click, ev.Type));
    }

    [Fact]
    public async Task ExecuteAsync_WhenBreakUsedInsideLoop_StopsNearestLoop()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "repeat 5 {",
                "click left",
                "break",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        _ = Assert.Single(PlayedMacro.Events);
        Assert.Equal(EventType.Click, PlayedMacro.Events[0].Type);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContinueUsedInsideLoop_SkipsRemainingBodySteps()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "for i from 1 to 3 {",
                "click left",
                "continue",
                "click right",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, PlayedMacro.Events.Count);
        Assert.All(PlayedMacro.Events, ev =>
        {
            Assert.Equal(EventType.Click, ev.Type);
            Assert.Equal(MacroMouseButton.Left, ev.Button);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WhenBreakUsedOutsideLoop_ReturnsInvalidArguments()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "break",
                "click left",
            ],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.ExitCode);
        Assert.Contains("break", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inside repeat/while/for blocks", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepeatBlockMissingClosingBrace_ReturnsInvalidArguments()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "repeat 2 {",
                "click left",
            ],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.ExitCode);
        Assert.Contains("Missing closing brace", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRandomDelayUsed_PreservesRandomDelayMetadata()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "click left",
                "delay random 10 20",
                "click left",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, PlayedMacro.Events.Count);
        Assert.Equal(0, PlayedMacro.Events[1].DelayMs);
        Assert.True(PlayedMacro.Events[1].HasRandomDelay);
        Assert.Equal(10, PlayedMacro.Events[1].RandomDelayMinMs);
        Assert.Equal(20, PlayedMacro.Events[1].RandomDelayMaxMs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfElseUsed_ExpandsMatchingBranch()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set mode=fast",
                "if $mode == fast {",
                "click left",
                "}",
                "else {",
                "click right",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        _ = Assert.Single(PlayedMacro.Events);
        Assert.Equal(EventType.Click, PlayedMacro.Events[0].Type);
        Assert.Equal(MacroMouseButton.Left, PlayedMacro.Events[0].Button);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEqualityConditionRightOperandContainsComparator_UsesEqualityBoundary()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set mode=a>=b",
                "if $mode == a>=b {",
                "click left",
                "}",
                "else {",
                "click right",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        _ = Assert.Single(PlayedMacro.Events);
        Assert.Equal(EventType.Click, PlayedMacro.Events[0].Type);
        Assert.Equal(MacroMouseButton.Left, PlayedMacro.Events[0].Button);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWhileWithIncUsed_ExpandsLoop()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set i=0",
                "while $i < 3 {",
                "click left",
                "inc i",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, PlayedMacro.Events.Count);
        Assert.All(PlayedMacro.Events, ev => Assert.Equal(EventType.Click, ev.Type));
    }

    [Fact]
    public async Task ExecuteAsync_WhenForLoopUsed_ExpandsRange()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "for i from 1 to 5 step 2 {",
                "click left",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, PlayedMacro.Events.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenForLoopUsesVariableBoundsAndStep_ExpandsRange()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set start=1",
                "set finish=5",
                "set step=2",
                "for i from $start to $finish step $step {",
                "click left",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, PlayedMacro.Events.Count);
        Assert.All(PlayedMacro.Events, ev => Assert.Equal(EventType.Click, ev.Type));
    }

    [Fact]
    public async Task ExecuteAsync_WhenForLoopUsesSameVariableForEndAndStep_ExpandsRange()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set limit=3",
                "for i from 0 to $limit step $limit {",
                "click left",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, PlayedMacro.Events.Count);
        Assert.All(PlayedMacro.Events, ev => Assert.Equal(EventType.Click, ev.Type));
    }

    [Fact]
    public async Task ExecuteAsync_WhenWhileUsesVariableIncrementAmount_ExpandsLoop()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set i=0",
                "set step=2",
                "while $i < 5 {",
                "click left",
                "inc i $step",
                "}",
            ],
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, PlayedMacro.Events.Count);
        Assert.All(PlayedMacro.Events, ev => Assert.Equal(EventType.Click, ev.Type));
    }

    [Fact]
    public async Task ExecuteAsync_WhenWhileNeverProgresses_ReturnsIterationLimitError()
    {
        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps =
            [
                "set i=0",
                "while $i < 3 {",
                "click left",
                "}",
            ],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.ExitCode);
        Assert.Contains("loop iteration limit exceeded", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsolutePlaybackUnsupported_ReturnsDedicatedMessage()
    {
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AbsolutePlaybackUnsupportedException("Relative Only")));

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["move abs 10 10"],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.RuntimeError, result.ExitCode);
        Assert.Equal("Absolute coordinate playback is not supported in this session.", result.Message);
        Assert.Contains(result.Errors, error => error.Contains("absolute mouse coordinates", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputInjectionPermissionRequired_ReturnsEnvironmentError()
    {
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InputInjectionPermissionRequiredException("permission missing")));

        var result = await _service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["type a"],
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.ExitCode);
        Assert.Equal("Playback permission is missing.", result.Message);
        Assert.Contains("permission missing", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlayerReportsRuntimeVariables_IncludesThemInData()
    {
        var player = new RuntimeVariablePlayer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sampled"] = "ABCDEF",
            ["found_x"] = "42",
        });
        var service = new RunScriptExecutionWorkflow(new RunScriptRuntimeService(() => player, _keyCodeMapper));

        var result = await service.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = ["click left"],
        }, CancellationToken.None);

        Assert.True(result.Success);
        var payload = Assert.IsType<RunScriptExecutionData>(result.Data);
        Assert.Equal("ABCDEF", payload.RuntimeVariables["sampled"]);
        Assert.Equal("42", payload.RuntimeVariables["found_x"]);
    }

    private sealed class RuntimeVariablePlayer(IReadOnlyDictionary<string, string> runtimeVariables) : IMacroPlayer, IRunScriptRuntimeVariableSource
    {
        public bool IsPlaying => false;
        public bool IsPaused => false;

        public int CurrentLoop => 0;

        public int TotalLoops => 0;

        public bool IsWaitingBetweenLoops => false;

        public event EventHandler? StateChanged
        {
            add { /* Empty */ }
            remove { /* Empty */ }
        }

        public IReadOnlyDictionary<string, string> RuntimeVariables { get; } = runtimeVariables;

        public Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void StopPlayback()
        {
        }

        public void Pause()
        {
        }

        public void ResumePlayback()
        {
        }

        public void Dispose()
        {
        }
    }
}
