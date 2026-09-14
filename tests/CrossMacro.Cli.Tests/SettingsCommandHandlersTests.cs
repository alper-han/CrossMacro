
namespace CrossMacro.Cli.Tests;

public sealed class SettingsCommandHandlersTests
{
    [Fact]
    public void SettingsGetHandler_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new SettingsGetCommandHandler(settingsCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SettingsGetHandler_WhenServiceSucceeds_ReturnsSuccess()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.GetAsync("playback.speed", Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "playback.speed=1",
            });

        var handler = new SettingsGetCommandHandler(service);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var options = new SettingsGetCliOptions("playback.speed", JsonOutput: true);
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        _ = await service.Received(1).GetAsync("playback.speed", cancellationToken);
    }

    [Fact]
    public async Task SettingsSetHandler_WhenServiceFails_ReturnsFailure()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.SetAsync("playback.loopCount", "-1", Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Invalid settings value.",
                Errors = ["Value must be a positive integer."],
            });

        var handler = new SettingsSetCommandHandler(service);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(new SettingsSetCliOptions("playback.loopCount", "-1", JsonOutput: true), cancellationToken);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("Invalid settings value.", result.Message);
        Assert.Equal(["Value must be a positive integer."], result.Errors);
        _ = await service.Received(1).SetAsync("playback.loopCount", "-1", cancellationToken);
    }

    [Fact]
    public void SettingsSetHandler_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new SettingsSetCommandHandler(settingsCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SettingsSetHandler_WhenServiceSucceeds_PreservesResultAndData()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.SetAsync("playback.speed", "1.25", Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Setting updated.",
                Data = "1.25",
            });

        var handler = new SettingsSetCommandHandler(service);
        var result = await handler.ExecuteAsync(
            new SettingsSetCliOptions("playback.speed", "1.25"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Setting updated.", result.Message);
        Assert.Equal("1.25", result.Data);
        _ = await service.Received(1).SetAsync("playback.speed", "1.25", CancellationToken.None);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenGetAllTextMode_FormatsAsKeyValueLines()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.GetAsync(key: null, Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
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

        var handler = new SettingsGetCommandHandler(service);
        var result = await handler.ExecuteAsync(new SettingsGetCliOptions(Key: null, JsonOutput: false), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("playback.loop=True\nplayback.speed=1.5", result.Message);
    }

    [Fact]
    public async Task SettingsGetHandler_WhenGetFails_PreservesFailureAndErrors()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.GetAsync("unknown", Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = ["Key was not found."],
            });

        var handler = new SettingsGetCommandHandler(service);
        var result = await handler.ExecuteAsync(new SettingsGetCliOptions("unknown"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("Unknown settings key.", result.Message);
        Assert.Equal(["Key was not found."], result.Errors);
    }

    [Fact]
    public async Task SettingsListKeysHandler_WhenServiceSucceeds_ReturnsSuccess()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.ListKeysAsync(Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Supported settings keys loaded.",
                Data = new List<string> { "ui.theme" },
            });

        var handler = new SettingsListKeysCommandHandler(service);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(new SettingsListKeysCliOptions(JsonOutput: true), cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Supported settings keys loaded.", result.Message);
        var keys = Assert.IsType<List<string>>(result.Data);
        Assert.Equal(["ui.theme"], keys);
        _ = await service.Received(1).ListKeysAsync(cancellationToken);
    }

    [Fact]
    public void SettingsListKeysHandler_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new SettingsListKeysCommandHandler(settingsCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SettingsListKeysHandler_WhenServiceFails_PreservesFailureAndErrors()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.ListKeysAsync(Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.EnvironmentError,
                Message = "Settings unavailable.",
                Errors = ["Profile could not be loaded."],
            });

        var handler = new SettingsListKeysCommandHandler(service);
        var result = await handler.ExecuteAsync(new SettingsListKeysCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Equal("Settings unavailable.", result.Message);
        Assert.Equal(["Profile could not be loaded."], result.Errors);
    }

    [Fact]
    public async Task SettingsResetHandler_WhenServiceFails_ReturnsFailure()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.ResetAsync("unknown", Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = ["Key was not found."],
            });

        var handler = new SettingsResetCommandHandler(service);
        var result = await handler.ExecuteAsync(new SettingsResetCliOptions("unknown", JsonOutput: true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("Unknown settings key.", result.Message);
        Assert.Equal(["Key was not found."], result.Errors);
        _ = await service.Received(1).ResetAsync("unknown", CancellationToken.None);
    }

    [Fact]
    public void SettingsResetHandler_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new SettingsResetCommandHandler(settingsCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SettingsResetHandler_WhenServiceSucceeds_PreservesResultAndCancellation()
    {
        var service = Substitute.For<ISettingsCliService>();
        _ = service.ResetAsync("playback.speed", Arg.Any<CancellationToken>())
            .Returns(new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Settings reset.",
                Data = new Dictionary<string, object?>(StringComparer.Ordinal) { ["playback.speed"] = 1.0 },
            });

        var handler = new SettingsResetCommandHandler(service);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var result = await handler.ExecuteAsync(new SettingsResetCliOptions("playback.speed"), cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Settings reset.", result.Message);
        Assert.NotNull(result.Data);
        _ = await service.Received(1).ResetAsync("playback.speed", cancellationToken);
    }
}
