using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class MacroExecutionServiceTests
{
    private readonly IMacroFileManager _fileManager;
    private readonly IMacroPlayer _player;
    private readonly IMacroExecutionService _service;

    public MacroExecutionServiceTests()
    {
        _fileManager = Substitute.For<IMacroFileManager>();
        _player = Substitute.For<IMacroPlayer>();
        _service = new MacroExecutionService(_fileManager, () => _player);
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
}
