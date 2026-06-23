using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using CrossMacro.Platform.Abstractions;
using NSubstitute;
using System.Text.Json;

namespace CrossMacro.Cli.Tests;

public class MacroExecutionServiceTests
{
    private readonly IMacroFileManager _fileManager;
    private readonly IMacroPlayer _player;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly IMacroExecutionService _service;

    public MacroExecutionServiceTests()
    {
        _fileManager = Substitute.For<IMacroFileManager>();
        _player = Substitute.For<IMacroPlayer>();
        _keyCodeMapper = CreateKeyCodeMapper();
        _service = new MacroExecutionService(_fileManager, () => _player, _keyCodeMapper);
    }

    [Fact]
    public async Task ValidateAsync_WhenFileMissing_ReturnsFileError()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".macro");

        var result = await _service.ValidateAsync(path, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.FileError, result.ExitCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenMacroValid_ReturnsSuccess()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _fileManager.LoadAsync(tempFile).Returns(CreateValidMacro());

            var result = await _service.ValidateAsync(tempFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(CliExitCode.Success, result.ExitCode);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidateAsync_WhenScriptUsesRuntimeMappedKey_ReturnsSuccess()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _fileManager.LoadAsync(tempFile).Returns(new MacroSequence
            {
                Name = "script",
                ScriptSteps = ["pixelcolor 1 2 sampled", "tap Backspace"]
            });

            var result = await _service.ValidateAsync(tempFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(CliExitCode.Success, result.ExitCode);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetInfoAsync_WhenMacroValid_ReturnsSuccessWithData()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _fileManager.LoadAsync(tempFile).Returns(CreateValidMacro());

            var result = await _service.GetInfoAsync(tempFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(CliExitCode.Success, result.ExitCode);
            Assert.NotNull(result.Data);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetInfoAsync_WhenMacroHasMixedCoordinateModes_ReportsMixedCoordinateMode()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _fileManager.LoadAsync(tempFile).Returns(CreateMixedMacro());

            var result = await _service.GetInfoAsync(tempFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            var payload = JsonSerializer.SerializeToElement(result.Data);
            Assert.Equal("mixed", payload.GetProperty("coordinateMode").GetString());
            Assert.False(payload.GetProperty("isAbsoluteCoordinates").GetBoolean());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_DoesNotInvokePlayer()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _fileManager.LoadAsync(tempFile).Returns(CreateValidMacro());

            var result = await _service.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = tempFile,
                DryRun = true
            }, CancellationToken.None);

            Assert.True(result.Success);
            await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotDryRun_InvokesPlayer()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var macro = CreateValidMacro();
            _fileManager.LoadAsync(tempFile).Returns(macro);

            var result = await _service.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = tempFile,
                SpeedMultiplier = 2.0,
                Loop = true,
                RepeatCount = 3,
                RepeatDelayMs = 100,
                DryRun = false
            }, CancellationToken.None);

            Assert.True(result.Success);
            await _player.Received(1).PlayAsync(
                macro,
                Arg.Is<PlaybackOptions>(x => x.SpeedMultiplier == 2.0 && x.Loop && x.RepeatCount == 3 && x.RepeatDelayMs == 100),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsolutePlaybackUnsupported_ReturnsDedicatedMessage()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var macro = CreateMixedMacro();
            _fileManager.LoadAsync(tempFile).Returns(macro);
            _player.PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns<Task>(_ => throw new AbsolutePlaybackUnsupportedException("Linux UInput"));

            var result = await _service.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = tempFile,
                DryRun = false
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(CliExitCode.RuntimeError, result.ExitCode);
            Assert.Equal("Absolute coordinate playback is not supported in this session.", result.Message);
            Assert.Contains("active backend cannot play absolute coordinates", result.Errors.Single());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static MacroSequence CreateValidMacro()
    {
        var macro = new MacroSequence
        {
            Name = "test"
        };
        macro.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 1,
            Y = 1,
            DelayMs = 0,
            Timestamp = 0
        });
        return macro;
    }

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        keyCodeMapper.GetKeyCode("Backspace").Returns(InputEventCode.KEY_BACKSPACE);
        keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(false);
        return keyCodeMapper;
    }

    private static MacroSequence CreateMixedMacro()
    {
        var macro = new MacroSequence
        {
            Name = "mixed"
        };

        macro.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 1,
            Y = 1,
            CoordinateMode = MouseCoordinateMode.Absolute,
            DelayMs = 0,
            Timestamp = 0
        });
        macro.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 2,
            Y = 2,
            CoordinateMode = MouseCoordinateMode.Relative,
            DelayMs = 0,
            Timestamp = 1
        });

        return macro;
    }
}
