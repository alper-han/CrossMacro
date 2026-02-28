using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;
using System.IO;

namespace CrossMacro.Cli.Tests;

public class RunScriptExecutionServiceTests
{
    private readonly IMacroPlayer _player;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly RunScriptExecutionService _service;

    public RunScriptExecutionServiceTests()
    {
        _player = Substitute.For<IMacroPlayer>();
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(false);

        _service = new RunScriptExecutionService(() => _player, _keyCodeMapper);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRunWithBasicSteps_ReturnsSuccess()
    {
        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps =
            [
                "move abs 100 120",
                "delay 50",
                "click left"
            ],
            DryRun = true
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
        Assert.Equal("Run script parsed successfully (dry-run).", result.Message);
        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTapCombo_CompilesAndPlays()
    {
        _keyCodeMapper.GetKeyCode("ctrl").Returns(29);
        _keyCodeMapper.GetKeyCode("c").Returns(46);
        _keyCodeMapper.IsModifierKeyCode(29).Returns(true);

        MacroSequence? captured = null;
        _player.PlayAsync(Arg.Do<MacroSequence>(m => captured = m), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["tap ctrl+c"]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(4, captured!.Events.Count);
        Assert.Equal(EventType.KeyPress, captured.Events[0].Type);
        Assert.Equal(29, captured.Events[0].KeyCode);
        Assert.Equal(EventType.KeyPress, captured.Events[1].Type);
        Assert.Equal(46, captured.Events[1].KeyCode);
        Assert.Equal(EventType.KeyRelease, captured.Events[2].Type);
        Assert.Equal(46, captured.Events[2].KeyCode);
        Assert.Equal(EventType.KeyRelease, captured.Events[3].Type);
        Assert.Equal(29, captured.Events[3].KeyCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMixingAbsAndRelMove_ReturnsInvalidArguments()
    {
        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps =
            [
                "move abs 100 100",
                "move rel 10 10"
            ]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("cannot mix absolute and relative move modes", result.Errors[0]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThirtyStepsDryRun_ReturnsSuccess()
    {
        var steps = Enumerable.Repeat("click left", 30).ToArray();

        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps = steps,
            DryRun = true
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDelayBetweenEvents_CompilesDeterministicDelay()
    {
        MacroSequence? captured = null;
        _player.PlayAsync(Arg.Do<MacroSequence>(m => captured = m), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps =
            [
                "click left",
                "delay 75",
                "click left"
            ]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Events.Count);
        Assert.Equal(0, captured.Events[0].DelayMs);
        Assert.Equal(75, captured.Events[1].DelayMs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTypeStep_CompilesCharacterEvents()
    {
        _keyCodeMapper.GetKeyCodeForCharacter('a').Returns(30);
        _keyCodeMapper.RequiresShift('a').Returns(false);
        _keyCodeMapper.RequiresAltGr('a').Returns(false);

        MacroSequence? captured = null;
        _player.PlayAsync(Arg.Do<MacroSequence>(m => captured = m), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["type a"]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Events.Count);
        Assert.Equal(EventType.KeyPress, captured.Events[0].Type);
        Assert.Equal(30, captured.Events[0].KeyCode);
        Assert.Equal(EventType.KeyRelease, captured.Events[1].Type);
        Assert.Equal(30, captured.Events[1].KeyCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTypeStepHasUnmappableCharacter_ReturnsInvalidArguments()
    {
        _keyCodeMapper.GetKeyCodeForCharacter('x').Returns(-1);

        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["type x"]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("cannot map character", result.Errors[0]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepsFileProvided_LoadsAndExecutesSteps()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile,
            [
                "# comment",
                "",
                "click left",
                "delay 20",
                "click left"
            ]);

            MacroSequence? captured = null;
            _player.PlayAsync(Arg.Do<MacroSequence>(m => captured = m), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var result = await _service.ExecuteAsync(new RunExecutionRequest
            {
                StepFilePath = tempFile
            }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(captured);
            Assert.Equal(2, captured!.Events.Count);
            Assert.Equal(20, captured.Events[1].DelayMs);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepsFileHasInvalidLine_ReturnsLineNumberInError()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile,
            [
                "click left",
                "bad command"
            ]);

            var result = await _service.ExecuteAsync(new RunExecutionRequest
            {
                StepFilePath = tempFile
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
            Assert.Contains("line 2", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenSetVariableUsedInMove_ResolvesValue()
    {
        MacroSequence? captured = null;
        _player.PlayAsync(Arg.Do<MacroSequence>(m => captured = m), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps =
            [
                "set x=123",
                "move abs $x 200"
            ]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Single(captured!.Events);
        Assert.Equal(EventType.MouseMove, captured.Events[0].Type);
        Assert.Equal(123, captured.Events[0].X);
        Assert.Equal(200, captured.Events[0].Y);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnknownVariableUsed_ReturnsInvalidArguments()
    {
        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps = ["move abs $missing 10"]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("Unknown variable '$missing'", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepeatBlockUsed_ExpandsSteps()
    {
        MacroSequence? captured = null;
        _player.PlayAsync(Arg.Do<MacroSequence>(m => captured = m), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps =
            [
                "set n 3",
                "repeat $n {",
                "click left",
                "}"
            ]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(3, captured!.Events.Count);
        Assert.All(captured.Events, ev => Assert.Equal(EventType.Click, ev.Type));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepeatBlockMissingClosingBrace_ReturnsInvalidArguments()
    {
        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps =
            [
                "repeat 2 {",
                "click left"
            ]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("Missing closing brace", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRandomDelayUsed_DelayFallsInRange()
    {
        MacroSequence? captured = null;
        _player.PlayAsync(Arg.Do<MacroSequence>(m => captured = m), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _service.ExecuteAsync(new RunExecutionRequest
        {
            Steps =
            [
                "click left",
                "delay random 10 20",
                "click left"
            ]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Events.Count);
        Assert.InRange(captured.Events[1].DelayMs, 10, 20);
    }
}
