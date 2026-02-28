using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Cli.Tests;

public class SettingsCommandHandlersTests
{
    [Fact]
    public async Task SettingsGetHandler_WhenServiceSucceeds_ReturnsSuccess()
    {
        var service = Substitute.For<ISettingsCliService>();
        service.GetAsync("playback.speed", Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "playback.speed=1"
            });

        var handler = new SettingsGetCommandHandler(service);
        var result = await handler.ExecuteAsync(new SettingsGetCliOptions("playback.speed", true), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SettingsSetHandler_WhenServiceFails_ReturnsFailure()
    {
        var service = Substitute.For<ISettingsCliService>();
        service.SetAsync("playback.loopCount", "-1", Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Invalid settings value."
            });

        var handler = new SettingsSetCommandHandler(service);
        var result = await handler.ExecuteAsync(new SettingsSetCliOptions("playback.loopCount", "-1", true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenGetAllTextMode_FormatsAsKeyValueLines()
    {
        var service = Substitute.For<ISettingsCliService>();
        service.GetAsync(null, Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Settings loaded.",
                Data = new Dictionary<string, object?>
                {
                    ["playback.speed"] = 1.5,
                    ["playback.loop"] = true
                }
            });

        var handler = new SettingsGetCommandHandler(service);
        var result = await handler.ExecuteAsync(new SettingsGetCliOptions(null, JsonOutput: false), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("playback.speed=1.5", result.Message);
        Assert.Contains("playback.loop=True", result.Message);
    }
}
