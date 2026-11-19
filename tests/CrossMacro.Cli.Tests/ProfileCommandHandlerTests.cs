using CrossMacro.Commands.Profiles;

namespace CrossMacro.Cli.Tests;

public sealed class ProfileCommandHandlerTests
{
    private readonly IProfileCommandService _service = Substitute.For<IProfileCommandService>();
    private readonly ProfileCommandHandler _handler;

    public ProfileCommandHandlerTests()
    {
        _ = _service.ListAsync(Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("list"));
        _ = _service.CurrentAsync(Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("current"));
        _ = _service.CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("create"));
        _ = _service.SwitchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("switch"));
        _ = _service.RenameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("rename"));
        _ = _service.DeleteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(OperationResult.Ok("delete"));
        _handler = new ProfileCommandHandler(_service);
    }

    [Theory]
    [InlineData(ProfileAction.List, "list")]
    [InlineData(ProfileAction.Current, "current")]
    [InlineData(ProfileAction.Create, "create")]
    [InlineData(ProfileAction.Switch, "switch")]
    [InlineData(ProfileAction.Rename, "rename")]
    [InlineData(ProfileAction.Delete, "delete")]
    public async Task ExecuteAsync_RoutesEachActionToItsOperation(ProfileAction action, string expectedMessage)
    {
        var result = await _handler.ExecuteAsync(new ProfileOptions(action, "work", "Office"), CancellationToken.None);

        Assert.Equal(expectedMessage, result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleting_ForwardsTheForceFlag()
    {
        _ = await _handler.ExecuteAsync(new ProfileOptions(ProfileAction.Delete, "work", Force: true), CancellationToken.None);
        _ = await _handler.ExecuteAsync(new ProfileOptions(ProfileAction.Delete, "work"), CancellationToken.None);

        _ = await _service.Received(1).DeleteAsync("work", force: true, CancellationToken.None);
        _ = await _service.Received(1).DeleteAsync("work", force: false, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRenaming_ForwardsIdentifierAndNewName()
    {
        _ = await _handler.ExecuteAsync(new ProfileOptions(ProfileAction.Rename, "work", "Office"), CancellationToken.None);

        _ = await _service.Received(1).RenameAsync("work", "Office", CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionIsUnknown_ReturnsInvalidArguments()
    {
        var result = await _handler.ExecuteAsync(new ProfileOptions((ProfileAction)999), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        Assert.Empty(_service.ReceivedCalls());
    }
}
