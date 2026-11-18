
namespace CrossMacro.Cli.Tests;

public sealed class SettingsCommandHandlersTests
{
    public static TheoryData<CommandOptions> SettingsOptions => new()
    {
        new SettingsGetOptions("playback.speed", JsonOutput: true),
        new SettingsSetOptions("playback.speed", "1.25"),
        new SettingsListKeysOptions(),
        new SettingsResetOptions("playback.speed"),
    };

    [Theory]
    [MemberData(nameof(SettingsOptions))]
    public async Task ExecuteAsync_WhenServiceFails_ReturnsTheServiceExitCodeMessageAndErrors(CommandOptions options)
    {
        var service = ServiceReturning(new OperationResult
        {
            Success = false,
            Outcome = ExecutionOutcomeCode.InvalidArguments,
            Message = "Invalid settings value.",
            Errors = ["Value must be a positive integer."],
        });

        var result = await CreateHandler(options, service).ExecuteAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        Assert.Equal("Invalid settings value.", result.Message);
        Assert.Equal(["Value must be a positive integer."], result.Errors);
    }

    [Theory]
    [MemberData(nameof(SettingsOptions))]
    public async Task ExecuteAsync_WhenServiceSucceeds_ReturnsTheServiceMessageAndData(CommandOptions options)
    {
        var service = ServiceReturning(new OperationResult
        {
            Success = true,
            Outcome = ExecutionOutcomeCode.Success,
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
        var service = ServiceReturning(new OperationResult
        {
            Success = true,
            Outcome = ExecutionOutcomeCode.Success,
            Message = "Settings loaded.",
            Data = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["playback.speed"] = 1.5,
                ["playback.loop"] = true,
            },
        });

        var result = await new SettingsGetCommandHandler(service).ExecuteAsync(new SettingsGetOptions(Key: null, JsonOutput: false), CancellationToken.None);

        Assert.Equal("playback.loop=True\nplayback.speed=1.5", result.Message);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenGetAllInTextModeAndNothingIsStored_ReportsNoSettings()
    {
        var service = ServiceReturning(new OperationResult
        {
            Success = true,
            Outcome = ExecutionOutcomeCode.Success,
            Message = "Settings loaded.",
            Data = new Dictionary<string, object?>(StringComparer.Ordinal),
        });

        var result = await new SettingsGetCommandHandler(service).ExecuteAsync(new SettingsGetOptions(Key: null, JsonOutput: false), CancellationToken.None);

        Assert.Equal("No settings available.", result.Message);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenGetAllInJsonMode_KeepsTheServiceMessageAndData()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal) { ["playback.speed"] = 1.5 };
        var service = ServiceReturning(new OperationResult
        {
            Success = true,
            Outcome = ExecutionOutcomeCode.Success,
            Message = "Settings loaded.",
            Data = data,
        });

        var result = await new SettingsGetCommandHandler(service).ExecuteAsync(new SettingsGetOptions(Key: null, JsonOutput: true), CancellationToken.None);

        Assert.Equal("Settings loaded.", result.Message);
        Assert.Same(data, result.Data);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenAKeyIsRequested_PassesTheKeyToTheService()
    {
        var service = ServiceReturning(new OperationResult { Success = true, Outcome = ExecutionOutcomeCode.Success, Message = "playback.speed=1" });

        _ = await new SettingsGetCommandHandler(service).ExecuteAsync(new SettingsGetOptions("playback.speed"), CancellationToken.None);

        _ = await service.Received(1).GetAsync("playback.speed", CancellationToken.None);
    }

    private static ISettingsCommandService ServiceReturning(OperationResult result)
    {
        var service = Substitute.For<ISettingsCommandService>();
        _ = service.GetAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(result);
        _ = service.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        _ = service.ListKeysAsync(Arg.Any<CancellationToken>()).Returns(result);
        _ = service.ResetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        return service;
    }

    private static ICliCommandHandler CreateHandler(CommandOptions options, ISettingsCommandService service) => options switch
    {
        SettingsGetOptions => new SettingsGetCommandHandler(service),
        SettingsSetOptions => new SettingsSetCommandHandler(service),
        SettingsListKeysOptions => new SettingsListKeysCommandHandler(service),
        SettingsResetOptions => new SettingsResetCommandHandler(service),
        _ => throw new ArgumentOutOfRangeException(nameof(options)),
    };
}
