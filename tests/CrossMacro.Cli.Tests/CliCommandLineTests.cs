namespace CrossMacro.Cli.Tests;

public sealed class CliCommandLineTests
{
    [Fact]
    public void TryParse_WithAKnownCommand_ReturnsItsOptions()
    {
        var commandLine = CreateCommandLine(Substitute.For<ICliCommandHandlerResolver>());

        string[] arguments = ["settings", "get", "--json", "ui.theme"];

        var parsed = commandLine.TryParse(arguments, out var options, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        var settings = Assert.IsType<SettingsGetOptions>(options);
        Assert.True(settings.JsonOutput);
    }

    [Fact]
    public void TryParse_WithAnUnknownCommand_ReportsTheGrammarError()
    {
        var commandLine = CreateCommandLine(Substitute.For<ICliCommandHandlerResolver>());

        string[] arguments = ["no-such-command"];

        var parsed = commandLine.TryParse(arguments, out var options, out var error);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Unknown command: no-such-command", error);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    public void TryParse_WithAnInformationalToken_IsNotACommand(string token)
    {
        var commandLine = CreateCommandLine(Substitute.For<ICliCommandHandlerResolver>());

        string[] arguments = [token];

        var parsed = commandLine.TryParse(arguments, out var options, out var error);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Null(error);
    }

    [Fact]
    public async Task ExecuteAsync_RunsTheHandlerRegisteredForTheOptionsWithoutWritingOutput()
    {
        var options = new SettingsListKeysOptions();
        var handler = Substitute.For<ICliCommandHandler>();
        _ = handler.ExecuteAsync(options, Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("done"));
        var resolver = Substitute.For<ICliCommandHandlerResolver>();
        _ = resolver.Resolve(options).Returns(handler);
        using var console = new ConsoleCapture();

        var result = await CreateCommandLine(resolver).ExecuteAsync(options, CancellationToken.None);

        Assert.Equal("done", result.Message);
        Assert.Equal(string.Empty, console.StandardOutput);
        Assert.Equal(string.Empty, console.StandardError);
    }

    private static CliCommandLine CreateCommandLine(ICliCommandHandlerResolver resolver) =>
        new(new CliCommandExecutor(resolver));
}
