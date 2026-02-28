using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class RecordExecutionServiceTests
{
    private readonly IMacroRecorder _macroRecorder;
    private readonly IMacroFileManager _macroFileManager;
    private readonly IMousePositionProvider _mousePositionProvider;
    private readonly IRecordExecutionService _service;

    public RecordExecutionServiceTests()
    {
        _macroRecorder = Substitute.For<IMacroRecorder>();
        _macroFileManager = Substitute.For<IMacroFileManager>();
        _mousePositionProvider = Substitute.For<IMousePositionProvider>();
        _service = new RecordExecutionService(_macroRecorder, _macroFileManager, _mousePositionProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsoluteRequestedButUnsupported_FallsBackToRelativeWithWarning()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(CreateSequenceWithOneEvent());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        var result = await _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-abs-fallback.macro",
            CoordinateMode = RecordCoordinateMode.Absolute
        }, cts.Token);

        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
        Assert.Contains(result.Warnings, x => x.Contains("Falling back to relative mode.", StringComparison.Ordinal));

        await _macroRecorder.Received(1).StartRecordingAsync(
            true,
            true,
            Arg.Any<IEnumerable<int>>(),
            forceRelative: true,
            skipInitialZero: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAutoAndAbsoluteSupported_UsesAbsolute()
    {
        _mousePositionProvider.IsSupported.Returns(true);
        _mousePositionProvider.GetAbsolutePositionAsync().Returns((100, 200));
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(CreateSequenceWithOneEvent());

        var result = await _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-auto-abs.macro",
            CoordinateMode = RecordCoordinateMode.Auto,
            DurationSeconds = 0
        }, new CancellationTokenSource(10).Token);

        Assert.True(result.Success);
        await _macroRecorder.Received(1).StartRecordingAsync(
            true,
            true,
            Arg.Any<IEnumerable<int>>(),
            forceRelative: false,
            skipInitialZero: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEventsRecorded_ReturnsRuntimeError()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(new MacroSequence());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        var result = await _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-empty.macro"
        }, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains("No events were recorded", result.Message);
    }

    private static MacroSequence CreateSequenceWithOneEvent()
    {
        var sequence = new MacroSequence
        {
            Name = "recorded"
        };
        sequence.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 1,
            Y = 1,
            Timestamp = 0
        });
        sequence.CalculateDuration();
        return sequence;
    }
}
