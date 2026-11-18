
namespace CrossMacro.Cli.Tests;

public sealed class ClipboardCommandHandlerTests
{
    private readonly IClipboardCliService _service = Substitute.For<IClipboardCliService>();
    private readonly ClipboardCommandHandler _handler;

    public ClipboardCommandHandlerTests()
    {
        _ = _service.GetAsync(Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("get"));
        _ = _service.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("set-text"));
        _ = _service.SetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("set-file"));
        _ = _service.ClearAsync(Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("clear"));
        _handler = new ClipboardCommandHandler(_service);
    }

    [Theory]
    [InlineData(ClipboardCliAction.Get, null, null, "get")]
    [InlineData(ClipboardCliAction.Clear, null, null, "clear")]
    [InlineData(ClipboardCliAction.Set, "hello", null, "set-text")]
    [InlineData(ClipboardCliAction.Set, null, "/tmp/in.txt", "set-file")]
    public async Task ExecuteAsync_RoutesEachActionToItsOperation(ClipboardCliAction action, string? text, string? filePath, string expectedMessage)
    {
        var result = await _handler.ExecuteAsync(new ClipboardCliOptions(action, text, filePath), CancellationToken.None);

        Assert.Equal(expectedMessage, result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSettingFromFile_PrefersTheFileOverText()
    {
        _ = await _handler.ExecuteAsync(new ClipboardCliOptions(ClipboardCliAction.Set, "ignored", "/tmp/in.txt"), CancellationToken.None);

        _ = await _service.Received(1).SetFileAsync("/tmp/in.txt", CancellationToken.None);
        _ = await _service.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSettingWithoutText_SetsTheEmptyString()
    {
        _ = await _handler.ExecuteAsync(new ClipboardCliOptions(ClipboardCliAction.Set), CancellationToken.None);

        _ = await _service.Received(1).SetTextAsync(string.Empty, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionIsUnknown_ReturnsInvalidArguments()
    {
        var result = await _handler.ExecuteAsync(new ClipboardCliOptions((ClipboardCliAction)999), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Empty(_service.ReceivedCalls());
    }
}
