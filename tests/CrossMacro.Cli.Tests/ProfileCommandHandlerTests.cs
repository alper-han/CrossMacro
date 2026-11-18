
namespace CrossMacro.Cli.Tests;

public sealed class ProfileCommandHandlerTests
{
    private readonly IProfileCliService _service = Substitute.For<IProfileCliService>();
    private readonly ProfileCommandHandler _handler;

    public ProfileCommandHandlerTests()
    {
        _ = _service.ListAsync(Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("list"));
        _ = _service.CurrentAsync(Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("current"));
        _ = _service.CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("create"));
        _ = _service.SwitchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("switch"));
        _ = _service.RenameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("rename"));
        _ = _service.DeleteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(CliCommandExecutionResult.Ok("delete"));
        _handler = new ProfileCommandHandler(_service);
    }

    [Theory]
    [InlineData(ProfileCliAction.List, "list")]
    [InlineData(ProfileCliAction.Current, "current")]
    [InlineData(ProfileCliAction.Create, "create")]
    [InlineData(ProfileCliAction.Switch, "switch")]
    [InlineData(ProfileCliAction.Rename, "rename")]
    [InlineData(ProfileCliAction.Delete, "delete")]
    public async Task ExecuteAsync_RoutesEachActionToItsOperation(ProfileCliAction action, string expectedMessage)
    {
        var result = await _handler.ExecuteAsync(new ProfileCliOptions(action, "work", "Office"), CancellationToken.None);

        Assert.Equal(expectedMessage, result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleting_ForwardsTheForceFlag()
    {
        _ = await _handler.ExecuteAsync(new ProfileCliOptions(ProfileCliAction.Delete, "work", Force: true), CancellationToken.None);
        _ = await _handler.ExecuteAsync(new ProfileCliOptions(ProfileCliAction.Delete, "work"), CancellationToken.None);

        _ = await _service.Received(1).DeleteAsync("work", force: true, CancellationToken.None);
        _ = await _service.Received(1).DeleteAsync("work", force: false, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRenaming_ForwardsIdentifierAndNewName()
    {
        _ = await _handler.ExecuteAsync(new ProfileCliOptions(ProfileCliAction.Rename, "work", "Office"), CancellationToken.None);

        _ = await _service.Received(1).RenameAsync("work", "Office", CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionIsUnknown_ReturnsInvalidArguments()
    {
        var result = await _handler.ExecuteAsync(new ProfileCliOptions((ProfileCliAction)999), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Empty(_service.ReceivedCalls());
    }
}
