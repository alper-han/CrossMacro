
namespace CrossMacro.Cli.Tests;

public sealed class ClipboardCommandHandlerTests
{
    private readonly IClipboardCommandService _service = Substitute.For<IClipboardCommandService>();
    private readonly ClipboardCommandHandler _handler;

    public ClipboardCommandHandlerTests()
    {
        _ = _service.GetAsync(Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("get"));
        _ = _service.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("set-text"));
        _ = _service.SetFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("set-file"));
        _ = _service.ClearAsync(Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("clear"));
        _handler = new ClipboardCommandHandler(_service);
    }

    [Theory]
    [InlineData(ClipboardAction.Get, null, null, "get")]
    [InlineData(ClipboardAction.Clear, null, null, "clear")]
    [InlineData(ClipboardAction.Set, "hello", null, "set-text")]
    [InlineData(ClipboardAction.Set, null, "/tmp/in.txt", "set-file")]
    public async Task ExecuteAsync_RoutesEachActionToItsOperation(ClipboardAction action, string? text, string? filePath, string expectedMessage)
    {
        var result = await _handler.ExecuteAsync(new ClipboardOptions(action, text, filePath), CancellationToken.None);

        Assert.Equal(expectedMessage, result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSettingFromFile_PrefersTheFileOverText()
    {
        _ = await _handler.ExecuteAsync(new ClipboardOptions(ClipboardAction.Set, "ignored", "/tmp/in.txt"), CancellationToken.None);

        _ = await _service.Received(1).SetFileAsync("/tmp/in.txt", CancellationToken.None);
        _ = await _service.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSettingWithoutText_SetsTheEmptyString()
    {
        _ = await _handler.ExecuteAsync(new ClipboardOptions(ClipboardAction.Set), CancellationToken.None);

        _ = await _service.Received(1).SetTextAsync(string.Empty, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionIsUnknown_ReturnsInvalidArguments()
    {
        var result = await _handler.ExecuteAsync(new ClipboardOptions((ClipboardAction)999), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        Assert.Empty(_service.ReceivedCalls());
    }
}
