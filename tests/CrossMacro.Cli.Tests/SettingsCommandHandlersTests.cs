
namespace CrossMacro.Cli.Tests;

public sealed class SettingsCommandHandlersTests
{
    public static TheoryData<CliCommandOptions> SettingsOptions => new()
    {
        new SettingsGetCliOptions("playback.speed", JsonOutput: true),
        new SettingsSetCliOptions("playback.speed", "1.25"),
        new SettingsListKeysCliOptions(),
        new SettingsResetCliOptions("playback.speed"),
    };

    [Theory]
    [MemberData(nameof(SettingsOptions))]
    public async Task ExecuteAsync_WhenServiceFails_ReturnsTheServiceExitCodeMessageAndErrors(CliCommandOptions options)
    {
        var service = ServiceReturning(new SettingsCommandResult
        {
            Success = false,
            ExitCode = CliExitCode.InvalidArguments,
            Message = "Invalid settings value.",
            Errors = ["Value must be a positive integer."],
        });

        var result = await CreateHandler(options, service).ExecuteAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("Invalid settings value.", result.Message);
        Assert.Equal(["Value must be a positive integer."], result.Errors);
    }

    [Theory]
    [MemberData(nameof(SettingsOptions))]
    public async Task ExecuteAsync_WhenServiceSucceeds_ReturnsTheServiceMessageAndData(CliCommandOptions options)
    {
        var service = ServiceReturning(new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = "Done.",
            Data = "1.25",
        });

        var result = await CreateHandler(options, service).ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Done.", result.Message);
        Assert.Equal("1.25", result.Data);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenGetAllInTextMode_FormatsSortedKeyValueLines()
    {
        var service = ServiceReturning(new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = "Settings loaded.",
            Data = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["playback.speed"] = 1.5,
                ["playback.loop"] = true,
            },
        });

        var result = await new SettingsGetCommandHandler(service).ExecuteAsync(new SettingsGetCliOptions(Key: null, JsonOutput: false), CancellationToken.None);

        Assert.Equal("playback.loop=True\nplayback.speed=1.5", result.Message);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenGetAllInTextModeAndNothingIsStored_ReportsNoSettings()
    {
        var service = ServiceReturning(new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = "Settings loaded.",
            Data = new Dictionary<string, object?>(StringComparer.Ordinal),
        });

        var result = await new SettingsGetCommandHandler(service).ExecuteAsync(new SettingsGetCliOptions(Key: null, JsonOutput: false), CancellationToken.None);

        Assert.Equal("No settings available.", result.Message);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenGetAllInJsonMode_KeepsTheServiceMessageAndData()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal) { ["playback.speed"] = 1.5 };
        var service = ServiceReturning(new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = "Settings loaded.",
            Data = data,
        });

        var result = await new SettingsGetCommandHandler(service).ExecuteAsync(new SettingsGetCliOptions(Key: null, JsonOutput: true), CancellationToken.None);

        Assert.Equal("Settings loaded.", result.Message);
        Assert.Same(data, result.Data);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenAKeyIsRequested_PassesTheKeyToTheService()
    {
        var service = ServiceReturning(new SettingsCommandResult { Success = true, ExitCode = CliExitCode.Success, Message = "playback.speed=1" });

        _ = await new SettingsGetCommandHandler(service).ExecuteAsync(new SettingsGetCliOptions("playback.speed"), CancellationToken.None);

        _ = await service.Received(1).GetAsync("playback.speed", CancellationToken.None);
    }

    private static ISettingsCliService ServiceReturning(SettingsCommandResult result)
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.GetAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(result);
        _ = service.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        _ = service.ListKeysAsync(Arg.Any<CancellationToken>()).Returns(result);
        _ = service.ResetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        return service;
    }

    private static ICliCommandHandler CreateHandler(CliCommandOptions options, ISettingsCliService service) => options switch
    {
        SettingsGetCliOptions => new SettingsGetCommandHandler(service),
        SettingsSetCliOptions => new SettingsSetCommandHandler(service),
        SettingsListKeysCliOptions => new SettingsListKeysCommandHandler(service),
        SettingsResetCliOptions => new SettingsResetCommandHandler(service),
        _ => throw new ArgumentOutOfRangeException(nameof(options)),
    };
}
